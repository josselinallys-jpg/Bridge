namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

/// <summary>Chapitre SEF : première enchère du répondant.</summary>
public sealed partial class SefBiddingEngine
{
    private static Bid? FirstResponse(HandEvaluation hand, Bid opening) => opening switch
    {
        { Level: 1, Denomination: Denomination.NoTrump } => RespondToOneNoTrump(hand),
        { Level: 2, Denomination: Denomination.NoTrump } => RespondToTwoNoTrump(hand),
        { Level: 2, Denomination: Denomination.Clubs } => new Bid(2, Denomination.Diamonds),
        { Level: 2, Denomination: Denomination.Diamonds } => RespondToGameForce(hand),
        { Level: 2, Denomination: Denomination.Hearts or Denomination.Spades } => RespondToWeakTwo(hand, opening),
        { Level: 1, Denomination: Denomination.Hearts or Denomination.Spades } => RespondToMajor(hand, opening),
        { Level: 1, Denomination: Denomination.Clubs or Denomination.Diamonds } => RespondToMinor(hand, opening),
        _ => RespondToPreempt(hand, opening)
    };

    private static Bid? RespondToOneNoTrump(HandEvaluation hand)
    {
        var h = hand.HighCardPoints;
        // Stayman dès 8H avec une majeure quatrième (y compris 5-4 majeur).
        if (hand.HasFourCardMajor && h >= 8) return new(2, Denomination.Clubs);
        // Bicolore majeur 5-5 : Texas ♥ faible, 4♦ conventionnel en zone de manche, Texas ♠ ensuite.
        if (hand.SuitLength(Suit.Hearts) >= 5 && hand.SuitLength(Suit.Spades) >= 5)
        {
            if (h >= 10) return new(2, Denomination.Hearts);
            if (h >= 6) return new(4, Denomination.Diamonds);
            return new(2, Denomination.Diamonds);
        }
        // Texas majeur dès 0 point avec cinq cartes.
        if (hand.SuitLength(Suit.Hearts) >= 5) return new(2, Denomination.Diamonds);
        if (hand.SuitLength(Suit.Spades) >= 5) return new(2, Denomination.Hearts);
        // Texas mineur : ambition de partielle avec six cartes.
        if (hand.SuitLength(Suit.Clubs) >= 6 && h <= 9) return new(2, Denomination.Spades);
        if (hand.SuitLength(Suit.Diamonds) >= 6 && h <= 9) return new(3, Denomination.Clubs);
        if (h >= 18) return new(4, Denomination.NoTrump); // invitation quantitative
        if (h >= 16) return new(4, Denomination.NoTrump); // quantitatif : proposition de chelem
        if (h >= 10) return new(3, Denomination.NoTrump);
        if (h == 9) return new(2, Denomination.NoTrump);
        // Règle du 8 avec 8H : 2SA si plus de huit cartes de rang ≥ 8.
        if (h == 8 && hand.Cards.Count(card => card.Rank >= Rank.Eight) > 8)
            return new(2, Denomination.NoTrump);
        return null;
    }

    private static Bid? RespondToTwoNoTrump(HandEvaluation hand)
    {
        if (hand.HasFourCardMajor && hand.HonorLengthPoints >= 4) return new(3, Denomination.Clubs);
        if (hand.SuitLength(Suit.Hearts) >= 5) return new(3, Denomination.Diamonds);
        if (hand.SuitLength(Suit.Spades) >= 5) return new(3, Denomination.Hearts);
        if (hand.SuitLength(Suit.Clubs) >= 6) return new(3, Denomination.Spades);
        if (hand.SuitLength(Suit.Diamonds) >= 6) return new(4, Denomination.Clubs);
        if (hand.HighCardPoints >= 14) return new(4, Denomination.NoTrump); // invitation quantitative
        if (hand.HighCardPoints >= 11) return new(4, Denomination.NoTrump); // quantitatif
        return hand.HonorLengthPoints >= 4 ? new(3, Denomination.NoTrump) : null;
    }

    // Réponses en As sur 2♦ forcing de manche.
    private static Bid RespondToGameForce(HandEvaluation hand)
    {
        if (hand.AceCount >= 2) return new(3, Denomination.NoTrump);
        if (hand.AceCount == 1)
        {
            if (hand.Cards.Contains(new Card(Suit.Clubs, Rank.Ace))) return new(3, Denomination.Clubs);
            if (hand.Cards.Contains(new Card(Suit.Diamonds, Rank.Ace))) return new(3, Denomination.Diamonds);
            return new(2, Denomination.Spades); // As majeur
        }
        return hand.HighCardPoints >= 8
            ? new(2, Denomination.NoTrump)
            : new(2, Denomination.Hearts);
    }

