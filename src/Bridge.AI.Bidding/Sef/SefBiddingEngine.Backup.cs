namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

public sealed partial class SefBiddingEngine
{
    private readonly record struct BackupPartnerEstimate(int HighCardPoints, int HonorLengthPoints);

    private AuctionCall EvaluateAndChooseBackup(Seat seat, HandEvaluation hand, Auction auction, string trigger)
    {
        var sideBids = auction.Calls.Where(call => SameSide(call.Seat, seat) && call.Bid.HasValue).ToArray();
        var partnerCalls = auction.Calls.Where(call => call.Seat == seat.Partner() && call.Bid.HasValue).ToArray();
        var partner = EstimatePartnerMinimum(seat, auction, partnerCalls);
        var combinedH = BackupHighCardPoints(hand) + partner.HighCardPoints;
        var combinedHL = hand.HonorLengthPoints + partner.HonorLengthPoints;
        var trump = FindAgreedTrump(sideBids.Select(call => call.Bid!.Value).ToArray());
        var fitLength = trump.HasValue
            ? hand.SuitLength((Suit)trump.Value) + PartnerTrumpLength(seat, trump.Value, auction)
            : 0;
        var hasFit = trump.HasValue && fitLength >= Profile.Backup.MinimumAgreedFit;
        var fitPoints = hasFit ? BackupFitPoints(hand, fitLength, partner.HighCardPoints) : 0;
        var safeNoTrump = BackupNoTrumpIsSafe(hand);
        var gameAvailable = hasFit
            ? fitPoints >= Profile.Backup.GameFitPoints
            : safeNoTrump && combinedH >= Profile.Backup.GameHighCardPoints;
        var slamAvailable = hasFit
            ? fitPoints >= Profile.Backup.SlamFitPoints
            : safeNoTrump && combinedH >= Profile.Backup.SlamHighCardPoints;
        var fitText = hasFit
            ? $"fit agréé {fitLength} cartes à {SuitName(trump!.Value)}"
            : "aucun fit agréé";
        var strengthText = $"force combinée estimée {combinedH} H / {combinedHL} HL, soutien {fitPoints} HLD";
        var activeForcing = auction.ForcingFor(seat);

        AuctionCall Finish(AuctionCall call, string decision)
        {
            LastForcing = activeForcing != AuctionForcing.None
                ? ToRuleForcing(activeForcing)
                : ToRuleForcing(call.Forcing);
            LastDecisionRule = $"Secours unique ({trigger}) : {fitText}; {strengthText}; {decision}";
            _logger.Information($"[Backup] [{Profile.Id}] {seat}: {CallText(call)} - {LastDecisionRule}");
            return call;
        }

        if (activeForcing != AuctionForcing.None)
        {
            var forcedCall = EnforceForcing(seat, hand, auction, new AuctionCall(seat, AuctionAction.Pass));
            if (forcedCall.Action == AuctionAction.Bid)
                return Finish(forcedCall, $"Continuation de sécurité : respect de l'obligation {activeForcing}, prioritaire sur les seuils de force");
        }

        if (slamAvailable && hasFit && Profile.IsConventionEnabled("blackwood") &&
            auction.IsLegal(new Bid(4, Denomination.NoTrump)))
            return Finish(new AuctionCall(seat, AuctionAction.Bid, new Bid(4, Denomination.NoTrump), AuctionForcing.OneRound),
                $"seuil de chelem {Profile.Backup.SlamFitPoints} atteint, exploration Blackwood à {SuitName(trump!.Value)}");

        var lastBidder = auction.Calls.LastOrDefault(call => call.Action == AuctionAction.Bid).Seat;
        if (slamAvailable && !hasFit && safeNoTrump && auction.HighestBid == new Bid(3, Denomination.NoTrump) &&
            SameSide(lastBidder, seat) && auction.IsLegal(new Bid(4, Denomination.NoTrump)))
            return Finish(new AuctionCall(seat, AuctionAction.Bid, new Bid(4, Denomination.NoTrump)),
                "seuil de chelem sans fit atteint, proposition quantitative à 4SA");

        if (gameAvailable)
        {
            Bid? game = null;
            if (hasFit && trump is { } agreedTrump)
            {
                if (agreedTrump is Denomination.Hearts or Denomination.Spades)
                    game = new Bid(4, agreedTrump);
                else if (safeNoTrump && combinedH >= Profile.Backup.GameHighCardPoints)
                    game = new Bid(3, Denomination.NoTrump);
                else
                    game = new Bid(5, agreedTrump);
            }
            else if (safeNoTrump)
                game = new Bid(3, Denomination.NoTrump);

            if (game is { } gameBid && auction.IsLegal(gameBid))
                return Finish(new AuctionCall(seat, AuctionAction.Bid, gameBid), "force combinée suffisante pour la manche");
        }

        var lastContract = auction.Calls.LastOrDefault(call => call.Bid.HasValue);
        if (lastContract.Bid.HasValue && SameSide(lastContract.Seat, seat))
            return Finish(new AuctionCall(seat, AuctionAction.Pass),
                "force insuffisante pour la manche, conservation du partiel déjà détenu par le camp");
        if (partnerCalls.Length == 0)
            return Finish(new AuctionCall(seat, AuctionAction.Pass), auction.HighestBid.HasValue
                ? "aucune enchère partenaire pour soutenir un contrat adverse"
                : "aucun contrat partenaire à soutenir");

        var partial = FindSafestPartScore(hand, auction, hasFit ? trump : null,
            safeNoTrump, sideBids, partnerCalls);
        return partial is { } partScore
            ? Finish(new AuctionCall(seat, AuctionAction.Bid, partScore),
                "force insuffisante pour la manche, choix du partiel légal le plus bas et le moins risqué")
            : Finish(new AuctionCall(seat, AuctionAction.Pass), "aucun partiel sûr et légal disponible");
    }

