namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

public sealed partial class SefBiddingEngine
{
    private readonly BiddingRule[] _configuredRules;

    public string? LastConfiguredRuleId { get; private set; }
    public RuleForcing LastForcing { get; private set; }
    public Vulnerability Vulnerability { get; set; }

    public AuctionCall AnnotateCall(Seat seat, IReadOnlyList<Card> cards,
        IReadOnlyList<AuctionCall> previousCalls, AuctionCall call)
    {
        var auction = new Auction();
        foreach (var previous in previousCalls) auction.AddCall(previous);
        if (!TryConfiguredCall(seat, new HandEvaluation(cards), auction, out _, out var rule, call)) return call;
        var forcing = rule!.Response.Forcing!.Value switch
        {
            RuleForcing.OneRound => AuctionForcing.OneRound,
            RuleForcing.SelfForcing => AuctionForcing.SelfForcing,
            RuleForcing.GameForcing => AuctionForcing.GameForcing,
            _ => AuctionForcing.None
        };
        return call with { Forcing = forcing };
    }

    private AuctionCall EnforceForcing(Seat seat, HandEvaluation hand, Auction auction, AuctionCall call)
    {
        var forcing = auction.ForcingFor(seat);
        if (forcing == AuctionForcing.None || call.Action == AuctionAction.Bid) return call;

        var prioritySuits = auction.Calls
            .Where(previous => SameSide(previous.Seat, seat) && previous.Bid is { Denomination: not Denomination.NoTrump })
            .Select(previous => previous.Bid!.Value.Denomination)
            .Where(denomination => hand.SuitLength((Suit)denomination) >= 3)
            .Distinct()
            .OrderByDescending(denomination => hand.SuitLength((Suit)denomination))
            .ThenByDescending(denomination => denomination)
            .ToList();
        prioritySuits.AddRange(Enum.GetValues<Suit>()
            .Select(suit => (Denomination)suit)
            .Where(denomination => !prioritySuits.Contains(denomination))
            .OrderByDescending(denomination => hand.SuitLength((Suit)denomination))
            .ThenByDescending(denomination => denomination));
        var denominations = hand.IsBalanced ? prioritySuits.Prepend(Denomination.NoTrump) : prioritySuits;

        foreach (var denomination in denominations)
        {
            var gameLevel = denomination == Denomination.NoTrump ? 3 :
                denomination is Denomination.Hearts or Denomination.Spades ? 4 : 5;
            var level = forcing == AuctionForcing.GameForcing
                ? Math.Max(gameLevel, auction.HighestBid?.Level ?? gameLevel)
                : Math.Max(1, auction.HighestBid?.Level ?? 1);
            while (level <= 7)
            {
                var bid = new Bid(level, denomination);
                if (auction.IsLegal(bid) && (forcing != AuctionForcing.GameForcing || level >= gameLevel))
                {
                    LastDecisionRule = $"Continuation de sécurité : respect de l'obligation {forcing}";
                    LastForcing = forcing switch
                    {
                        AuctionForcing.OneRound => RuleForcing.OneRound,
                        AuctionForcing.SelfForcing => RuleForcing.SelfForcing,
                        AuctionForcing.GameForcing => RuleForcing.GameForcing,
                        _ => RuleForcing.NonForcing
                    };
                    return new AuctionCall(seat, AuctionAction.Bid, bid);
                }
                level++;
            }
        }
        return call;
    }

