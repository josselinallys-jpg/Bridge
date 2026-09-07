namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

/// <summary>Chapitres SEF : ouvertures (mineures, majeures, 1SA, barrages, fortes).</summary>
public sealed partial class SefBiddingEngine
{
    private static Bid? Opening(HandEvaluation hand)
    {
        var h = hand.HighCardPoints;
        if (hand.IsNoTrumpOpeningShape && h >= 24) return new(2, Denomination.Diamonds);
        if (hand.IsNoTrumpOpeningShape && h is >= 22 and <= 23) return new(2, Denomination.Clubs);
        if (hand.IsNoTrumpOpeningShape && h is >= 20 and <= 21) return new(2, Denomination.NoTrump);
        if (h >= 24) return new(2, Denomination.Diamonds);
        // 2♣ fort indéterminé : très belle couleur au moins cinquième, 20-23HL.
        if (hand.HonorLengthPoints is >= 20 and <= 23 && LongestLength(hand) >= 5)
            return new(2, Denomination.Clubs);

        // 2 faible : six cartes exactement, belle couleur, au plus un gros honneur annexe.
        var weakMajor = LongerMajor(hand);
        var otherMajor = weakMajor == Suit.Spades ? Suit.Hearts : Suit.Spades;
        if (h is >= 5 and <= 10 && hand.SuitLength(weakMajor) == 6 &&
            hand.SuitLength(otherMajor) < 4 && SuitHonors(hand, weakMajor) >= 2 &&
            SideBigHonors(hand, weakMajor) <= 1)
            return new(2, ToDenomination(weakMajor));

        // 3SA gambling : mineure maîtresse de sept cartes, au plus une dame annexe.
        foreach (var minor in new[] { Suit.Clubs, Suit.Diamonds })
            if (hand.SuitLength(minor) >= 7 && HasTopHonors(hand, minor) &&
                SideBigHonors(hand, minor) == 0 && SideQueens(hand, minor) <= 1)
                return new(3, Denomination.NoTrump);

        var longest = hand.LongestSuit;
        if (h is >= 5 and <= 10 && hand.SuitLength(longest) >= 7)
            return new(Math.Min(4, hand.SuitLength(longest) - 4), ToDenomination(longest));

        // Ouverture au palier de 1 : 12H, ou 11H avec 13HL.
        if (h < 12 && !(h == 11 && hand.HonorLengthPoints >= 13)) return null;
        if (hand.IsNoTrumpOpeningShape && h is >= 15 and <= 17) return new(1, Denomination.NoTrump);

        var hearts = hand.SuitLength(Suit.Hearts);
        var spades = hand.SuitLength(Suit.Spades);
        if (hearts >= 5 || spades >= 5)
            return new(1, ToDenomination(spades >= hearts ? Suit.Spades : Suit.Hearts));

        var clubs = hand.SuitLength(Suit.Clubs);
        var diamonds = hand.SuitLength(Suit.Diamonds);
        return new(1, diamonds > clubs || diamonds == clubs && diamonds >= 4
            ? Denomination.Diamonds : Denomination.Clubs);
    }

    private static int LongestLength(HandEvaluation hand) => Enum.GetValues<Suit>().Max(hand.SuitLength);

    private static Suit LongerMajor(HandEvaluation hand) =>
        hand.SuitLength(Suit.Spades) >= hand.SuitLength(Suit.Hearts) ? Suit.Spades : Suit.Hearts;

    private static int SuitHonors(HandEvaluation hand, Suit suit) =>
        hand.Cards.Count(card => card.Suit == suit && card.Rank >= Rank.Ten);

    private static int SideBigHonors(HandEvaluation hand, Suit suit) =>
        hand.Cards.Count(card => card.Suit != suit && card.Rank >= Rank.King);

    private static int SideQueens(HandEvaluation hand, Suit suit) =>
        hand.Cards.Count(card => card.Suit != suit && card.Rank == Rank.Queen);

    private static bool HasTopHonors(HandEvaluation hand, Suit suit) =>
        new[] { Rank.Ace, Rank.King, Rank.Queen }.All(rank => hand.Cards.Contains(new Card(suit, rank)));
}