    private static Bid? RespondToWeakTwo(HandEvaluation hand, Bid opening)
    {
        var support = hand.SuitLength((Suit)opening.Denomination);
        // Relais 2SA forcing : espoir de manche (15HLD et plus).
        if (hand.TotalPoints >= 15) return new(2, Denomination.NoTrump);
        // Changement de couleur forcing : cinq cartes en majeure, six en mineure, avec des points.
        if (hand.HonorLengthPoints >= 13)
        {
            var suit = Enum.GetValues<Suit>()
                .Where(candidate => ToDenomination(candidate) != opening.Denomination &&
                    hand.SuitLength(candidate) >= (candidate is Suit.Hearts or Suit.Spades ? 5 : 6))
                .OrderByDescending(hand.SuitLength).ThenByDescending(candidate => candidate)
                .Cast<Suit?>().FirstOrDefault();
            if (suit.HasValue) return CheapestBid(ToDenomination(suit.Value), opening);
        }
        // Soutiens barrage, loi des atouts : neuf atouts → palier 3, dix → palier 4.
        if (support >= 4) return new(4, opening.Denomination);
        if (support >= 3) return new(3, opening.Denomination);
        return null;
    }

    private static Bid? RespondToMajor(HandEvaluation hand, Bid opening)
    {
        var support = hand.SuitLength((Suit)opening.Denomination);
        if (hand.HonorLengthPoints < 6) return null;
        Suit? singleton = Enum.GetValues<Suit>()
            .Where(suit => suit != (Suit)opening.Denomination && hand.SuitLength(suit) <= 1)
            .OrderBy(suit => hand.SuitLength(suit))
            .Cast<Suit?>()
            .FirstOrDefault();
        if (support >= 4 && hand.TotalPoints >= 13 && singleton.HasValue)
            return new(4, ToDenomination(singleton.Value));
        if (support == 3 && hand.TotalPoints is >= 11 and <= 12)
            return new(2, Denomination.NoTrump);
        if (support >= 3)
        {
            // Fit : zones SEF en HLD revalorisés (l'ouvreur promet cinq atouts).
            var points = FitPoints(hand, (Suit)opening.Denomination, 5);
            if (points <= 10) return new(2, opening.Denomination);
            if (points <= 12) return new(3, opening.Denomination);
            if (points <= 15 || hand.HonorLengthPoints < 13) return new(4, opening.Denomination);
            // 16+ HLD et 13+ HL : chelem possible, passage par un changement de couleur.
        }
        if (opening.Denomination == Denomination.Hearts && hand.SuitLength(Suit.Spades) >= 4)
            return new(1, Denomination.Spades);

        var newSuit = BestNewSuit(hand, opening.Denomination, 4);
        if (hand.HonorLengthPoints >= 13 && newSuit.HasValue)
        {
            if (opening.Denomination == Denomination.Spades && newSuit == Denomination.Hearts &&
                hand.SuitLength(Suit.Hearts) < 5) newSuit = BestMinor(hand, 4);
            if (newSuit.HasValue) return new(2, newSuit.Value);
        }
        if (support >= 3) return new(4, opening.Denomination);
        if (hand.HighCardPoints >= 13) return new(3, Denomination.NoTrump);
        if (hand.HighCardPoints >= 11) return new(2, Denomination.NoTrump);
        return new(1, Denomination.NoTrump);
    }

    private static Bid? RespondToMinor(HandEvaluation hand, Bid opening)
    {
        if (hand.HonorLengthPoints < 6) return null;
        var candidates = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 4).ToList();

        // En zone 6-12HL, la majeure quatrième prime sur une mineure même plus longue.
        if (hand.HonorLengthPoints < 13 && candidates.Any(suit => suit is Suit.Hearts or Suit.Spades))
            candidates.RemoveAll(suit => suit is Suit.Clubs or Suit.Diamonds);

        if (candidates.Count > 0)
        {
            var maxLength = candidates.Max(hand.SuitLength);
            var longest = candidates.Where(suit => hand.SuitLength(suit) == maxLength);
            // Deux cinquièmes : la plus chère ; deux quatrièmes : la plus économique au palier de 1.
            var chosen = maxLength >= 5
                ? longest.Max()
                : longest.OrderBy(suit => ToDenomination(suit) > opening.Denomination ? 0 : 1)
                    .ThenBy(suit => suit).First();
            var bid = new Bid(1, ToDenomination(chosen));
            if (Auction.Compare(bid, opening) > 0) return bid;
            if (hand.HonorLengthPoints >= 11) return new(2, ToDenomination(chosen));
        }
        var support = hand.SuitLength((Suit)opening.Denomination);
        // Soutien simple : cinq cartes, 5-10 HLD.
        if (support >= 5 && hand.TotalPoints <= 10) return new(2, opening.Denomination);
        if (support >= 5 && hand.TotalPoints >= 11) return new(3, opening.Denomination);
        if (hand.HighCardPoints >= 13) return new(3, Denomination.NoTrump);
        if (hand.HighCardPoints >= 11) return new(2, Denomination.NoTrump);
        return new(1, Denomination.NoTrump);
    }

    private static Bid? RespondToPreempt(HandEvaluation hand, Bid opening)
    {
        if (opening.Denomination == Denomination.NoTrump) return null;
        var support = hand.SuitLength((Suit)opening.Denomination);
        // Loi des atouts : allonger le barrage majeur à la manche avec le fit troisième.
        if (opening.Level == 3 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            support >= 3)
            return new(4, opening.Denomination);
        return support >= 3 && hand.TotalPoints >= 14 && opening.Level < 4
            ? new(Math.Min(4, opening.Level + 1), opening.Denomination) : null;
    }
}