    private bool TryConfiguredCall(Seat seat, HandEvaluation hand, Auction auction,
        out AuctionCall result, out BiddingRule? appliedRule, AuctionCall? requestedCall = null,
        bool describeMeaning = false)
    {
        var ownSide = auction.Calls.Where(call => SameSide(call.Seat, seat) && !call.IsPass).ToArray();
        var opponents = auction.Calls.Where(call => !SameSide(call.Seat, seat) && !call.IsPass).ToArray();
        var sideBids = ownSide.Where(call => call.Bid.HasValue).ToArray();
        var stage = ConfiguredStage(seat, auction, sideBids);
        foreach (var rule in _configuredRules)
        {
            if (!rule.Enabled || rule.Stage != BiddingStage.Any && rule.Stage != stage ||
                rule.OpponentsHaveActed is { } acted && acted != (opponents.Length > 0) ||
                !MatchesSequence(rule.SideSequence, ownSide, rule.AllowAdditionalSideBids) ||
                rule.OpponentSequence.Count > 0 && !MatchesSequence(rule.OpponentSequence, opponents, false) ||
                rule.AuctionSequence.Count > 0 && !MatchesSequence(rule.AuctionSequence,
                    auction.Calls.Where(call => !rule.IgnorePasses || !call.IsPass).ToArray(), false) ||
                rule.LastSideCall is { } lastSide && (ownSide.Length == 0 || !MatchesSequence([lastSide], ownSide.TakeLast(1).ToArray(), false)) ||
                rule.LastOpponentCall is { } lastOpponent && (opponents.Length == 0 || !MatchesSequence([lastOpponent], opponents.TakeLast(1).ToArray(), false)) ||
                rule.LastOpponentAction is { } action && (opponents.Length == 0 || opponents[^1].Action != action switch
                {
                    RuleCallAction.Bid => AuctionAction.Bid, RuleCallAction.Pass => AuctionAction.Pass,
                    RuleCallAction.Double => AuctionAction.Double, _ => AuctionAction.Redouble
                }) ||
                rule.Convention is { } convention && !Profile.IsConventionEnabled(convention) ||
                rule.RequiresConventions.Any(required => !Profile.IsConventionEnabled(required)) ||
                rule.DisabledConventions.Any(Profile.IsConventionEnabled)) continue;
            IEnumerable<Denomination?> candidates = rule.CandidateSuits.Count == 0
                ? new Denomination?[] { null }
                : rule.SuitOrder switch
                {
                    RuleSuitOrder.Lowest => rule.CandidateSuits.OrderBy(suit => suit).Select(suit => (Denomination?)(Denomination)suit),
                    RuleSuitOrder.Highest => rule.CandidateSuits.OrderByDescending(suit => suit).Select(suit => (Denomination?)(Denomination)suit),
                    RuleSuitOrder.LongestThenLowest => rule.CandidateSuits.OrderByDescending(suit => hand.SuitLength((Suit)suit))
                        .ThenBy(suit => suit).Select(suit => (Denomination?)(Denomination)suit),
                    _ => rule.CandidateSuits.OrderByDescending(suit => hand.SuitLength((Suit)suit))
                        .ThenByDescending(suit => suit).Select(suit => (Denomination?)(Denomination)suit)
                };
            foreach (var candidateSuit in candidates)
            {
                bool Satisfies(HandCondition condition)
                {
                    if (describeMeaning && !IsAuctionCondition(condition.Fact)) return true;
                    var value = RuleFact(condition.Fact, seat, hand, auction, sideBids, opponents, candidateSuit);
                    var lower = condition.MinimumFact is { } minimumFact
                        ? RuleFact(minimumFact, seat, hand, auction, sideBids, opponents, candidateSuit) : condition.Minimum;
                    var upper = condition.MaximumFact is { } maximumFact
                        ? RuleFact(maximumFact, seat, hand, auction, sideBids, opponents, candidateSuit) : condition.Maximum;
                    if (condition.MinimumFact is not null && lower is null || condition.MaximumFact is not null && upper is null) return false;
                    return value.HasValue && (!lower.HasValue || value >= lower) && (!upper.HasValue || value <= upper);
                }
                if (!rule.Conditions.All(Satisfies) || rule.AnyOf.Count > 0 && !rule.AnyOf.Any(group => group.All(Satisfies))) continue;
                var candidate = RuleCall(rule.Response, seat, hand, auction, candidateSuit);
                if (candidate is not { } legal) continue;
                if (requestedCall is { } requested &&
                    (legal.Seat != requested.Seat || legal.Action != requested.Action || legal.Bid != requested.Bid)) continue;
                result = legal;
                appliedRule = rule;
                return true;
            }
        }
        result = default;
        appliedRule = null;
        return false;
    }

