namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

/// <summary>Chapitres SEF : ouvertures (mineures, majeures, 1SA, barrages, fortes).</summary>
public sealed partial class SefBiddingEngine
{
    private static Bid? Opening(HandEvaluation hand, Auction auction)
    {
        var h = hand.HighCardPoints;
        var passedBeforeOpening = auction.Calls.Count >= 2 && auction.Calls.All(call => call.IsPass);
        if (passedBeforeOpening && h is >= 8 and <= 9 && hand.HonorLengthPoints >= 8)
        {
            var major = new[] { Suit.Spades, Suit.Hearts }
                .Where(suit => hand.SuitLength(suit) >= 5 && SuitHonors(hand, suit) >= 2)
                .OrderByDescending(hand.SuitLength)
                .ThenByDescending(suit => suit)
                .FirstOrDefault();
            if (hand.SuitLength(major) >= 5)
                return new(1, ToDenomination(major));
        }
        if (hand.HonorLengthPoints >= 24 || IsOneTrickFromGame(hand))
            return new(2, Denomination.Diamonds);
        if (hand.IsNoTrumpOpeningShape && hand.HonorLengthPoints is >= 22 and <= 23)
            return new(2, Denomination.Clubs);
        if (hand.IsNoTrumpOpeningShape && hand.HonorLengthPoints is >= 20 and <= 21)
            return new(2, Denomination.NoTrump);
        // 2♣ fort indéterminé : 21-23HL avec au moins cinq cartes.
        if (hand.HonorLengthPoints is >= 21 and <= 23 && LongestLength(hand) >= 5)
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

        var preempt = Enum.GetValues<Suit>()
            .Where(suit => hand.SuitLength(suit) >= 7)
            .Where(suit =>
            {
                var otherMajor = suit == Suit.Hearts ? Suit.Spades : Suit.Hearts;
                var hasForbiddenMajor = suit is Suit.Clubs or Suit.Diamonds
                    ? hand.SuitLength(Suit.Hearts) >= 4 || hand.SuitLength(Suit.Spades) >= 4
                    : hand.SuitLength(otherMajor) >= 4;
                var level = hand.SuitLength(suit) >= 8 ? 4 : 3;
                var pointsInRange = level == 3 ? h is >= 4 and <= 10 : h is >= 5 and <= 10;
                return pointsInRange && !hasForbiddenMajor && SuitHonors(hand, suit) >= 2 &&
                    SideBigHonors(hand, suit) <= 1;
            })
            .OrderByDescending(hand.SuitLength)
            .ThenByDescending(suit => suit)
            .Select(suit => (Suit?)suit)
            .FirstOrDefault();
        if (preempt.HasValue)
            return new(hand.SuitLength(preempt.Value) >= 8 ? 4 : 3, ToDenomination(preempt.Value));

        // Ouverture au palier de 1 : 12H, ou 11H avec 13HL.
        if (h < 12 && !(h == 11 && hand.HonorLengthPoints >= 13)) return null;
        if (IsOneNoTrumpOpeningShape(hand) && h is >= 15 and <= 17 &&
            (hand.IsNoTrumpOpeningShape || h <= 16))
            return new(1, Denomination.NoTrump);

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

    private static bool IsOneTrickFromGame(HandEvaluation hand)
    {
        var longSuit = hand.LongestSuit;
        var isMajor = longSuit is Suit.Hearts or Suit.Spades;
        if (hand.SuitLength(longSuit) < (isMajor ? 6 : 7)) return false;

        var defensiveTricks = Enum.GetValues<Suit>()
            .Where(suit => suit != longSuit)
            .Sum(suit =>
            {
                var length = hand.SuitLength(suit);
                var tricks = hand.Cards.Contains(new Card(suit, Rank.Ace)) ? 1 : 0;
                if (hand.Cards.Contains(new Card(suit, Rank.King)) &&
                    (length >= 2 || hand.Cards.Contains(new Card(suit, Rank.Ace))))
                    tricks++;
                return tricks;
            });

        var playingTricks = 13 - LosingTricks(hand);
        return defensiveTricks >= 2 && playingTricks >= (isMajor ? 9 : 10);
    }

    private static bool IsOneNoTrumpOpeningShape(HandEvaluation hand)
    {
        if (hand.IsNoTrumpOpeningShape) return true;

        var clubs = hand.SuitLength(Suit.Clubs);
        var diamonds = hand.SuitLength(Suit.Diamonds);
        var hearts = hand.SuitLength(Suit.Hearts);
        var spades = hand.SuitLength(Suit.Spades);
        var lengths = new[] { clubs, diamonds, hearts, spades };

        var fiveFourTwoTwoMinor =
            (clubs == 5 && (diamonds == 4 || hearts == 4)) ||
            (diamonds == 5 && hearts == 4);
        if (hand.IsBalanced && fiveFourTwoTwoMinor && lengths.Count(length => length == 2) == 2)
            return true;

        var sixThreeTwoTwoMinor =
            (clubs == 6 || diamonds == 6) && lengths.Count(length => length == 2) == 2 &&
            lengths.Contains(3) && lengths.Count(length => length == 3) == 1;
        if (sixThreeTwoTwoMinor)
        {
            var shortSuits = Enum.GetValues<Suit>().Where(suit => hand.SuitLength(suit) == 2);
            return shortSuits.All(suit => SuitHonors(hand, suit) >= 1);
        }

        return hand.IsBalanced && (hearts == 5 || spades == 5) && lengths.Count(length => length == 2) == 1;
    }

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
