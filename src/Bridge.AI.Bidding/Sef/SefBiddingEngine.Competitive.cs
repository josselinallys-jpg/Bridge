namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

/// <summary>Chapitre SEF : interventions et réveils.</summary>
public sealed partial class SefBiddingEngine
{
    private static AuctionCall RebidAfterMinorOpeningIntervention(
        Seat seat, HandEvaluation hand, Bid opening, Bid response, Auction auction)
    {
        var intervention = auction.Calls.Last(call =>
            call.Action == AuctionAction.Bid && !SameSide(call.Seat, seat)).Bid!.Value;
        var stickySuit = intervention.Denomination switch
        {
            Denomination.Diamonds => Denomination.Hearts,
            Denomination.Hearts => Denomination.Spades,
            _ => default
        };

        if (response.Denomination == stickySuit && stickySuit is Denomination.Hearts or Denomination.Spades)
        {
            var support = hand.SuitLength((Suit)response.Denomination);
            if (support >= 3)
            {
                var fitPoints = FitPoints(hand, (Suit)response.Denomination, 5);
                var level = fitPoints >= 20
                    ? 4
                    : fitPoints >= 17 && !hand.IsBalanced
                        ? 3
                        : 2;
                return BidCall(seat, new Bid(level, response.Denomination));
            }
        }

        return BidCall(seat, OpenerRebid(hand, opening, response));
    }