    private static bool IsAuctionCondition(string fact) => fact is
        "passedHand" or "passedOpening" or "ownBids" or "partnerBids" or "opponentBids" or
        "lastOpponentLevel" or "sideBids" or "opponentDoubled" or "opponentRedoubled" or
        "candidateIsOpening" or "candidateIsOpponent" or "candidateIsMajor" or "candidateIsResponse" or
        "candidateIsPartner" or "openingLevel" or "openingIsMajor" or "openingIsMinor" or
        "firstResponseLevel" or "firstResponseIsOpening" or "lastSideIsOpening" or "firstThreeDistinct" or
        "partnerAskedBlackwood" or "opponentsShowTwoSuits" or "partnerIsMajor" or "openerRebidIsMajor" or
        "openerRebidLevel" or "seatPosition" or "gameReached" or "candidateCheapestLevel" or
        "repeatCheapestLevel" or "candidateReverse" or "candidateEconomic" or "repeatNeedsSix" or
        "forcedRebid" or "freeRebid" or "partnerSputnik" or "opponentsRaised" or "trialSpace" or
        "partnerResponseIsSuit" or "openerFakeJump" or "naturalResponse" or "fourthSeatActed" or
        "minorTexas" or "minorTexasShortness" or "minorTexasAgreed" or "candidateIsTransferredMinor" or
        "candidateIsShortMajor" or "candidateIsShownControl" or "minorTexasNewControl" or "minorTexasBlackwoodAnswered";

    private static BiddingStage ConfiguredStage(Seat seat, Auction auction, IReadOnlyList<AuctionCall> sideBids)
    {
        if (auction.HighestBid is null) return BiddingStage.Opening;
        if (sideBids.Count == 0) return BiddingStage.Intervention;
        if (sideBids.Count == 1 && sideBids[0].Seat != seat) return BiddingStage.Response;
        if (sideBids.Count == 2 && sideBids[0].Seat == seat) return BiddingStage.OpenerRebid;
        if (sideBids.Count == 1 && sideBids[0].Seat == seat &&
            auction.Calls.Any(call => call.Seat == seat.Partner() && call.Action == AuctionAction.Double))
            return BiddingStage.OpenerRebid;
        if (sideBids.Count == 3 && sideBids[0].Seat != seat) return BiddingStage.ResponderRebid;
        return BiddingStage.Continuation;
    }

    private static bool MatchesSequence(IReadOnlyList<string> patterns, IReadOnlyList<AuctionCall> calls, bool prefix)
    {
        if (patterns.Count == 0) return true;
        if (prefix ? calls.Count < patterns.Count : calls.Count != patterns.Count) return false;
        for (var index = 0; index < patterns.Count; index++)
        {
            var pattern = patterns[index];
            var call = calls[index];
            if (pattern == "*") continue;
            if (call.Action != AuctionAction.Bid)
            {
                if (pattern != (call.IsPass ? "P" : call.Action == AuctionAction.Double ? "X" : "XX")) return false;
                continue;
            }
            if (call.Bid is not { } bid || pattern[0] != '0' + bid.Level) return false;
            var suit = pattern[1..];
            if (suit == "x" && bid.Denomination != Denomination.NoTrump ||
                suit == "M" && bid.Denomination is Denomination.Hearts or Denomination.Spades ||
                suit == "m" && bid.Denomination is Denomination.Clubs or Denomination.Diamonds) continue;
            var expected = bid.Denomination switch
            {
                Denomination.Clubs => "C", Denomination.Diamonds => "D", Denomination.Hearts => "H",
                Denomination.Spades => "S", _ => "NT"
            };
            if (suit != expected) return false;
        }
        return true;
    }

    private AuctionCall? RuleCall(RuleResponse response, Seat seat, HandEvaluation hand, Auction auction, Denomination? candidateSuit)
    {
        var forcing = response.Forcing!.Value switch
        {
            RuleForcing.OneRound => AuctionForcing.OneRound,
            RuleForcing.SelfForcing => AuctionForcing.SelfForcing,
            RuleForcing.GameForcing => AuctionForcing.GameForcing,
            _ => AuctionForcing.None
        };
        if (response.Action == RuleCallAction.Pass) return new(seat, AuctionAction.Pass, Forcing: forcing);
        if (response.Action == RuleCallAction.Double)
            return auction.CanDouble(seat) ? new(seat, AuctionAction.Double, Forcing: forcing) : null;
        if (response.Action == RuleCallAction.Redouble)
            return auction.CanRedouble(seat) ? new(seat, AuctionAction.Redouble, Forcing: forcing) : null;
        var denomination = ResolveRuleDenomination(response.Denomination, seat, hand, auction, candidateSuit);
        if (denomination is null) return null;
        var cheapest = auction.HighestBid is { } highest ? CheapestBid(denomination.Value, highest).Level : 1;
        var level = response.LevelMode switch
        {
            RuleBidLevel.Cheapest => cheapest,
            RuleBidLevel.Jump => cheapest + 1,
            RuleBidLevel.DoubleJump => cheapest + 2,
            RuleBidLevel.Support => Math.Max(2, cheapest),
            RuleBidLevel.SupportJump => Math.Max(2, cheapest) + 1,
            RuleBidLevel.Game => denomination == Denomination.NoTrump ? 3 : denomination is Denomination.Hearts or Denomination.Spades ? 4 : 5,
            RuleBidLevel.SmallSlam => 6,
            RuleBidLevel.GrandSlam => 7,
            _ => response.Level
        };
        var bid = new Bid(level, denomination.Value);
        return level >= response.MinimumLevel && level <= response.MaximumLevel && auction.IsLegal(bid)
            ? new(seat, AuctionAction.Bid, bid, forcing) : null;
    }