    private static RuleForcing ToRuleForcing(AuctionForcing forcing) => forcing switch
    {
        AuctionForcing.OneRound => RuleForcing.OneRound,
        AuctionForcing.SelfForcing => RuleForcing.SelfForcing,
        AuctionForcing.GameForcing => RuleForcing.GameForcing,
        _ => RuleForcing.NonForcing
    };

    private BackupPartnerEstimate EstimatePartnerMinimum(Seat seat, Auction auction,
        IReadOnlyList<AuctionCall> partnerCalls)
    {
        if (partnerCalls.Count == 0) return new(0, 0);
        var first = partnerCalls[0].Bid!.Value;
        var firstSideBid = auction.Calls.FirstOrDefault(call => SameSide(call.Seat, seat) && call.Bid.HasValue);
        var partnerOpened = firstSideBid.Seat == seat.Partner() && firstSideBid.Bid == first;
        int high;
        int hl;
        if (partnerOpened)
        {
            var passedBefore = auction.Calls.TakeWhile(call => call != firstSideBid)
                .Any(call => call.Seat == seat.Partner() && call.IsPass);
            (high, hl) = first switch
            {
                { Level: 1, Denomination: Denomination.NoTrump } =>
                    (Profile.Backup.PartnerOneNoTrumpMinimumH, Profile.Backup.PartnerOneNoTrumpMinimumH),
                { Level: 1 } => (passedBefore ? 8 : Profile.Backup.PartnerOneLevelMinimumH,
                    passedBefore ? 8 : Profile.Backup.PartnerOneLevelMinimumHL),
                { Level: 2, Denomination: Denomination.Clubs } =>
                    (Math.Max(0, Profile.Backup.PartnerStrongTwoClubsMinimumHL - 3), Profile.Backup.PartnerStrongTwoClubsMinimumHL),
                { Level: 2, Denomination: Denomination.Diamonds } =>
                    (Math.Max(0, Profile.Backup.PartnerGameForceMinimumHL - 4), Profile.Backup.PartnerGameForceMinimumHL),
                { Level: 2, Denomination: Denomination.NoTrump } =>
                    (Math.Max(0, Profile.TwoNoTrump.Minimum - 3), Profile.TwoNoTrump.Minimum),
                { Level: 2 } => (5, 5),
                { Level: 3 } => (4, 4),
                { Level: 4 } => (5, 5),
                _ => (0, 0)
            };
        }
        else
        {
            (high, hl) = first switch
            {
                { Level: 1 } => (6, 6),
                { Level: 2, Denomination: Denomination.NoTrump } => (9, 11),
                { Level: 2 } => (11, 12),
                { Level: 3, Denomination: Denomination.NoTrump } => (13, 15),
                { Level: 3 } => (10, 11),
                _ => (6, 6)
            };
        }

        foreach (var later in partnerCalls.Skip(1).Select(call => call.Bid!.Value))
        {
            if (later == new Bid(2, Denomination.NoTrump)) { high = Math.Max(high, 15); hl = Math.Max(hl, 15); }
            if (later == new Bid(3, Denomination.NoTrump)) { high = Math.Max(high, 18); hl = Math.Max(hl, 18); }
        }
        return new(high, hl);
    }

