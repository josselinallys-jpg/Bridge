namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

public sealed partial class SefBiddingEngine
{
    private Denomination? MinorTexasTrump(IReadOnlyList<Bid> bids)
    {
        if (!Profile.IsConventionEnabled("minorTransfers") || bids.Count < 3 ||
            bids[0] != new Bid(1, Denomination.NoTrump)) return null;
        if (bids[1] == new Bid(2, Denomination.Spades) && bids[2] == new Bid(3, Denomination.Clubs))
            return Denomination.Clubs;
        if (bids[1] == new Bid(3, Denomination.Clubs) && bids[2] == new Bid(3, Denomination.Diamonds))
            return Denomination.Diamonds;
        return null;
    }

    private static Denomination? MinorTexasShortMajor(IReadOnlyList<Bid> bids) =>
        bids.Count >= 4 && bids[3] is { Level: 3, Denomination: Denomination.Hearts or Denomination.Spades }
            ? bids[3].Denomination : null;

    private int? MinorTexasFact(string fact, Seat seat, HandEvaluation hand,
        IReadOnlyList<AuctionCall> sideBids, IReadOnlyList<AuctionCall> opponents,
        Denomination? candidate, int honorLength, int distribution)
    {
        var bids = sideBids.Select(call => call.Bid!.Value).ToArray();
        var minor = opponents.Count == 0 ? MinorTexasTrump(bids) : null;
        var shortMajor = minor.HasValue ? MinorTexasShortMajor(bids) : null;
        var agreed = minor.HasValue && shortMajor.HasValue && bids.Length >= 5 &&
            bids[4].Level >= 4 && bids[4].Denomination != Denomination.NoTrump;
        var ownIndex = sideBids.Count > 0 && sideBids[0].Seat == seat ? 0 : 1;
        var partnerControls = sideBids.Select((call, index) => (call, index))
            .Where(item => item.index >= 4 && item.index % 2 != ownIndex &&
                item.call.Bid is { Level: >= 4, Denomination: not Denomination.NoTrump } &&
                item.call.Bid.Value.Denomination != minor)
            .Select(item => item.call.Bid!.Value.Denomination).ToHashSet();
        var shownControls = bids.Skip(4).Where(bid => bid.Level >= 4 && bid.Denomination != Denomination.NoTrump &&
            bid.Denomination != minor).Select(bid => bid.Denomination).ToHashSet();
        var controlsCovered = minor.HasValue && shortMajor.HasValue && Enum.GetValues<Suit>()
            .Where(suit => (Denomination)suit != minor)
            .All(suit => (Denomination)suit == shortMajor || partnerControls.Contains((Denomination)suit) ||
                hand.SuitLength(suit) <= 1 || hand.Cards.Any(card => card.Suit == suit && card.Rank is Rank.Ace or Rank.King));
        var blackwoodAnswered = agreed && bids.Length >= 8 && bids[^2] == new Bid(4, Denomination.NoTrump) &&
            bids[^1] is { Level: 5 };
        var ownKeys = minor.HasValue ? hand.AceCount + (hand.Cards.Contains(new Card((Suit)minor.Value, Rank.King)) ? 1 : 0) : 0;
        var partnerKeys = blackwoodAnswered ? bids[^1].Denomination switch
        {
            Denomination.Clubs => ownKeys >= 2 ? 3 : 0,
            Denomination.Diamonds => ownKeys >= 1 ? 4 : 1,
            Denomination.Hearts or Denomination.Spades => 2,
            _ => 0
        } : 0;
        return fact switch
        {
            "minorTexas" => minor.HasValue ? 1 : 0,
            "minorTexasShortness" => shortMajor.HasValue ? 1 : 0,
            "minorTexasAgreed" => agreed ? 1 : 0,
            "minorTexasFitPoints" => minor.HasValue ? honorLength + distribution +
                FitBonus(hand.SuitLength((Suit)minor.Value) + (ownIndex == 0 ? 5 : 3)) : null,
            "candidateIsTransferredMinor" => candidate.HasValue && minor.HasValue ? candidate == minor ? 1 : 0 : null,
            "candidateIsShortMajor" => candidate.HasValue && shortMajor.HasValue ? candidate == shortMajor ? 1 : 0 : null,
            "candidateIsShownControl" => candidate.HasValue ? shownControls.Contains(candidate.Value) ? 1 : 0 : null,
            "minorTexasControlsCovered" => controlsCovered ? 1 : 0,
            "minorTexasNewControl" => agreed && sideBids.Count >= 6 && sideBids[^1].Bid is
                { Level: >= 4, Denomination: not Denomination.NoTrump } last && last.Denomination != minor &&
                sideBids[^1].Seat != seat ? 1 : 0,
            "minorTexasBlackwoodAnswered" => blackwoodAnswered ? 1 : 0,
            "minorTexasCombinedKeys" => blackwoodAnswered ? ownKeys + partnerKeys : null,
            _ => null
        };
    }
}