    private Denomination? ResolveRuleDenomination(RuleDenomination denomination, Seat seat,
        HandEvaluation hand, Auction auction, Denomination? candidateSuit = null)
    {
        var side = auction.Calls.Where(call => SameSide(call.Seat, seat) && call.Bid.HasValue).ToArray();
        return denomination switch
        {
            <= RuleDenomination.NoTrump => (Denomination)denomination,
            RuleDenomination.Candidate => candidateSuit,
            RuleDenomination.TransferredMinor => MinorTexasTrump(side.Select(call => call.Bid!.Value).ToArray()),
            RuleDenomination.ShortMajor => MinorTexasShortMajor(side.Select(call => call.Bid!.Value).ToArray()),
            RuleDenomination.Trump => FindAgreedTrump(side.Select(call => call.Bid!.Value).ToArray()) ??
                (side.FirstOrDefault().Bid?.Denomination is { } openingSuit && openingSuit != Denomination.NoTrump ? openingSuit : null),
            RuleDenomination.FittedMajor => side.LastOrDefault().Bid?.Denomination switch
            {
                Denomination.Hearts when hand.SuitLength(Suit.Hearts) >= 4 => Denomination.Hearts,
                Denomination.Spades when hand.SuitLength(Suit.Spades) >= 4 => Denomination.Spades,
                Denomination.NoTrump when hand.SuitLength(Suit.Hearts) >= 4 => Denomination.Hearts,
                Denomination.NoTrump when hand.SuitLength(Suit.Spades) >= 4 => Denomination.Spades,
                _ => null
            },
            RuleDenomination.CheapCueBid => side.FirstOrDefault().Bid?.Denomination is Denomination.Clubs or Denomination.Diamonds
                ? Denomination.Hearts : Denomination.Clubs,
            RuleDenomination.ExpensiveCueBid => side.FirstOrDefault().Bid?.Denomination is Denomination.Clubs or Denomination.Diamonds
                ? Denomination.Spades : Denomination.Diamonds,
            RuleDenomination.Opening => side.FirstOrDefault().Bid?.Denomination,
            RuleDenomination.FirstResponse => side.ElementAtOrDefault(1).Bid?.Denomination ??
                (auction.Calls.Any(call => call.Seat == seat.Partner() && call.Action == AuctionAction.Double)
                    ? ResolveSputnikMajor(auction, seat) : null),
            RuleDenomination.OtherUnbid => Enum.GetValues<Denomination>()
                .Where(suit => suit != Denomination.NoTrump && suit != candidateSuit &&
                    !side.Take(2).Any(call => call.Bid?.Denomination == suit))
                .Select(suit => (Denomination?)suit).FirstOrDefault(),
            RuleDenomination.OpenerRebid => side.ElementAtOrDefault(2).Bid?.Denomination,
            RuleDenomination.FourthSuit => Enum.GetValues<Denomination>().Where(suit => suit != Denomination.NoTrump &&
                !side.Take(3).Any(call => call.Bid?.Denomination == suit)).Select(suit => (Denomination?)suit).FirstOrDefault(),
            RuleDenomination.OtherMinor => side.FirstOrDefault().Bid?.Denomination == Denomination.Clubs ? Denomination.Diamonds : Denomination.Clubs,
            RuleDenomination.Partner => auction.Calls.LastOrDefault(call => call.Seat == seat.Partner() && call.Bid.HasValue).Bid?.Denomination,
            RuleDenomination.Opponent => auction.Calls.LastOrDefault(call => !SameSide(call.Seat, seat) && call.Bid.HasValue).Bid?.Denomination,
            RuleDenomination.Longest => ToDenomination(hand.LongestSuit),
            RuleDenomination.LongestMajor => ToDenomination(LongerMajor(hand)),
            RuleDenomination.LongestMinor => hand.SuitLength(Suit.Diamonds) >= hand.SuitLength(Suit.Clubs) ? Denomination.Diamonds : Denomination.Clubs,
            RuleDenomination.OtherMajor => side.Select(call => call.Bid!.Value.Denomination)
                .FirstOrDefault(suit => suit is Denomination.Hearts or Denomination.Spades, Denomination.NoTrump) switch
            {
                Denomination.Hearts => Denomination.Spades,
                Denomination.Spades => Denomination.Hearts,
                _ => candidateSuit is Denomination.Hearts ? Denomination.Spades :
                    candidateSuit is Denomination.Spades ? Denomination.Hearts :
                    ToDenomination(LongerMajor(hand) == Suit.Hearts ? Suit.Spades : Suit.Hearts)
            },
            RuleDenomination.Shortest => ToDenomination(Enum.GetValues<Suit>().OrderBy(hand.SuitLength).First()),
            _ => null
        };
    }

