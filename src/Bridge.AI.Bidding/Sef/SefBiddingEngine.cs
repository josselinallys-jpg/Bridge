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

    public string LastDecisionRule { get; private set; } = "Aucune décision";
    public BiddingProfile Profile { get; }

    public SefBiddingEngine(ILogger? logger = null, BiddingProfile? profile = null)
    {
        var selected = profile ?? new BiddingProfile();
        selected.Validate();
        var rules = selected.Rules.Select(rule => rule with
        {
            SideSequence = Array.AsReadOnly(rule.SideSequence.ToArray()),
            OpponentSequence = Array.AsReadOnly(rule.OpponentSequence.ToArray()),
            AuctionSequence = Array.AsReadOnly(rule.AuctionSequence.ToArray()),
            Conditions = Array.AsReadOnly(rule.Conditions.ToArray()),
            CandidateSuits = Array.AsReadOnly(rule.CandidateSuits.ToArray()),
            RequiresConventions = Array.AsReadOnly(rule.RequiresConventions.ToArray()),
            DisabledConventions = Array.AsReadOnly(rule.DisabledConventions.ToArray()),
            AnyOf = Array.AsReadOnly(rule.AnyOf.Select(group => (IReadOnlyList<HandCondition>)Array.AsReadOnly(group.ToArray())).ToArray())
        }).ToArray();
        Profile = selected with
        {
            Rules = Array.AsReadOnly(rules),
            CustomConventions = new System.Collections.ObjectModel.ReadOnlyDictionary<string, bool>(
                new Dictionary<string, bool>(selected.CustomConventions))
        };
        _configuredRules = rules.OrderByDescending(rule => rule.Priority).ToArray();
        _logger = logger ?? LoggerProvider.GetLogger();
    }

    public BidExplanation ExplainCall(Seat seat, IReadOnlyList<Card> cards,
        IReadOnlyList<AuctionCall> previousCalls, AuctionCall call)
    {
        var hand = new HandEvaluation(cards);
        var values = new Dictionary<string, string>
        {
            ["H"] = hand.HighCardPoints.ToString(),
            ["HL"] = hand.HonorLengthPoints.ToString(),
            ["HLD"] = hand.TotalPoints.ToString(),
            ["profil"] = Profile.Id,
            ["minimum1SA"] = Profile.OneNoTrump.Minimum.ToString(),
            ["maximum1SA"] = Profile.OneNoTrump.Maximum.ToString(),
            ["minimum2SA"] = Profile.TwoNoTrump.Minimum.ToString(),
            ["maximum2SA"] = Profile.TwoNoTrump.Maximum.ToString(),
            ["majeureCinquieme1SA"] = Profile.OneNoTrump.AllowFiveCardMajor ? "possible selon la distribution" : "exclue",
            ["majeureCinquieme2SA"] = Profile.TwoNoTrump.AllowFiveCardMajor ? "possible selon la distribution" : "exclue",
            ["minimumStayman"] = Profile.Conventions.StaymanMinimumHL.ToString(),
            ["minimumStayman2SA"] = Profile.Conventions.TwoNoTrumpStaymanMinimumHL.ToString(),
            ["palier"] = call.Bid?.Level.ToString() ?? "",
            ["couleur"] = call.Bid is { } bidValue ? SuitName(bidValue.Denomination) : "",
            ["cartes"] = call.Bid is { } suitBid && suitBid.Denomination != Denomination.NoTrump
                ? hand.SuitLength((Suit)suitBid.Denomination).ToString() : ""
        };

        if (_configuredRules.Length > 0)
        {
            var previousAuction = new Auction();
            foreach (var previous in previousCalls) previousAuction.AddCall(previous);
            if (TryConfiguredCall(seat, hand, previousAuction, out var configured, out var rule, call) ||
                TryConfiguredCall(seat, hand, previousAuction, out configured, out rule, call, describeMeaning: true))
            {
                var explainedCall = call with { Forcing = configured.Forcing };
                values["descriptionRegle"] = BiddingRuleText.Description(rule!, explainedCall);
                values["forcing"] = BiddingRuleText.Forcing(rule!.Response.Forcing!.Value);
                values["conditionsRegle"] = BiddingRuleText.Conditions(rule);
                return Explanation(rule.Id, values);
            }
            var bids = previousCalls.Where(previous => SameSide(previous.Seat, seat) && previous.Bid.HasValue)
                .Select(previous => previous.Bid!.Value).ToArray();
            if (call.Action == AuctionAction.Pass && MinorTexasTrump(bids).HasValue && MinorTexasShortMajor(bids).HasValue &&
                !bids.Any(previous => previous.Level >= (previous.Denomination == Denomination.NoTrump ? 3 :
                    previous.Denomination is Denomination.Hearts or Denomination.Spades ? 4 : 5)))
                return Explanation("pass-forcing", values);
        }

        if (call.Action == AuctionAction.Pass) return Explanation("pass", values);
        if (call.Action == AuctionAction.Double) return Explanation("double", values);
        if (call.Action == AuctionAction.Redouble) return Explanation("redouble", values);
        if (call.Bid is not { } bid) return Explanation("unknown", values);

        var hasPreviousBid = previousCalls.Any(existing => existing.Action == AuctionAction.Bid);
        var ownBids = previousCalls.Where(existing => existing.Action == AuctionAction.Bid && existing.Seat == seat).ToList();
        var partnerBids = previousCalls.Where(existing => existing.Action == AuctionAction.Bid && existing.Seat == seat.Partner()).ToList();
        var partnerLast = partnerBids.LastOrDefault().Bid;

        values["HLD"] = TooltipHld(hand, seat, bid, previousCalls).ToString();
        if (!hasPreviousBid && bid == new Bid(3, Denomination.NoTrump))
        {
            var gamblingMinor = hand.SuitLength(Suit.Clubs) >= 7 ? Suit.Clubs : Suit.Diamonds;
            values["couleur"] = SuitName(ToDenomination(gamblingMinor));
            values["cartes"] = hand.SuitLength(gamblingMinor).ToString();
        }

        if (!hasPreviousBid) return ExplainOpeningCall(bid, values);
        if (ownBids.Count == 0 && partnerBids.Count == 0)
            return ExplainInterventionCall(bid, hand, values, previousCalls);

        var oneNoTrumpOpening = previousCalls.FirstOrDefault(existing =>
            existing.Action == AuctionAction.Bid && existing.Bid == new Bid(1, Denomination.NoTrump));
        if (oneNoTrumpOpening.Action == AuctionAction.Bid)
        {
            var oneNoTrumpRule = ExplainOneNoTrumpCall(bid, seat, oneNoTrumpOpening, ownBids, partnerBids, hand, values);
            if (oneNoTrumpRule is not null) return oneNoTrumpRule;
        }

        var strongRule = ExplainStrongOpeningCall(bid, seat, previousCalls, ownBids, partnerBids, hand, values);
        if (strongRule is not null) return strongRule;

        var partnerPreempt = partnerBids.Any(existing => existing.Bid is
            { Level: 2, Denomination: Denomination.Hearts or Denomination.Spades } or
            { Level: 3 or 4, Denomination: not Denomination.NoTrump });
        if (bid == new Bid(4, Denomination.NoTrump) &&
            (HasAgreedTrump(previousCalls, seat) || partnerPreempt))
            return Explanation("blackwood-ask", values);
        if (partnerBids.Any(existing => existing.Bid == new Bid(4, Denomination.NoTrump)))
            return Explanation(bid switch
            {
                { Level: 5, Denomination: Denomination.Clubs } => "blackwood-0-3",
                { Level: 5, Denomination: Denomination.Diamonds } => "blackwood-1-4",
                { Level: 5, Denomination: Denomination.Hearts } => "blackwood-2-5-no-queen",
                { Level: 5, Denomination: Denomination.Spades } => "blackwood-2-5-queen",
                { Level: 5, Denomination: Denomination.NoTrump } => "blackwood-void-2-4",
                { Level: 6 } => "blackwood-void-1-3",
                _ => "unknown"
            }, values);
        if (bid.Level is >= 5 and <= 7 && HasAgreedTrump(previousCalls, seat))
            return Explanation("slam", values);
        if (partnerLast is { Denomination: Denomination.Hearts or Denomination.Spades })
        {
            if (bid.Denomination == partnerLast.Value.Denomination && bid.Level is >= 2 and <= 4)
                return Explanation($"major-support-{bid.Level}", values);
            if (bid.Denomination is Denomination.Hearts or Denomination.Spades && bid.Level is 1 or 2 or 3)
                return Explanation($"major-change-{(bid.Level == 1 ? 1 : 2)}", values);
        }
        if (bid.Denomination == Denomination.NoTrump && bid.Level is >= 1 and <= 4)
            return Explanation("nt", values);
        if (bid.Denomination is Denomination.Clubs or Denomination.Diamonds && bid.Level is 1 or 2 or 3)
            return Explanation("minor-change", values);
        return Explanation("unknown", values);
    }

    private BidExplanation? ExplainOneNoTrumpCall(Bid bid, Seat seat, AuctionCall opening,
        IReadOnlyList<AuctionCall> ownBids, IReadOnlyList<AuctionCall> partnerBids,
        HandEvaluation hand, Dictionary<string, string> values)
    {
        var partnerLast = partnerBids.LastOrDefault().Bid;
        if (opening.Seat == seat.Partner() && ownBids.Count == 0 && partnerLast == opening.Bid)
        {
            if (Profile.Conventions.Stayman && bid == new Bid(2, Denomination.Clubs) &&
                hand.HasFourCardMajor && hand.HonorLengthPoints >= Profile.Conventions.StaymanMinimumHL)
                return Explanation("one-nt-stayman", values);
            if (Profile.Conventions.MajorTransfers && bid == new Bid(4, Denomination.Diamonds) &&
                hand.SuitLength(Suit.Hearts) >= 5 && hand.SuitLength(Suit.Spades) >= 5)
            {
                values["zone"] = $"{hand.HighCardPoints}H";
                return Explanation("one-nt-major-two-suited", values);
            }
            if (Profile.Conventions.MajorTransfers && bid == new Bid(2, Denomination.Diamonds) && hand.SuitLength(Suit.Hearts) >= 5)
                return Explanation("one-nt-texas-hearts", values);
            if (Profile.Conventions.MajorTransfers && bid == new Bid(2, Denomination.Hearts) && hand.SuitLength(Suit.Spades) >= 5)
                return Explanation("one-nt-texas-spades", values);
            if (Profile.Conventions.MinorTransfers &&
                bid is ({ Level: 2, Denomination: Denomination.Spades } or { Level: 3, Denomination: Denomination.Clubs }))
                return Explanation("one-nt-texas-minor", values);
            if (bid.Denomination == Denomination.NoTrump && bid.Level is >= 2 and <= 7)
                return Explanation("one-nt-quantitative", values);
            return Explanation("natural", values);
        }

        if (Profile.Conventions.Stayman && opening.Seat == seat && ownBids.Count == 1 && partnerLast == new Bid(2, Denomination.Clubs))
        {
            return bid.Denomination switch
            {
                Denomination.Diamonds => Explanation("stayman-no-major", values),
                Denomination.Hearts => Explanation("stayman-four-hearts", values),
                Denomination.Spades => Explanation("stayman-four-spades", values),
                Denomination.NoTrump => Explanation("stayman-both-majors", values),
                _ => null
            };
        }

        if (Profile.Conventions.Stayman && opening.Seat != seat && ownBids.Count > 0 && ownBids[0].Bid == new Bid(2, Denomination.Clubs) &&
            partnerLast == new Bid(2, Denomination.NoTrump))
        {
            if (bid is { Level: 3, Denomination: Denomination.Clubs or Denomination.Diamonds })
                return Explanation("stayman-two-nt-transfer", values);
            if (bid is { Level: 4, Denomination: Denomination.Clubs or Denomination.Diamonds })
                return Explanation("stayman-two-nt-transfer-game", values);
        }

        if (Profile.Conventions.Stayman && opening.Seat == seat && ownBids.Count >= 2 && ownBids[^1].Bid == new Bid(2, Denomination.NoTrump))
        {
            var transfer = partnerLast switch
            {
                { Level: 3, Denomination: Denomination.Clubs } => ((Suit Target, bool GameForcing)?)(Suit.Hearts, false),
                { Level: 3, Denomination: Denomination.Diamonds } => (Suit.Spades, false),
                { Level: 4, Denomination: Denomination.Clubs } => (Suit.Hearts, true),
                { Level: 4, Denomination: Denomination.Diamonds } => (Suit.Spades, true),
                _ => ((Suit Target, bool GameForcing)?)null
            };
            if (transfer.HasValue)
            {
                var (targetSuit, gameForcing) = transfer.Value;
                if (bid.Denomination == ToDenomination(targetSuit) &&
                    hand.SuitLength(targetSuit) >= 3 && bid.Level is 3 or 4)
                    return Explanation(gameForcing
                        ? "stayman-two-nt-transfer-game-rectification"
                        : "stayman-two-nt-transfer-rectification", values);
                if (bid == new Bid(3, Denomination.NoTrump))
                    return Explanation("stayman-two-nt-transfer-no-fit", values);
                if (bid.Denomination == ToDenomination(targetSuit))
                    return Explanation("stayman-two-nt-transfer-no-fit", values);
            }
        }

        return null;
    }

    private static BidExplanation ExplainOpeningCall(Bid bid, Dictionary<string, string> values)
    {
        var id = bid switch
        {
            { Level: 1, Denomination: Denomination.NoTrump } => "opening-1nt",
            { Level: 2, Denomination: Denomination.NoTrump } => "opening-2nt",
            { Level: 1, Denomination: Denomination.Clubs or Denomination.Diamonds } => "opening-minor",
            { Level: 1, Denomination: Denomination.Hearts or Denomination.Spades } => "opening-major",
            { Level: 2, Denomination: Denomination.Clubs } => "strong-two-clubs-opening",
            { Level: 2, Denomination: Denomination.Diamonds } => "strong-two-diamonds-opening",
            { Level: 2, Denomination: Denomination.Hearts or Denomination.Spades } => "weak-two",
            { Level: 3, Denomination: Denomination.NoTrump } => "strong-gambling-three-no-trump",
            { Level: 3 or 4 } => "preempt",
            _ => "unknown"
        };
        return Explanation(id, values);
    }

    private static BidExplanation ExplainInterventionCall(Bid bid, HandEvaluation hand,
        Dictionary<string, string> values, IReadOnlyList<AuctionCall> previousCalls)
    {
        if (bid == new Bid(2, Denomination.Clubs) && previousCalls.Any(call =>
            call.Action == AuctionAction.Bid && call.Bid == new Bid(1, Denomination.NoTrump)))
            return Explanation("landy-two-suited", values);
        if (bid.Level == 1 && bid.Denomination is Denomination.Hearts or Denomination.Spades)
            return Explanation("intervention-major", values);
        if (bid.Level == 1 && bid.Denomination is Denomination.Clubs or Denomination.Diamonds)
            return Explanation("intervention-minor", values);
        if (bid.Level == 2 && bid.Denomination != Denomination.NoTrump && hand.SuitLength((Suit)bid.Denomination) >= 5)
            return Explanation("intervention-two-level", values);
        if (bid.Denomination == Denomination.NoTrump && bid.Level is 1 or 2)
            return Explanation("intervention-nt", values);
        return Explanation("unknown", values);
    }

    private BidExplanation? ExplainStrongOpeningCall(Bid bid, Seat seat,
        IReadOnlyList<AuctionCall> previousCalls, IReadOnlyList<AuctionCall> ownBids,
        IReadOnlyList<AuctionCall> partnerBids, HandEvaluation hand, Dictionary<string, string> values)
    {
        var opening = previousCalls.FirstOrDefault(call => call.Action == AuctionAction.Bid);
        var partnerLast = partnerBids.LastOrDefault().Bid;
        if (opening.Bid == new Bid(2, Denomination.Clubs))
        {
            if (opening.Seat != seat && ownBids.Count == 0 && partnerLast == opening.Bid)
            {
                if (bid == new Bid(2, Denomination.Diamonds))
                    return Explanation("strong-two-clubs-relay", values);
                if (previousCalls.Any(call => call.Action is AuctionAction.Bid or AuctionAction.Double &&
                    !SameSide(call.Seat, seat)))
                    return Explanation("strong-opening-interference", values);
            }
            if (opening.Seat == seat && ownBids.Count == 1 && partnerLast == new Bid(2, Denomination.Diamonds))
                return Explanation("strong-two-clubs-description", values);
            if (opening.Seat != seat && ownBids.Count > 0 && partnerLast is { Denomination: not Denomination.NoTrump })
                return Explanation("strong-two-clubs-development", values);
        }

        if (opening.Bid == new Bid(2, Denomination.Diamonds))
        {
            if (opening.Seat != seat && ownBids.Count == 0 && partnerLast == opening.Bid)
            {
                if (bid == new Bid(2, Denomination.Hearts))
                    return Explanation("strong-two-diamonds-no-ace", values);
                if (bid == new Bid(2, Denomination.NoTrump))
                    return Explanation("strong-two-diamonds-no-ace-regular", values);
                if (bid == new Bid(2, Denomination.Spades))
                    return Explanation("strong-two-diamonds-major-ace", values);
                if (bid is { Level: 3, Denomination: Denomination.Clubs or Denomination.Diamonds })
                    return Explanation("strong-two-diamonds-minor-ace", values);
                if (bid is { Level: 3, Denomination: Denomination.Hearts or Denomination.Spades or Denomination.NoTrump })
                    return Explanation("strong-two-diamonds-ace-group", values);
            }
            if (opening.Seat == seat && ownBids.Count > 0)
                return Explanation("strong-two-diamonds-rebid", values);
            if (opening.Seat != seat && ownBids.Count > 0)
                return Explanation("strong-two-diamonds-development", values);
        }

        if (opening.Bid == new Bid(2, Denomination.NoTrump))
        {
            if (opening.Seat != seat && ownBids.Count == 0 && partnerLast == opening.Bid)
            {
                if (Profile.Conventions.Stayman && bid == new Bid(3, Denomination.Clubs)) return Explanation("strong-two-nt-stayman", values);
                if (Profile.Conventions.MajorTransfers && bid is { Level: 3, Denomination: Denomination.Diamonds or Denomination.Hearts })
                    return Explanation("strong-two-nt-major-texas", values);
                if (Profile.Conventions.MinorTransfers && bid == new Bid(3, Denomination.Spades)) return Explanation("strong-two-nt-club-texas", values);
                if (Profile.Conventions.MinorTransfers && bid == new Bid(4, Denomination.Clubs)) return Explanation("strong-two-nt-diamond-texas", values);
                if (Profile.Conventions.MajorTransfers && bid == new Bid(4, Denomination.Diamonds)) return Explanation("strong-two-nt-five-five", values);
                if (bid is { Level: 4, Denomination: Denomination.Hearts or Denomination.Spades })
                    return Explanation("strong-two-nt-major-game", values);
                if (bid == new Bid(3, Denomination.NoTrump)) return Explanation("strong-two-nt-natural-game", values);
                if (bid is { Level: 4 or 5, Denomination: Denomination.NoTrump })
                    return Explanation("strong-two-nt-quantitative", values);
            }
            if (opening.Seat == seat && ownBids.Count > 0)
            {
                if (Profile.Conventions.Stayman && partnerLast == new Bid(3, Denomination.Clubs))
                    return Explanation("strong-two-nt-stayman-answer", values);
                if (Profile.Conventions.MajorTransfers && partnerLast == new Bid(3, Denomination.Diamonds))
                    return Explanation(hand.SuitLength(Suit.Hearts) >= 3
                        ? "strong-two-nt-texas-fit" : "strong-two-nt-texas-no-fit", values);
                if (Profile.Conventions.MajorTransfers && partnerLast == new Bid(3, Denomination.Hearts))
                    return Explanation(hand.SuitLength(Suit.Spades) >= 3
                        ? "strong-two-nt-texas-fit" : "strong-two-nt-texas-no-fit", values);
                if (Profile.Conventions.MinorTransfers && partnerLast == new Bid(3, Denomination.Spades))
                    return Explanation("strong-two-nt-club-texas-rectification", values);
                if (Profile.Conventions.MinorTransfers && partnerLast == new Bid(4, Denomination.Clubs))
                    return Explanation("strong-two-nt-diamond-texas-rectification", values);
                if (Profile.Conventions.MajorTransfers && partnerLast == new Bid(4, Denomination.Diamonds))
                    return Explanation("strong-two-nt-five-five-choice", values);
            }
        }

        return null;
    }

    private static BidExplanation Explanation(string id, Dictionary<string, string> values) =>
        new(id, values);

    private static int TooltipHld(HandEvaluation hand, Seat seat, Bid bid,
        IReadOnlyList<AuctionCall> previousCalls)
    {
        if (bid.Denomination == Denomination.NoTrump) return hand.HonorLengthPoints;
        var denomination = bid.Denomination;
        var partnerBid = previousCalls.FirstOrDefault(call => call.Seat == seat.Partner() &&
            call.Action == AuctionAction.Bid && call.Bid?.Denomination == denomination).Bid;
        if (partnerBid is null) return hand.HonorLengthPoints;

        var ownSuitBid = previousCalls.FirstOrDefault(call => call.Seat == seat &&
            call.Action == AuctionAction.Bid && call.Bid?.Denomination == denomination).Bid;
        var partnerLength = ownSuitBid.HasValue
            ? partnerBid.Value.Level switch
            {
                2 when ownSuitBid.Value.Level == 1 => 3,
                3 when ownSuitBid.Value.Level == 1 => 4,
                4 when ownSuitBid.Value.Level == 1 => 5,
                2 => 3,
                3 => 3,
                4 => 4,
                _ => 3
            }
            : partnerBid.Value.Level switch
            {
                1 when denomination is Denomination.Hearts or Denomination.Spades => 5,
                1 => 3,
                2 when denomination is Denomination.Hearts or Denomination.Spades => 6,
                2 => 5,
                3 => 7,
                4 => 8,
                _ => 3
            };
        return FitPoints(hand, (Suit)denomination, partnerLength);
    }

    private static bool HasAgreedTrump(IReadOnlyList<AuctionCall> calls, Seat seat) =>
        calls.Where(call => call.Action == AuctionAction.Bid && call.Seat.IsNorthSouth() == seat.IsNorthSouth())
            .Where(call => call.Bid is { Denomination: not Denomination.NoTrump })
            .GroupBy(call => call.Bid!.Value.Denomination).Any(group => group.Count() >= 2);

    private static string SuitName(Denomination denomination) => denomination switch
    {
        Denomination.Spades => "Piques",
        Denomination.Hearts => "Cœurs",
        Denomination.Diamonds => "Carreaux",
        Denomination.Clubs => "Trèfles",
        _ => "Sans-Atout"
    };

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
        LastConfiguredRuleId = null;
        LastForcing = RuleForcing.NonForcing;
        if (TryConfiguredCall(seat, hand, auction, out var configured, out var configuredRule))
        {
            LastConfiguredRuleId = configuredRule!.Id;
            LastForcing = configuredRule.Response.Forcing!.Value;
            LastDecisionRule = $"{configuredRule.Id} : {configuredRule.Description}";
            var actual = EnforceForcing(seat, hand, auction, configured);
            _logger.Information($"[{Profile.Id}] {seat}: {CallText(actual)} - {LastDecisionRule}");
            return actual;
        }
        if (!Profile.UseStandardFallback)
        {
            return EvaluateAndChooseBackup(seat, hand, auction, "aucune règle configurée applicable");
        }
        if (auction.HighestBid is null)
            call = BidCall(seat, Opening(hand, auction));
        else if (auction.Calls.Any(call => SameSide(call.Seat, seat) && call.Action == AuctionAction.Double))
            call = RespondToTakeoutDouble(seat, hand, auction);
        else if (sideBids.Count == 0 && TryGetPartnerMinorOpening(seat, auction, out var partnerOpening))
            call = RespondToMinorOpeningIntervention(seat, hand, partnerOpening, auction);
        else if (sideBids.Count == 2 && sideBids[0].Seat == seat &&
             sideBids[0].Bid is { Level: 1, Denomination: Denomination.Clubs or Denomination.Diamonds } &&
                 TryGetPartnerResponseAfterIntervention(seat, auction, out var partnerResponse))
            call = RebidAfterMinorOpeningIntervention(seat, hand, sideBids[0].Bid!.Value, partnerResponse, auction);
        else if (sideBids.Count == 0)
            call = Intervention(seat, hand, auction);
        else
            call = ContinueAuction(seat, hand, auction, sideBids);

        var proposedCall = call;
        call = EnforceForcing(seat, hand, auction, call);
        var forcingFallback = call != proposedCall;
        if (call.Action == AuctionAction.Bid &&
            (!call.Bid.HasValue || call.Bid.Value.Level > 7 || !auction.IsLegal(call.Bid.Value)))
        {
            LastDecisionRule = "Sécurité SEF : aucune enchère automatique au palier 7";
            call = CompetitiveFallback(seat, hand, auction);
        }
        else
        {
            if (!forcingFallback) LastDecisionRule = ExplainDecision(hand, auction, sideBids, call);
        }

        _logger.Information($"[SEF] {seat}: {CallText(call)} — {LastDecisionRule}");
        return call;
    }

    public AuctionCall EnforceCall(Seat seat, IReadOnlyList<Card> cards, Auction auction, AuctionCall call) =>
        EnforceForcing(seat, new HandEvaluation(cards), auction, AnnotateCall(seat, cards, auction.Calls, call));

    private static string ExplainDecision(HandEvaluation hand, Auction auction,
        IReadOnlyList<AuctionCall> sideBids, AuctionCall call)
    {
        if (auction.HighestBid is null)
            return call.Action == AuctionAction.Pass
                ? "Ouverture : main sous les seuils SEF"
                : "Ouverture naturelle selon force et distribution";
        if (call.Action == AuctionAction.Double)
            return "Contre : soutien des couleurs non nommées et seuil de points atteint";
        if (call.Action == AuctionAction.Redouble)
            return "Surcontre : reprise compétitive après le contre adverse";
        if (call.Action == AuctionAction.Pass)
            return "Passe : aucun contrat sûr ou aucune enchère forcing disponible";
        if (sideBids.Count == 0)
            return "Intervention : couleur longue ou Sans-Atout selon la force";
        if (sideBids.Count == 1)
            return auction.Calls.Any(existing => existing.Action == AuctionAction.Bid &&
                !SameSide(existing.Seat, call.Seat))
                ? "Réponse compétitive : soutien, couleur forcing ou contre Sputnik"
                : "Réponse au partenaire : couleur majeure, soutien ou Sans-Atout";
        if (sideBids.Count == 2 && sideBids[0].Seat == call.Seat)
            return "Redemande de l'ouvreur : soutien, répétition ou bicolore SEF";
        if (call.Bid is { } bid && bid == new Bid(4, Denomination.NoTrump))
            return "Blackwood : demande des clés après agrément de l'atout";
        if (hand.HighCardPoints >= 13)
            return "Continuation forcing : description de la force et recherche de manche";
        return "Continuation naturelle selon la séquence d'enchères";
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

    private AuctionCall ContinueAuction(Seat seat, HandEvaluation hand, Auction auction,
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

        var lastBid = auction.Calls.LastOrDefault(call => call.Action == AuctionAction.Bid).Bid;
        if (lastBid is { Level: 4, Denomination: not Denomination.NoTrump } &&
            sideBids.Count >= 2 &&
            sideBids[0].Bid is { Level: 1, Denomination: Denomination.Hearts or Denomination.Spades } &&
            lastBid.Value.Denomination != sideBids[0].Bid!.Value.Denomination &&
            !sideBids.Any(call => call.Bid == new Bid(4, Denomination.NoTrump)) &&
            auction.IsLegal(new Bid(4, Denomination.NoTrump)))
            return BidCall(seat, new Bid(4, Denomination.NoTrump));

        var opening = sideBids[0];
        if (opening.Seat != seat && sideBids.Count == 1)
        {
            if (opening.Bid is { Level: 2, Denomination: Denomination.Hearts or Denomination.Spades } or
                { Level: 3 or 4 } && auction.Calls.Any(call => call.Action == AuctionAction.Bid &&
                    !SameSide(call.Seat, seat)))
                return RespondToPreemptIntervention(seat, hand, opening.Bid!.Value, auction);
            if (opening.Bid == new Bid(1, Denomination.NoTrump) &&
                auction.Calls.Any(call => call.Action is AuctionAction.Bid or AuctionAction.Double &&
                    !SameSide(call.Seat, seat)))
                return RespondToOneNoTrumpIntervention(seat, hand, auction);
            if (opening.Bid is { Level: 1, Denomination: Denomination.Hearts or Denomination.Spades } &&
                auction.Calls.Any(call => call.Action is AuctionAction.Bid or AuctionAction.Double &&
                    !SameSide(call.Seat, seat)))
                return RespondToMajorOpeningIntervention(seat, hand, opening.Bid.Value, auction);
            if (opening.Bid is { Level: 1, Denomination: Denomination.Clubs or Denomination.Diamonds } &&
                auction.Calls.Any(call => call.Action == AuctionAction.Double && !SameSide(call.Seat, seat)))
                return RespondToMinorOpeningDouble(seat, hand, opening.Bid.Value, auction);
            if (opening.Bid is { Level: 1, Denomination: Denomination.Clubs or Denomination.Diamonds } &&
                auction.Calls.Any(call => call.Action == AuctionAction.Bid && !SameSide(call.Seat, seat)))
                return RespondToMinorOpeningIntervention(seat, hand, opening.Bid.Value, auction);
            if (opening.Bid is { Level: 2, Denomination: Denomination.Clubs or Denomination.Diamonds } &&
                auction.Calls.Any(call => !SameSide(call.Seat, seat) &&
                    call.Action is AuctionAction.Bid or AuctionAction.Double))
                return RespondToStrongOpeningIntervention(seat, hand, opening.Bid.Value, auction);
            if (auction.Calls.Any(call => call.Action == AuctionAction.Bid &&
                !SameSide(call.Seat, seat)))
                return BidCall(seat, RespondToIntervention(hand, opening.Bid!.Value, auction));
            return BidCall(seat, FirstResponse(hand, opening.Bid!.Value, auction));
        }
        if (opening.Seat == seat && sideBids.Count == 2 && IsNaturalOpeningSequence(auction, opening))
        {
            if (opening.Bid == new Bid(2, Denomination.Clubs))
            {
                var intervention = auction.Calls.LastOrDefault(call => !SameSide(call.Seat, seat) &&
                    call.Action is AuctionAction.Bid or AuctionAction.Double);
                if (intervention.Action is AuctionAction.Bid or AuctionAction.Double)
                    return RespondToStrongTwoClubsFourthSeat(seat, hand, intervention, auction);
            }
            if (opening.Bid == new Bid(1, Denomination.NoTrump))
            {
                var rubensohlIntervention = auction.Calls.LastOrDefault(call =>
                    call.Action == AuctionAction.Bid && !SameSide(call.Seat, seat) &&
                    call.Bid is { Level: 2, Denomination: not Denomination.NoTrump });
                if (rubensohlIntervention.Action == AuctionAction.Bid)
                {
                    var response = sideBids[1].Bid!.Value;
                    return BidCall(seat, rubensohlIntervention.Bid == new Bid(2, Denomination.Clubs)
                        ? RespondToLandyRubensohl(hand, response, rubensohlIntervention.Bid!.Value)
                        : RespondToRubensohl(hand, response, rubensohlIntervention.Bid!.Value));
                }
            }
            if (opening.Bid == new Bid(1, Denomination.NoTrump) &&
                auction.Calls.Any(call => call.Action == AuctionAction.Double && !SameSide(call.Seat, seat)))
                return RespondToOneNoTrumpConventionDouble(seat, hand, sideBids[1].Bid!.Value, auction);
            return BidCall(seat, OpenerRebid(hand, opening.Bid!.Value, sideBids[1].Bid!.Value,
                IsThirdOrFourthSeatOpening(auction, opening.Bid.Value)));
        }
        if (opening.Seat != seat && sideBids.Count == 3 && IsNaturalOpeningSequence(auction, opening))
            return BidCall(seat, ResponderRebid(hand, opening.Bid!.Value,
                sideBids[1].Bid!.Value, sideBids[2].Bid!.Value,
                IsThirdOrFourthSeatOpening(auction, opening.Bid.Value)));
        return AdvancedContinuation(seat, hand, auction, sideBids);
    }

    private static bool IsNaturalOpeningSequence(Auction auction, AuctionCall opening) =>
        auction.Calls.FirstOrDefault(call => call.Action == AuctionAction.Bid) == opening;

    private AuctionCall AdvancedContinuation(Seat seat, HandEvaluation hand, Auction auction,
        IReadOnlyList<AuctionCall> sideBids)
    {
        var bids = sideBids.Select(call => call.Bid!.Value).ToList();

        if (bids.Count is 4 or 5 && IsStrongNoTrumpRebid(bids))
        {
            var responderBid = bids[3];
            if (bids.Count == 5 && bids[4] == new Bid(3, Denomination.NoTrump) &&
                TexasTarget(responderBid, 2).HasValue)
                return new(seat, AuctionAction.Pass);
            if (responderBid == new Bid(3, Denomination.Clubs))
                return BidCall(seat, bids.Count == 4
                    ? StaymanAnswer(hand, 3)
                    : AfterStayman(hand, bids[4], 3, NoTrumpOpenerMinimum(bids[0]), useProfile: false));
            if (TexasTarget(responderBid, 2).HasValue)
                return BidCall(seat, bids.Count == 4
                    ? TwoNoTrumpRebid(hand, responderBid, useProfile: false)
                    : new Bid(4, TexasTarget(responderBid, 2)!.Value));
        }

        var agreedTrump = FindAgreedTrump(bids);

        if (bids[^1] == new Bid(4, Denomination.NoTrump) && agreedTrump.HasValue)
            return BidCall(seat, BlackwoodResponse(hand, agreedTrump.Value));

        if (bids.Count >= 2 && bids[^2] == new Bid(4, Denomination.NoTrump) && agreedTrump.HasValue)
            return BidCall(seat, BlackwoodConclusion(hand, bids[^1], agreedTrump.Value));

        // Suites de l'ouverture de 1SA : deuxième redemande de l'ouvreur.
        if (bids.Count == 4 && bids[0] == new Bid(1, Denomination.NoTrump))
        {
            if (Profile.Conventions.Stayman && bids[1] == new Bid(2, Denomination.Clubs))
                return BidCall(seat, StaymanContinuation(hand, bids));
            // Bicolore 5-5 : après Texas ♠ rectifié, l'annonce des cœurs demande un choix.
            if (Profile.Conventions.MajorTransfers && bids[1] == new Bid(2, Denomination.Hearts) && bids[2] == new Bid(2, Denomination.Spades) &&
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

    private Denomination? FindAgreedTrump(IReadOnlyList<Bid> bids)
    {
        if (MinorTexasTrump(bids) is { } transferred && MinorTexasShortMajor(bids).HasValue &&
            bids.Count >= 5 && bids[4] is { Level: >= 4, Denomination: not Denomination.NoTrump })
            return transferred;
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

    private bool IsArtificialBid(IReadOnlyList<Bid> bids, int index)
    {
        var bid = bids[index];
        if (index == 0 && bid.Level == 2 && bid.Denomination is Denomination.Clubs or Denomination.Diamonds)
            return true;
        if (index == 1 && bids[0] == new Bid(2, Denomination.Clubs) &&
            bid == new Bid(2, Denomination.Diamonds))
            return true;
        if (index == 3 && IsStrongNoTrumpStayman(bids.Take(4).ToList()))
            return true;
        if (index == 1 && bids[0] is { Level: 1 or 2, Denomination: Denomination.NoTrump } && !Profile.IsConventionEnabled("stayman"))
            return false;
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