    private int PartnerTrumpLength(Seat seat, Denomination trump, Auction auction)
    {
        var calls = auction.Calls.Where(call => call.Seat == seat.Partner() && call.Bid.HasValue).ToArray();
        var promised = 0;
        foreach (var call in calls)
        {
            var bid = call.Bid!.Value;
            if (bid.Denomination != trump) continue;
            var supportsOurSuit = auction.Calls.TakeWhile(previous => previous != call)
                .Any(previous => SameSide(previous.Seat, seat) && previous.Bid?.Denomination == trump);
            if (supportsOurSuit)
                promised = Math.Max(promised, trump is Denomination.Hearts or Denomination.Spades
                    ? bid.Level switch { 2 => 3, 3 => 4, _ => 5 }
                    : bid.Level switch { 2 => 4, _ => 5 });
            else if (bid.Level == 1)
                promised = Math.Max(promised, trump is Denomination.Hearts or Denomination.Spades ? 5 : 3);
            else if (calls[0] == call)
                promised = Math.Max(promised, bid.Level switch { 2 => 6, 3 => 7, _ => 8 });
            else
            {
                var fiveHeartsOnTwo = bid.Level == 2 && trump == Denomination.Hearts &&
                    auction.Calls.Any(previous => SameSide(previous.Seat, seat) && previous.Bid == new Bid(1, Denomination.Spades));
                promised = Math.Max(promised, fiveHeartsOnTwo ? 5 : 4);
            }
        }
        return promised;
    }

    private int BackupHighCardPoints(HandEvaluation hand) => hand.Cards.Sum(card => card.Rank switch
    {
        Rank.Ace => Profile.Evaluation.Ace,
        Rank.King => Profile.Evaluation.King,
        Rank.Queen => Profile.Evaluation.Queen,
        Rank.Jack => Profile.Evaluation.Jack,
        _ => 0
    });

    private int BackupFitPoints(HandEvaluation hand, int fitLength, int partnerHonorLength)
    {
        var settings = Profile.Evaluation;
        var length = Enum.GetValues<Suit>().Sum(suit =>
            Math.Max(0, hand.SuitLength(suit) - settings.LengthStartsAt + 1) * settings.LengthPoint);
        var distribution = Enum.GetValues<Suit>().Sum(suit => hand.SuitLength(suit) switch
        {
            0 => settings.Void,
            1 => settings.Singleton,
            2 => settings.Doubleton,
            _ => 0
        });
        var trumpBonus = fitLength >= 9
            ? settings.NinthTrump + (fitLength - 9) * settings.ExtraTrump
            : 0;
        return BackupHighCardPoints(hand) + length + distribution + trumpBonus + partnerHonorLength;
    }

    private bool BackupNoTrumpIsSafe(HandEvaluation hand) => hand.IsBalanced &&
        (!Profile.Backup.RequireAllStopperForNoTrump || Enum.GetValues<Suit>().All(suit => HasGuard(suit, hand)));

    private Bid? LowestPartScore(Denomination denomination, Auction auction)
    {
        for (var level = 1; level < BackupGameLevel(denomination); level++)
        {
            var bid = new Bid(level, denomination);
            if (auction.IsLegal(bid)) return bid;
        }
        return null;
    }

    private static int BackupGameLevel(Denomination denomination) => denomination == Denomination.NoTrump
        ? 3
        : denomination is Denomination.Hearts or Denomination.Spades ? 4 : 5;

    private Bid? FindSafestPartScore(HandEvaluation hand, Auction auction, Denomination? fit,
        bool safeNoTrump, IReadOnlyList<AuctionCall> sideBids, IReadOnlyList<AuctionCall> partnerCalls)
    {
        if (fit.HasValue && LowestPartScore(fit.Value, auction) is { } fitBid) return fitBid;
        if (safeNoTrump && LowestPartScore(Denomination.NoTrump, auction) is { } noTrumpBid) return noTrumpBid;

        var knownSuits = sideBids.Select(call => call.Bid!.Value.Denomination)
            .Concat(partnerCalls.Select(call => call.Bid!.Value.Denomination))
            .Where(color => color != Denomination.NoTrump && hand.SuitLength((Suit)color) >= 2);
        var longSuits = Enum.GetValues<Suit>()
            .Where(suit => hand.SuitLength(suit) >= Profile.Backup.MinimumNaturalSuitLength)
            .Select(suit => (Denomination)suit);
        return knownSuits.Concat(longSuits).Distinct()
            .OrderByDescending(color => hand.SuitLength((Suit)color))
            .ThenByDescending(color => color)
            .Select(color => LowestPartScore(color, auction))
            .FirstOrDefault(candidate => candidate.HasValue);
    }
}