namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

/// <summary>Conventions SEF : Roudi et quatrième couleur forcing.</summary>
public sealed partial class SefBiddingEngine
{
    private static bool IsRoudiQuestion(IReadOnlyList<Bid> bids) => bids.Count == 4 && IsRoudiSequence(bids);

    private static bool IsRoudiSequence(IReadOnlyList<Bid> bids) => bids.Count >= 4 &&
        bids[0].Level == 1 && bids[0].Denomination is Denomination.Clubs or Denomination.Diamonds &&
        bids[1].Level == 1 && bids[1].Denomination is Denomination.Hearts or Denomination.Spades &&
        bids[2] == new Bid(1, Denomination.NoTrump) &&
        bids[3] == new Bid(2, Denomination.Clubs);

    private static Bid RoudiResponse(HandEvaluation hand, Denomination responderMajor)
    {
        var support = hand.SuitLength((Suit)responderMajor);
        if (support < 3)
            return new Bid(2, Denomination.Diamonds);
        if (hand.HighCardPoints <= 13)
            return new Bid(2, responderMajor);
        var otherMajor = responderMajor == Denomination.Hearts ? Denomination.Spades : Denomination.Hearts;
        return new Bid(2, otherMajor);
    }

    private static Bid AfterRoudi(HandEvaluation hand, Denomination major, Bid answer)
    {
        var otherMajor = major == Denomination.Hearts ? Denomination.Spades : Denomination.Hearts;
        var hasSupport = answer.Denomination == major || answer.Denomination == otherMajor;
        if (hasSupport)
            return new Bid(hand.HonorLengthPoints >= 13 ? 4 : 3, major);
        return new Bid(hand.HighCardPoints >= 12 ? 3 : 2, Denomination.NoTrump);
    }

    private static bool IsFourthSuitForcing(IReadOnlyList<Bid> bids)
    {
        return bids.Count == 4 && IsFourthSuitSequence(bids);
    }

    private static bool IsFourthSuitSequence(IReadOnlyList<Bid> bids)
    {
        if (bids.Count < 4 || bids.Take(4).Any(bid => bid.Denomination == Denomination.NoTrump))
            return false;

        var firstThree = bids.Take(3).Select(bid => bid.Denomination).Distinct().ToList();
        return firstThree.Count == 3 && !firstThree.Contains(bids[3].Denomination);
    }

    private static Bid? FourthSuitResponse(HandEvaluation hand, IReadOnlyList<Bid> bids)
    {
        var responderSuit = bids[1].Denomination;
        var forcingSuit = bids[3];
        if (hand.SuitLength((Suit)responderSuit) >= 3)
            return CheapestBid(responderSuit, forcingSuit);
        if (hand.SuitLength((Suit)forcingSuit.Denomination) >= 4)
            return CheapestBid(forcingSuit.Denomination, forcingSuit);
        if (hand.IsNoTrumpOpeningShape)
            return CheapestBid(Denomination.NoTrump, forcingSuit);
        var openingSuit = bids[0].Denomination;
        if (hand.SuitLength((Suit)openingSuit) >= 6)
            return CheapestBid(openingSuit, forcingSuit);
        return CheapestBid(bids[2].Denomination, forcingSuit);
    }

    private static Bid AfterFourthSuit(HandEvaluation hand, IReadOnlyList<Bid> bids)
    {
        var responderSuit = bids[1].Denomination;
        var answer = bids[4];
        if (answer.Denomination == responderSuit && hand.SuitLength((Suit)responderSuit) >= 5)
            return new Bid(4, responderSuit);
        if (answer.Denomination is Denomination.Hearts or Denomination.Spades &&
            hand.SuitLength((Suit)answer.Denomination) >= 4)
            return new Bid(4, answer.Denomination);
        return new Bid(3, Denomination.NoTrump);
    }
}