    private int? RuleFact(string fact, Seat seat, HandEvaluation hand, Auction auction,
        IReadOnlyList<AuctionCall> sideBids, IReadOnlyList<AuctionCall> opponents, Denomination? candidateSuit)
    {
        var opening = sideBids.FirstOrDefault().Bid;
        var trump = FindAgreedTrump(sideBids.Select(call => call.Bid!.Value).ToArray()) ??
            (opening?.Denomination != Denomination.NoTrump ? opening?.Denomination : null);
        var lengths = Enum.GetValues<Suit>().Select(hand.SuitLength).ToArray();
        var evaluation = Profile.Evaluation;
        var highCards = hand.Cards.Sum(card => card.Rank switch
        {
            Rank.Ace => evaluation.Ace, Rank.King => evaluation.King, Rank.Queen => evaluation.Queen,
            Rank.Jack => evaluation.Jack, _ => 0
        });
        var honorLength = highCards + lengths.Sum(length => Math.Max(0, length - evaluation.LengthStartsAt + 1) * evaluation.LengthPoint);
        var distribution = lengths.Sum(length => length switch
        {
            0 => evaluation.Void, 1 => evaluation.Singleton, 2 => evaluation.Doubleton, _ => 0
        });
        var agreedTrump = FindAgreedTrump(sideBids.Select(call => call.Bid!.Value).ToArray());
        var fitSuit = agreedTrump ?? (opening is { Level: 1, Denomination: not Denomination.NoTrump }
            ? opening.Value.Denomination : null);
        var partnerTrumpLength = fitSuit.HasValue ? PartnerTrumpLength(seat, fitSuit.Value, auction) : 0;
        var fitDiscovered = fitSuit.HasValue &&
            hand.SuitLength((Suit)fitSuit.Value) + partnerTrumpLength >= Profile.Backup.MinimumAgreedFit;
        var distributionAfterFit = fitDiscovered ? distribution : 0;
        if (fact.StartsWith("shape.", StringComparison.Ordinal))
            return lengths.OrderByDescending(length => length).SequenceEqual(fact[6..].Select(character => character - '0').OrderByDescending(length => length)) ? 1 : 0;
        if (fact == "length.otherMajorThanCandidate")
            return candidateSuit is Denomination.Hearts ? hand.SuitLength(Suit.Spades) :
                candidateSuit is Denomination.Spades ? hand.SuitLength(Suit.Hearts) : null;
        if (fact.Contains('.'))
        {
            var parts = fact.Split('.');
            var selector = parts[1] switch
            {
                "clubs" => RuleDenomination.Clubs, "diamonds" => RuleDenomination.Diamonds,
                "hearts" => RuleDenomination.Hearts, "spades" => RuleDenomination.Spades,
                "opening" => RuleDenomination.Opening, "partner" => RuleDenomination.Partner,
                "opponent" => RuleDenomination.Opponent, "longest" => RuleDenomination.Longest,
                "longestMajor" => RuleDenomination.LongestMajor, "longestMinor" => RuleDenomination.LongestMinor,
                "otherMajor" => RuleDenomination.OtherMajor, "candidate" => RuleDenomination.Candidate,
                "firstResponse" => RuleDenomination.FirstResponse, "openerRebid" => RuleDenomination.OpenerRebid,
                "fourthSuit" => RuleDenomination.FourthSuit, "otherMinor" => RuleDenomination.OtherMinor,
                "trump" => RuleDenomination.Trump,
                "otherUnbid" => RuleDenomination.OtherUnbid,
                "transferredMinor" => RuleDenomination.TransferredMinor, "shortMajor" => RuleDenomination.ShortMajor,
                _ => RuleDenomination.Shortest
            };
            var denomination = ResolveRuleDenomination(selector, seat, hand, auction, candidateSuit);
            if (denomination is null or Denomination.NoTrump) return null;
            var suit = (Suit)denomination.Value;
            return parts[0] switch
            {
                "length" => hand.SuitLength(suit), "honors" => SuitHonors(hand, suit),
                "bigHonors" => hand.Cards.Count(card => card.Suit == suit && card.Rank >= Rank.King),
                "sideBigHonors" => SideBigHonors(hand, suit), "sideQueens" => SideQueens(hand, suit),
                "topHonors" => HasTopHonors(hand, suit) ? 1 : 0, "stopper" => HasGuard(hand, suit) ? 1 : 0,
                "ace" => hand.Cards.Contains(new Card(suit, Rank.Ace)) ? 1 : 0,
                "king" => hand.Cards.Contains(new Card(suit, Rank.King)) ? 1 : 0,
                "queen" => hand.Cards.Contains(new Card(suit, Rank.Queen)) ? 1 : 0,
                "force" => hand.Cards.Contains(new Card(suit, Rank.Ace)) ||
                    hand.SuitLength(suit) >= 2 && hand.Cards.Contains(new Card(suit, Rank.King)) ||
                    hand.SuitLength(suit) >= 3 && hand.Cards.Contains(new Card(suit, Rank.Queen)) ? 1 : 0,
                "control" => hand.SuitLength(suit) <= 1 || hand.Cards.Any(card => card.Suit == suit && card.Rank is Rank.Ace or Rank.King) ? 1 : 0,
                _ => null
            };
        }
        return fact switch
        {
            "h" => highCards, "hl" => honorLength, "hld" => honorLength + distributionAfterFit,
            "fitPoints" => trump.HasValue ? honorLength + distributionAfterFit +
                (fitDiscovered ? RuleTrumpBonus(hand, trump.Value, opening) : 0) : null,
            "aces" => hand.AceCount,
            "keys" => trump.HasValue ? hand.AceCount + (hand.Cards.Contains(new Card((Suit)trump.Value, Rank.King)) ? 1 : 0) : null,
            "trumpQueen" => trump.HasValue ? hand.Cards.Contains(new Card((Suit)trump.Value, Rank.Queen)) ? 1 : 0 : null,
            "balanced" => hand.IsBalanced ? 1 : 0,
            "regular" => lengths.All(length => length is >= 2 and <= 5) && lengths.Count(length => length == 2) <= 1 ? 1 : 0,
            "singletons" => lengths.Count(length => length == 1), "voids" => lengths.Count(length => length == 0),
            "doubletons" => lengths.Count(length => length == 2),
            "majorFours" => new[] { Suit.Hearts, Suit.Spades }.Count(suit => hand.SuitLength(suit) >= 4),
            "majorFit" => ResolveRuleDenomination(RuleDenomination.FittedMajor, seat, hand, auction).HasValue ? 1 : 0,
            "singleSuited" => lengths.Count(length => length >= 6) == 1 && lengths.Count(length => length >= 4) == 1 ? 1 : 0,
            "twoSuited" => lengths.Max() >= 5 && lengths.Count(length => length >= 4) == 2 ? 1 : 0,
            "sideFourCardSuits" => Enum.GetValues<Suit>().Count(suit => (Denomination)suit != candidateSuit && hand.SuitLength(suit) >= 4),
            "sideMajorFours" => new[] { Suit.Hearts, Suit.Spades }.Count(suit => (Denomination)suit != candidateSuit && hand.SuitLength(suit) >= 4),
            "ownKings" => hand.Cards.Count(card => card.Rank == Rank.King),
            "ownQueens" => hand.Cards.Count(card => card.Rank == Rank.Queen),
            "vulnerable" => Vulnerability.IsVulnerable(seat) ? 1 : 0,
            "opponentsVulnerable" => Vulnerability.IsVulnerable((Seat)(((int)seat + 1) % 4)) ? 1 : 0,
            "favorableVulnerability" => !Vulnerability.IsVulnerable(seat) &&
                Vulnerability.IsVulnerable((Seat)(((int)seat + 1) % 4)) ? 1 : 0,
            "seatPosition" => ((int)seat - (int)(auction.Calls.Count > 0 ? auction.Calls[0].Seat : seat) + 4) % 4 + 1,
            "shortSuitsHaveHonor" => Enum.GetValues<Suit>().Where(suit => hand.SuitLength(suit) == 2)
                .All(suit => SuitHonors(hand, suit) >= 1) ? 1 : 0,
            "losers" => LosingTricks(hand), "playingTricks" => 13 - LosingTricks(hand),
            "playingTricksHalf" => PlayingTricksHalf(hand),
            "defensiveTricks" => Enum.GetValues<Suit>().Where(suit => suit != hand.LongestSuit).Sum(suit =>
                (hand.Cards.Contains(new Card(suit, Rank.Ace)) ? 1 : 0) +
                (hand.SuitLength(suit) >= 2 && hand.Cards.Contains(new Card(suit, Rank.King)) ? 1 : 0)),
            "passedHand" => auction.Calls.TakeWhile(call => call.Seat != seat || call.Action != AuctionAction.Bid)
                .Any(call => call.Seat == seat && call.IsPass) ? 1 : 0,
            "passedOpening" => auction.Calls.TakeWhile(call => call.Action != AuctionAction.Bid).Count() >= 2 ? 1 : 0,
            "ownBids" => sideBids.Count(call => call.Seat == seat),
            "partnerBids" => sideBids.Count(call => call.Seat == seat.Partner()),
            "opponentBids" => opponents.Count(call => call.Bid.HasValue),
            "lastOpponentLevel" => opponents.LastOrDefault(call => call.Bid.HasValue).Bid?.Level,
            "sideBids" => sideBids.Count,
            "opponentDoubled" => opponents.Any(call => call.Action == AuctionAction.Double) ? 1 : 0,
            "opponentRedoubled" => opponents.Any(call => call.Action == AuctionAction.Redouble) ? 1 : 0,
            "candidateIsOpening" => candidateSuit.HasValue ? candidateSuit == opening?.Denomination ? 1 : 0 : null,
            "candidateIsOpponent" => candidateSuit.HasValue ? candidateSuit == opponents.LastOrDefault(call => call.Bid.HasValue).Bid?.Denomination ? 1 : 0 : null,
            "candidateIsMajor" => candidateSuit.HasValue ? candidateSuit is Denomination.Hearts or Denomination.Spades ? 1 : 0 : null,
            "candidateIsResponse" => candidateSuit.HasValue ? candidateSuit == ResolveRuleDenomination(RuleDenomination.FirstResponse, seat, hand, auction) ? 1 : 0 : null,
            "candidateIsPartner" => candidateSuit.HasValue ? candidateSuit == auction.Calls.LastOrDefault(call => call.Seat == seat.Partner() && call.Bid.HasValue).Bid?.Denomination ? 1 : 0 : null,
            "partnerFirstBidIsCandidate" => candidateSuit.HasValue ? candidateSuit == auction.Calls.FirstOrDefault(call => call.Seat == seat.Partner() && call.Bid.HasValue).Bid?.Denomination ? 1 : 0 : null,
            "partnerIsMajor" => auction.Calls.LastOrDefault(call => call.Seat == seat.Partner() && call.Bid.HasValue).Bid?.Denomination is Denomination.Hearts or Denomination.Spades ? 1 : 0,
            "openerRebidIsMajor" => sideBids.ElementAtOrDefault(2).Bid?.Denomination is Denomination.Hearts or Denomination.Spades ? 1 : 0,
            "openerRebidLevel" => sideBids.ElementAtOrDefault(2).Bid?.Level,
            "length.otherMajorThanCandidate" => candidateSuit is Denomination.Hearts ? hand.SuitLength(Suit.Spades) :
                candidateSuit is Denomination.Spades ? hand.SuitLength(Suit.Hearts) : null,
            "hasSideControl" => trump.HasValue ? Enum.GetValues<Suit>().Any(suit => (Denomination)suit != trump &&
                (hand.SuitLength(suit) <= 1 || hand.Cards.Any(card => card.Suit == suit && card.Rank is Rank.Ace or Rank.King))) ? 1 : 0 : null,
            "partnerAskedBlackwood" => auction.Calls.LastOrDefault(call => call.Seat == seat.Partner() && call.Bid.HasValue).Bid == new Bid(4, Denomination.NoTrump) ? 1 : 0,
            "opponentsShowTwoSuits" => opponents.LastOrDefault(call => call.Bid.HasValue).Bid is { } opponent &&
                (opponent.Denomination == opening?.Denomination || opponent is { Level: 2, Denomination: Denomination.NoTrump }) ? 1 : 0,
            "openingLevel" => opening?.Level,
            "openingIsMajor" => opening.HasValue ? opening.Value.Denomination is Denomination.Hearts or Denomination.Spades ? 1 : 0 : null,
            "openingIsMinor" => opening.HasValue ? opening.Value.Denomination is Denomination.Clubs or Denomination.Diamonds ? 1 : 0 : null,
            "firstResponseLevel" => sideBids.ElementAtOrDefault(1).Bid?.Level,
            "firstResponseIsOpening" => sideBids.Count > 1 ? sideBids[1].Bid?.Denomination == opening?.Denomination ? 1 : 0 : null,
            "lastSideIsOpening" => sideBids.Count > 0 ? sideBids[^1].Bid?.Denomination == opening?.Denomination ? 1 : 0 : null,
            "allUnbidStoppers" => Enum.GetValues<Suit>().Where(suit =>
                !sideBids.Any(call => call.Bid?.Denomination == (Denomination)suit)).All(suit => HasGuard(hand, suit)) ? 1 : 0,
            "agreedTrump" => FindAgreedTrump(sideBids.Select(call => call.Bid!.Value).ToArray()).HasValue ? 1 : 0,
            "gameReached" => sideBids.Any(call => call.Bid is { } bid && bid.Level >=
                (bid.Denomination == Denomination.NoTrump ? 3 : bid.Denomination is Denomination.Hearts or Denomination.Spades ? 4 : 5)) ? 1 : 0,
            "firstThreeDistinct" => sideBids.Take(3).Select(call => call.Bid!.Value.Denomination).Distinct().Count(),
            "sameColorAces" => hand.AceCount == 2 &&
                (hand.Cards.Where(card => card.Rank == Rank.Ace).All(card => card.Suit is Suit.Clubs or Suit.Spades) ||
                 hand.Cards.Where(card => card.Rank == Rank.Ace).All(card => card.Suit is Suit.Diamonds or Suit.Hearts)) ? 1 : 0,
            "sameRankAces" => hand.AceCount == 2 &&
                (hand.Cards.Where(card => card.Rank == Rank.Ace).All(card => card.Suit is Suit.Clubs or Suit.Diamonds) ||
                 hand.Cards.Where(card => card.Rank == Rank.Ace).All(card => card.Suit is Suit.Hearts or Suit.Spades)) ? 1 : 0,
            _ => MinorTexasFact(fact, seat, hand, sideBids, opponents, candidateSuit, honorLength, distribution) ??
                OpenerRebidFact(fact, seat, hand, auction, sideBids, candidateSuit, highCards, honorLength, distribution)
        };
    }

    private static int PlayingTricksHalf(HandEvaluation hand)
    {
        var aces = hand.Cards.Count(card => card.Rank == Rank.Ace);
        var tens = hand.Cards.Count(card => card.Rank == Rank.Ten);
        var nines = hand.Cards.Count(card => card.Rank == Rank.Nine);
        var extraHalfTrick = aces >= 3 || tens >= 3 || tens >= 2 && nines >= 3;
        return (13 - LosingTricks(hand)) * 2 + (extraHalfTrick ? 1 : 0);
    }

    private int RuleTrumpBonus(HandEvaluation hand, Denomination trump, Bid? opening)
    {
        var partnerLength = opening?.Level == 2 ? 6 : opening?.Level == 3 ? 7 : opening?.Level == 4 ? 8 :
            trump is Denomination.Hearts or Denomination.Spades ? 5 : 3;
        var combined = hand.SuitLength((Suit)trump) + partnerLength;
        return combined >= 9 ? Profile.Evaluation.NinthTrump + Math.Max(0, combined - 9) * Profile.Evaluation.ExtraTrump : 0;
    }
}