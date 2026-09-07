namespace Bridge.AI.Bidding;

using Bridge.Core.Logging;
using Bridge.Core.Models;
using Bridge.Core.Strategies;

/// <summary>Moteur d'enchères fondé sur le Système d'Enseignement Français.</summary>
/// <remarks>Fichier principal : dispatch des situations d'enchère et helpers partagés.
/// Les chapitres du SEF sont implémentés dans les fichiers partiels SefBiddingEngine.*.cs.</remarks>
public sealed partial class SefBiddingEngine : ICompetitiveBiddingStrategy
{
    private readonly ILogger _logger;

    public SefBiddingEngine(ILogger? logger = null) => _logger = logger ?? LoggerProvider.GetLogger();

    public Bid? DecideBid(Seat seat, IReadOnlyList<Card> hand, Auction auction)
    {
        var call = DecideCall(seat, hand, auction);
        return call.Action == AuctionAction.Bid ? call.Bid : null;
    }

    public AuctionCall DecideCall(Seat seat, IReadOnlyList<Card> cards, Auction auction)
    {
        var hand = new HandEvaluation(cards);
        var sideBids = auction.Calls
            .Where(call => call.Action == AuctionAction.Bid && SameSide(call.Seat, seat))
            .ToList();
        _logger.Debug($"[SEF] {seat}: H={hand.HighCardPoints}, HL={hand.HonorLengthPoints}, HLD={hand.TotalPoints}");

        AuctionCall call;
        if (auction.HighestBid is null)
            call = BidCall(seat, Opening(hand));
        else if (auction.Calls.Any(call => SameSide(call.Seat, seat) && call.Action == AuctionAction.Double))
            call = RespondToTakeoutDouble(seat, hand, auction);
        else if (sideBids.Count == 0 && TryGetPartnerMinorOpening(seat, auction, out var partnerOpening))
            call = RespondToMinorOpeningIntervention(seat, hand, partnerOpening, auction);
        else if (sideBids.Count == 2 && sideBids[0].Seat == seat &&
                 TryGetPartnerResponseAfterIntervention(seat, auction, out var partnerResponse))
            call = RebidAfterMinorOpeningIntervention(seat, hand, sideBids[0].Bid!.Value, partnerResponse, auction);
        else if (sideBids.Count == 0)
            call = Intervention(seat, hand, auction);
        else
            call = ContinueAuction(seat, hand, auction, sideBids);

        if (call.Action == AuctionAction.Bid &&
            (!call.Bid.HasValue || !auction.IsLegal(call.Bid.Value)))
            call = CompetitiveFallback(seat, hand, auction);

        _logger.Information($"[SEF] {seat}: {CallText(call)}");
        return call;
    }

    private static bool TryGetPartnerMinorOpening(Seat seat, Auction auction, out Bid opening)
    {
        var partnerCall = auction.Calls.FirstOrDefault(call =>
            call.Seat == seat.Partner() && call.Action == AuctionAction.Bid);
        opening = partnerCall.Bid ?? default;
        return partnerCall.Action == AuctionAction.Bid && partnerCall.Bid is
            { Level: 1, Denomination: Denomination.Clubs or Denomination.Diamonds } &&
            auction.Calls.Any(call => call.Action == AuctionAction.Bid && !SameSide(call.Seat, seat));
    }

    private static bool TryGetPartnerResponseAfterIntervention(
        Seat seat, Auction auction, out Bid response)
    {
        var partnerCall = auction.Calls.LastOrDefault(call =>
            call.Seat == seat.Partner() && call.Action == AuctionAction.Bid);
        response = partnerCall.Bid ?? default;
        return partnerCall.Action == AuctionAction.Bid &&
            auction.Calls.Any(call => call.Action == AuctionAction.Bid &&
                !SameSide(call.Seat, seat) && call.Seat != seat.Partner());
    }