    private static AuctionCall RespondToMinorOpeningIntervention(
        Seat seat, HandEvaluation hand, Bid opening, Auction auction)
    {
        var intervention = auction.Calls
            .Where(call => call.Action == AuctionAction.Bid && !SameSide(call.Seat, seat) &&
                Auction.Compare(call.Bid!.Value, opening) > 0)
            .Select(call => call.Bid!.Value)
            .LastOrDefault();

        if (intervention == default)
            return BidCall(seat, FirstResponse(hand, opening));

        var interventionSuit = (Suit)intervention.Denomination;
        var unbidFourCardMajor = Enum.GetValues<Suit>().Any(suit =>
            suit is Suit.Hearts or Suit.Spades && hand.SuitLength(suit) >= 4 &&
            !auction.Calls.Any(call => call.Action == AuctionAction.Bid &&
                call.Bid!.Value.Denomination == ToDenomination(suit)));

        if (intervention.Level >= 2 && hand.SuitLength(interventionSuit) == 0 &&
            hand.HonorLengthPoints >= 13 && !unbidFourCardMajor)
            return BidCall(seat, new Bid(intervention.Level + 1, intervention.Denomination));

        if (hand.SuitLength((Suit)opening.Denomination) >= 5)
        {
            if (hand.HighCardPoints is >= 9 and <= 11)
                return BidCall(seat, new Bid(3, opening.Denomination));
            if (hand.HighCardPoints is >= 6 and <= 8)
                return BidCall(seat, new Bid(2, opening.Denomination));
        }

        var sevenCardSuit = Enum.GetValues<Suit>()
            .Where(suit => hand.SuitLength(suit) >= 7)
            .OrderByDescending(hand.SuitLength)
            .FirstOrDefault();
        if (hand.HighCardPoints <= 10 && hand.SuitLength(sevenCardSuit) >= 7)
        {
            var preempt = new Bid(Math.Min(4, intervention.Level + 1), ToDenomination(sevenCardSuit));
            if (auction.IsLegal(preempt)) return BidCall(seat, preempt);
        }

        var oneLevelSuit = BestResponseSuit(hand, opening, intervention, 4);
        if (intervention.Level == 1 && hand.HonorLengthPoints >= 7 && oneLevelSuit.HasValue)
            return BidCall(seat, CheapestBid(ToDenomination(oneLevelSuit.Value), intervention));

        var twoLevelSuit = BestResponseSuit(hand, opening, intervention, 5);
        if (intervention.Level == 2 && hand.HonorLengthPoints >= 11 && twoLevelSuit.HasValue)
            return BidCall(seat, CheapestBid(ToDenomination(twoLevelSuit.Value), intervention));

        if (hand.HonorLengthPoints >= 13 && !unbidFourCardMajor &&
            auction.IsLegal(new Bid(intervention.Level + 1, intervention.Denomination)))
            return BidCall(seat, new Bid(intervention.Level + 1, intervention.Denomination));

        if (hand.HonorLengthPoints >= 8 && AllowsSputnikDouble(intervention, hand))
            return new(seat, AuctionAction.Double);

        if (hand.IsBalanced && hand.HonorLengthPoints is >= 8 and <= 10 &&
            auction.IsLegal(new Bid(1, Denomination.NoTrump)))
            return BidCall(seat, new Bid(1, Denomination.NoTrump));
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 11 and <= 12 &&
            auction.IsLegal(new Bid(2, Denomination.NoTrump)))
            return BidCall(seat, new Bid(2, Denomination.NoTrump));
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 13 and <= 15 &&
            auction.IsLegal(new Bid(3, Denomination.NoTrump)))
            return BidCall(seat, new Bid(3, Denomination.NoTrump));

        return new(seat, AuctionAction.Pass);
    }

    private static Suit? BestResponseSuit(HandEvaluation hand, Bid opening, Bid intervention, int minimumLength) =>
        Enum.GetValues<Suit>()
            .Where(suit => hand.SuitLength(suit) >= minimumLength &&
                ToDenomination(suit) != opening.Denomination &&
                ToDenomination(suit) != intervention.Denomination)
            .OrderByDescending(hand.SuitLength)
            .ThenBy(suit => ToDenomination(suit))
            .Cast<Suit?>()
            .FirstOrDefault();

    private static bool AllowsSputnikDouble(Bid intervention, HandEvaluation hand) => intervention.Denomination switch
    {
        Denomination.Diamonds => !hand.HasFourCardMajor,
        Denomination.Hearts => hand.SuitLength(Suit.Spades) < 4,
        Denomination.Spades => hand.SuitLength(Suit.Hearts) >= 4,
        _ => false
    };

    private static Bid? RespondToIntervention(HandEvaluation hand, Bid intervention, Auction auction)
    {
        if (intervention.Denomination != Denomination.NoTrump)
        {
            var support = hand.SuitLength((Suit)intervention.Denomination);
            // Un soutien compétitif direct ne doit jamais transformer une intervention
            // de chelem en grand chelem automatique.
            if (support >= 3 && intervention.Level <= 3)
            {
                var raise = new Bid(intervention.Level + 1, intervention.Denomination);
                if (auction.IsLegal(raise)) return raise;
            }

            if (intervention.Level >= 4)
                return null;

            var major = new[] { Suit.Hearts, Suit.Spades }
                .Where(suit => hand.SuitLength(suit) >= 4)
                .OrderByDescending(hand.SuitLength)
                .ThenByDescending(suit => suit)
                .Select(suit => ToDenomination(suit))
                .FirstOrDefault();
            if (major != Denomination.Clubs)
            {
                var bid = CheapestBid(major, intervention);
                if (auction.IsLegal(bid)) return bid;
            }
        }

        return hand.HighCardPoints >= 8 && auction.IsLegal(new Bid(1, Denomination.NoTrump))
            ? new Bid(1, Denomination.NoTrump)
            : null;
    }

    private static Bid RespondToLandy(HandEvaluation hand)
    {
        if (hand.SuitLength(Suit.Spades) >= 5) return new Bid(2, Denomination.Spades);
        if (hand.SuitLength(Suit.Hearts) >= 5) return new Bid(2, Denomination.Hearts);
        if (hand.SuitLength(Suit.Hearts) >= 4 && hand.SuitLength(Suit.Spades) >= 4)
            return new Bid(2, Denomination.Diamonds);
        return new Bid(2, Denomination.NoTrump);
    }

    private static AuctionCall RespondToTakeoutDouble(Seat seat, HandEvaluation hand, Auction auction)
    {
        var opponentBid = auction.Calls.Last(call => call.Action == AuctionAction.Bid).Bid!.Value;
        var majors = new[] { Suit.Hearts, Suit.Spades }
            .Where(suit => hand.SuitLength(suit) >= 4)
            .OrderByDescending(hand.SuitLength)
            .ThenByDescending(suit => suit)
            .ToList();
        var chosen = majors.FirstOrDefault();
        if (hand.HighCardPoints >= 7 && chosen != default)
        {
            var bid = new Bid(hand.HighCardPoints >= 9 ? 2 : 1, ToDenomination(chosen));
            if (auction.IsLegal(bid)) return BidCall(seat, bid);
        }

        var longest = Enum.GetValues<Suit>()
            .Where(suit => hand.SuitLength(suit) >= 4)
            .OrderByDescending(hand.SuitLength)
            .ThenByDescending(suit => suit)
            .FirstOrDefault();
        if (hand.SuitLength(longest) >= 4)
        {
            var bid = CheapestBid(ToDenomination(longest), opponentBid);
            if (auction.IsLegal(bid)) return BidCall(seat, bid);
        }

        if (hand.HighCardPoints is >= 7 and <= 9 && hand.IsNoTrumpOpeningShape &&
            HasGuard(SuitFrom(opponentBid), hand) && auction.IsLegal(new Bid(1, Denomination.NoTrump)))
            return BidCall(seat, new Bid(1, Denomination.NoTrump));

        return CompetitiveFallback(seat, hand, auction);
    }

    private static AuctionCall Intervention(Seat seat, HandEvaluation hand, Auction auction)
    {
        var opponentOpening = auction.Calls.First(call => call.Action == AuctionAction.Bid).Bid!.Value;
        var isReopening = auction.Calls.Count >= 3 &&
            auction.Calls.Skip(1).All(call => call.IsPass);

        if (isReopening && opponentOpening.Level == 1 &&
            opponentOpening.Denomination != Denomination.NoTrump)
        {
            var suit = Enum.GetValues<Suit>()
                .Where(candidate => ToDenomination(candidate) != opponentOpening.Denomination &&
                    hand.SuitLength(candidate) >= 5)
                .OrderByDescending(hand.SuitLength)
                .ThenByDescending(candidate => candidate)
                .FirstOrDefault();
            if (hand.SuitLength(suit) >= 5 && hand.HonorLengthPoints >= 7)
            {
                var bid = CheapestBid(ToDenomination(suit), opponentOpening);
                if (auction.IsLegal(bid)) return BidCall(seat, bid);
            }
            if (auction.CanDouble(seat) && hand.TotalPoints >= 10)
                return new(seat, AuctionAction.Double);
        }

        if (opponentOpening == new Bid(1, Denomination.NoTrump) && hand.HighCardPoints >= 10 &&
            hand.SuitLength(Suit.Hearts) >= 4 && hand.SuitLength(Suit.Spades) >= 4)
            return BidCall(seat, new Bid(2, Denomination.Clubs));

        if (opponentOpening.Level == 1 && opponentOpening.Denomination != Denomination.NoTrump)
        {
            if (hand.IsNoTrumpOpeningShape && hand.HighCardPoints is >= 16 and <= 18 &&
                HasGuard(SuitFrom(opponentOpening), hand))
                return BidCall(seat, new Bid(1, Denomination.NoTrump));
            var suits = Enum.GetValues<Suit>()
                .Where(suit => ToDenomination(suit) != opponentOpening.Denomination && hand.SuitLength(suit) >= 5)
                .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).ToList();
            var jumpSuit = suits.FirstOrDefault(suit => hand.SuitLength(suit) >= 6 &&
                SuitHonors(hand, suit) >= 2 && hand.HighCardPoints <= 10);
            if (suits.Contains(jumpSuit) && auction.IsLegal(new Bid(2, ToDenomination(jumpSuit))))
                return BidCall(seat, new Bid(2, ToDenomination(jumpSuit)));
            if (suits.Count > 0 && hand.HonorLengthPoints is >= 9 and <= 18)
            {
                var bid = CheapestBid(ToDenomination(suits[0]), opponentOpening);
                if (auction.IsLegal(bid)) return BidCall(seat, bid);
            }
            var opponentSuit = (Suit)opponentOpening.Denomination;
            var suitableShape = hand.SuitLength(opponentSuit) <= 2 &&
                Enum.GetValues<Suit>().Count(suit => suit != opponentSuit && hand.SuitLength(suit) >= 3) >= 2;
            if (auction.CanDouble(seat) && (hand.HighCardPoints >= 19 || hand.TotalPoints >= 14 && suitableShape))
                return new(seat, AuctionAction.Double);
        }
        return CompetitiveFallback(seat, hand, auction);
    }

    private static Suit SuitFrom(Bid bid) => (Suit)bid.Denomination;

    private static bool HasGuard(Suit suit, HandEvaluation hand) =>
        hand.Cards.Any(card => card.Suit == suit && card.Rank >= Rank.King) ||
        hand.Cards.Contains(new Card(suit, Rank.Queen)) && hand.SuitLength(suit) >= 3;

    private static AuctionCall CompetitiveFallback(Seat seat, HandEvaluation hand, Auction auction)
    {
        if (auction.CanRedouble(seat) && hand.HighCardPoints >= 10) return new(seat, AuctionAction.Redouble);
        if (auction.CanDouble(seat) && hand.HighCardPoints >= 16) return new(seat, AuctionAction.Double);
        return new(seat, AuctionAction.Pass);
    }
}