    private static AuctionCall ContinueAuction(Seat seat, HandEvaluation hand, Auction auction,
        IReadOnlyList<AuctionCall> sideBids)
    {
        var bids = sideBids.Select(call => call.Bid!.Value).ToList();
        var agreedTrump = FindAgreedTrump(bids);
        if (bids[^1] == new Bid(4, Denomination.NoTrump) && agreedTrump.HasValue)
            return BidCall(seat, BlackwoodResponse(hand, agreedTrump.Value));
        // 4SA direct sur un barrage : Blackwood, l'atout est la couleur d'ouverture.
        if (bids[^1] == new Bid(4, Denomination.NoTrump) && !agreedTrump.HasValue &&
            bids[0].Denomination != Denomination.NoTrump &&
            (bids[0].Level >= 3 || bids[0] is { Level: 2, Denomination: Denomination.Hearts or Denomination.Spades }))
            return BidCall(seat, BlackwoodResponse(hand, bids[0].Denomination));

        var opening = sideBids[0];
        if (opening.Seat != seat && sideBids.Count == 1)
        {
            if (opening.Bid is { Level: 1, Denomination: Denomination.Clubs or Denomination.Diamonds } &&
                auction.Calls.Any(call => call.Action == AuctionAction.Bid && !SameSide(call.Seat, seat)))
                return RespondToMinorOpeningIntervention(seat, hand, opening.Bid.Value, auction);
            if (opening.Bid == new Bid(2, Denomination.Clubs) &&
                auction.Calls.Any(call => call.Action == AuctionAction.Bid &&
                    call.Bid == new Bid(1, Denomination.NoTrump) && !SameSide(call.Seat, seat)))
                return BidCall(seat, RespondToLandy(hand));
            if (auction.Calls.Any(call => call.Action == AuctionAction.Bid &&
                !SameSide(call.Seat, seat)))
                return BidCall(seat, RespondToIntervention(hand, opening.Bid!.Value, auction));
            return BidCall(seat, FirstResponse(hand, opening.Bid!.Value));
        }
        if (opening.Seat == seat && sideBids.Count == 2)
            return BidCall(seat, OpenerRebid(hand, opening.Bid!.Value, sideBids[1].Bid!.Value));
        if (opening.Seat != seat && sideBids.Count == 3)
            return BidCall(seat, ResponderRebid(hand, opening.Bid!.Value,
                sideBids[1].Bid!.Value, sideBids[2].Bid!.Value));
        return AdvancedContinuation(seat, hand, auction, sideBids);
    }

    private static AuctionCall AdvancedContinuation(Seat seat, HandEvaluation hand, Auction auction,
        IReadOnlyList<AuctionCall> sideBids)
    {
        var bids = sideBids.Select(call => call.Bid!.Value).ToList();

        if (bids.Count is 4 or 5 && IsStrongNoTrumpRebid(bids))
        {
            var responderBid = bids[3];
            if (responderBid == new Bid(3, Denomination.Clubs))
                return BidCall(seat, bids.Count == 4
                    ? StaymanAnswer(hand, 3)
                    : AfterStayman(hand, bids[4], 3, NoTrumpOpenerMinimum(bids[0])));
            if (TexasTarget(responderBid, 2).HasValue)
                return BidCall(seat, bids.Count == 4
                    ? TwoNoTrumpRebid(hand, responderBid)
                    : AfterTexas(hand, responderBid, 2));
        }

        var agreedTrump = FindAgreedTrump(bids);

        if (bids[^1] == new Bid(4, Denomination.NoTrump) && agreedTrump.HasValue)
            return BidCall(seat, BlackwoodResponse(hand, agreedTrump.Value));

        if (bids.Count >= 2 && bids[^2] == new Bid(4, Denomination.NoTrump) && agreedTrump.HasValue)
            return BidCall(seat, BlackwoodConclusion(hand, bids[^1], agreedTrump.Value));

        // Suites de l'ouverture de 1SA : deuxième redemande de l'ouvreur.
        if (bids.Count == 4 && bids[0] == new Bid(1, Denomination.NoTrump))
        {
            if (bids[1] == new Bid(2, Denomination.Clubs))
                return BidCall(seat, StaymanContinuation(hand, bids));
            // Bicolore 5-5 : après Texas ♠ rectifié, l'annonce des cœurs demande un choix.
            if (bids[1] == new Bid(2, Denomination.Hearts) && bids[2] == new Bid(2, Denomination.Spades) &&
                bids[3].Denomination == Denomination.Hearts)
                return BidCall(seat, ChooseMajorGame(hand, bids[3]));
        }

        if (bids.Count == 5 && IsRoudiSequence(bids))
            return BidCall(seat, AfterRoudi(hand, bids[1].Denomination, bids[4]));

        if (bids.Count == 5 && IsFourthSuitSequence(bids))
            return BidCall(seat, AfterFourthSuit(hand, bids));

        if (IsRoudiQuestion(bids))
            return BidCall(seat, RoudiResponse(hand, bids[1].Denomination));

        if (IsFourthSuitForcing(bids))
            return BidCall(seat, FourthSuitResponse(hand, bids));

        return CompetitiveFallback(seat, hand, auction);
    }

    private static Denomination? FindAgreedTrump(IReadOnlyList<Bid> bids)
    {
        foreach (var denomination in new[]
                 { Denomination.Spades, Denomination.Hearts, Denomination.Diamonds, Denomination.Clubs })
        {
            var occurrences = bids.Select((bid, index) => (bid, index))
                .Count(item => item.bid.Denomination == denomination && !IsArtificialBid(bids, item.index));
            if (occurrences >= 2)
                return denomination;
        }
        return null;
    }

    private static bool IsStrongNoTrumpRebid(IReadOnlyList<Bid> bids) => bids.Count >= 4 &&
        bids[0] is { Level: 2, Denomination: Denomination.Clubs or Denomination.Diamonds } &&
        bids[2] == new Bid(2, Denomination.NoTrump);

    private static bool IsStrongNoTrumpStayman(IReadOnlyList<Bid> bids) =>
        IsStrongNoTrumpRebid(bids) && bids[3] == new Bid(3, Denomination.Clubs);

    private static bool IsArtificialBid(IReadOnlyList<Bid> bids, int index)
    {
        var bid = bids[index];
        if (index == 0 && bid.Level == 2 && bid.Denomination is Denomination.Clubs or Denomination.Diamonds)
            return true;
        if (index == 1 && bids[0] == new Bid(2, Denomination.Clubs) &&
            bid == new Bid(2, Denomination.Diamonds))
            return true;
        if (index == 3 && IsStrongNoTrumpStayman(bids.Take(4).ToList()))
            return true;
        if (index > 0 && bids[index - 1].Denomination == Denomination.NoTrump &&
            bid.Denomination == Denomination.Clubs && bid.Level == bids[index - 1].Level + 1)
            return true;
        return false;
    }

    // HLD revalorisés après fit : +2 pour le 9e atout, +1 par atout supplémentaire.
    private static int FitPoints(HandEvaluation hand, Suit trump, int partnerLength)
    {
        var combined = hand.SuitLength(trump) + partnerLength;
        return hand.TotalPoints + (combined >= 9 ? combined - 7 : 0);
    }

    private static bool HasGuard(HandEvaluation hand, Suit suit) =>
        hand.Cards.Any(card => card.Suit == suit && card.Rank >= Rank.King) ||
        hand.Cards.Contains(new Card(suit, Rank.Queen)) && hand.SuitLength(suit) >= 3;

    private static Denomination? BestNewSuit(HandEvaluation hand, Denomination excluded, int minimumLength) =>
        Enum.GetValues<Suit>().Where(suit => ToDenomination(suit) != excluded && hand.SuitLength(suit) >= minimumLength)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit)
            .Select(ToDenomination).Cast<Denomination?>().FirstOrDefault();

    private static Denomination? BestMinor(HandEvaluation hand, int minimumLength) =>
        new[] { Suit.Diamonds, Suit.Clubs }.Where(suit => hand.SuitLength(suit) >= minimumLength)
            .OrderByDescending(hand.SuitLength).Select(ToDenomination).Cast<Denomination?>().FirstOrDefault();

    private static Bid CheapestBid(Denomination denomination, Bid over)
    {
        var sameLevel = new Bid(over.Level, denomination);
        return Auction.Compare(sameLevel, over) > 0 ? sameLevel : new(over.Level + 1, denomination);
    }

    private static AuctionCall BidCall(Seat seat, Bid? bid) =>
        new(seat, bid.HasValue ? AuctionAction.Bid : AuctionAction.Pass, bid);
    private static Denomination ToDenomination(Suit suit) => (Denomination)suit;
    private static bool SameSide(Seat left, Seat right) => left.IsNorthSouth() == right.IsNorthSouth();
    private static string CallText(AuctionCall call) => call.Action switch
    {
        AuctionAction.Pass => "Passe",
        AuctionAction.Double => "Contre",
        AuctionAction.Redouble => "Surcontre",
        _ => call.Bid?.ToString() ?? "Passe"
    };
}
