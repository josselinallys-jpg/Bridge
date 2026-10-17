namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

/// <summary>Chapitre SEF : première enchère du répondant.</summary>
public sealed partial class SefBiddingEngine
{
    private static Bid? FirstResponse(HandEvaluation hand, Bid opening, Auction auction) => opening switch
    {
        { Level: 1, Denomination: Denomination.NoTrump } => RespondToOneNoTrump(hand),
        { Level: 2, Denomination: Denomination.NoTrump } => RespondToTwoNoTrump(hand),
        { Level: 2, Denomination: Denomination.Clubs } => new Bid(2, Denomination.Diamonds),
        { Level: 2, Denomination: Denomination.Diamonds } => RespondToGameForce(hand),
        { Level: 2, Denomination: Denomination.Hearts or Denomination.Spades } => RespondToWeakTwo(hand, opening),
        { Level: 1, Denomination: Denomination.Hearts or Denomination.Spades } => IsThirdOrFourthSeatOpening(auction, opening)
            ? RespondToMajorThirdOrFourthSeat(hand, opening)
            : RespondToMajor(hand, opening),
        { Level: 1, Denomination: Denomination.Clubs or Denomination.Diamonds } => IsThirdOrFourthSeatOpening(auction, opening)
            ? RespondToMinorThirdSeat(hand, opening)
            : RespondToMinor(hand, opening),
        _ => RespondToPreempt(hand, opening)
    };

    // Ouverture en 3e/4e position : précédée d'au moins deux passes avant le premier enchérisseur.
    private static bool IsThirdOrFourthSeatOpening(Auction auction, Bid opening)
    {
        var openingIndex = auction.Calls.ToList().FindIndex(call => call.Bid == opening);
        return openingIndex >= 2 && auction.Calls.Take(openingIndex).All(call => call.IsPass);
    }

    private static Bid? RespondToMajorThirdOrFourthSeat(HandEvaluation hand, Bid opening)
    {
        var support = hand.SuitLength((Suit)opening.Denomination);
        var fitPoints = support >= 3 ? MajorSupportPoints(hand, (Suit)opening.Denomination, 5) : 0;
        var singleton = Enum.GetValues<Suit>()
            .Where(suit => suit != (Suit)opening.Denomination && hand.SuitLength(suit) <= 1)
            .OrderBy(suit => hand.SuitLength(suit)).FirstOrDefault();

        if (support == 4 && fitPoints is >= 11 and <= 12 && hand.SuitLength(singleton) <= 1)
            return new(2, Denomination.NoTrump);
        if (support >= 3 && fitPoints is >= 11 and <= 12)
            return new(2, Denomination.Clubs);
        if (support >= 4 && hand.HighCardPoints is >= 8 and <= 11)
        {
            var meetingSuit = Enum.GetValues<Suit>()
                .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 5)
                .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
            if (hand.SuitLength(meetingSuit) >= 5)
            {
                var natural = new Bid(1, ToDenomination(meetingSuit));
                var level = Auction.Compare(natural, opening) > 0 ? 2 : 3;
                return new(level, ToDenomination(meetingSuit));
            }
        }
        if (support >= 4 && hand.HighCardPoints <= 10)
            return new(3, opening.Denomination);
        if (support >= 5 && hand.HighCardPoints >= 8)
            return new(4, opening.Denomination);

        var sixCardSuit = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 6)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
        if (hand.HighCardPoints is >= 8 and <= 10 && hand.SuitLength(sixCardSuit) >= 6)
        {
            var bid = new Bid(2, ToDenomination(sixCardSuit));
            if (Auction.Compare(bid, opening) > 0) return bid;
        }

        var newSuit = BestResponseSuitAfterMajor(hand, opening, true);
        if (newSuit.HasValue) return newSuit.Value;
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 6 and <= 11)
            return new(1, Denomination.NoTrump);
        return null;
    }

    private static Bid? RespondToOneNoTrump(HandEvaluation hand)
    {
        var h = hand.HighCardPoints;
        var hl = hand.HonorLengthPoints;
        // Le Stayman est prioritaire dès 8HL, même avec une majeure cinquième ou une mineure sixième.
        if (hand.HasFourCardMajor && hl >= 8) return new(2, Denomination.Clubs);
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
        var slamSuit = new[] { Suit.Diamonds, Suit.Hearts, Suit.Spades }
            .Where(suit => hand.SuitLength(suit) >= 6)
            .OrderByDescending(hand.SuitLength)
            .ThenByDescending(suit => suit)
            .FirstOrDefault();
        if (hand.SuitLength(slamSuit) >= 6 && hl >= 16)
            return new(3, ToDenomination(slamSuit));
        // Texas mineur : six cartes, ou cinq cartes avec une courte / une ambition de chelem.
        if (IsMinorTexasHand(hand, Suit.Clubs)) return new(2, Denomination.Spades);
        if (IsMinorTexasHand(hand, Suit.Diamonds)) return new(3, Denomination.Clubs);
        if (hl is >= 22 and <= 25) return new(7, Denomination.NoTrump);
        if (hl is >= 20 and <= 21) return new(5, Denomination.NoTrump);
        if (hl is >= 18 and <= 19) return new(6, Denomination.NoTrump);
        if (hl is >= 16 and <= 17) return new(4, Denomination.NoTrump);
        if (hl is >= 10 and <= 15) return new(3, Denomination.NoTrump);
        if (hl is >= 8 and <= 9) return new(2, Denomination.NoTrump);
        return null;
    }

    private static bool IsMinorTexasHand(HandEvaluation hand, Suit minor)
    {
        if (hand.SuitLength(minor) < 5 || hand.HasFourCardMajor) return false;
        var hasShortness = Enum.GetValues<Suit>().Any(suit => suit != minor && hand.SuitLength(suit) <= 1);
        return hand.SuitLength(minor) >= 6 && hand.HonorLengthPoints <= 9 || hasShortness ||
            hand.HonorLengthPoints >= 16;
    }

    private static Bid? RespondToTwoNoTrump(HandEvaluation hand, bool gameForcing = false)
    {
        var hl = hand.HonorLengthPoints;
        if (hand.HasFourCardMajor && (gameForcing || hl >= 4)) return new(3, Denomination.Clubs);
        if (hand.SuitLength(Suit.Hearts) >= 5 && hand.SuitLength(Suit.Spades) >= 5)
            return new(4, Denomination.Diamonds);
        if (hand.SuitLength(Suit.Hearts) >= 6) return new(4, Denomination.Hearts);
        if (hand.SuitLength(Suit.Spades) >= 6) return new(4, Denomination.Spades);
        if (hand.SuitLength(Suit.Hearts) >= 5) return new(3, Denomination.Diamonds);
        if (hand.SuitLength(Suit.Spades) >= 5) return new(3, Denomination.Hearts);
        if ((gameForcing || hl >= 16) && hand.SuitLength(Suit.Clubs) >= 6) return new(3, Denomination.Spades);
        if ((gameForcing || hl >= 16) && hand.SuitLength(Suit.Diamonds) >= 6) return new(4, Denomination.Clubs);
        if (hand.IsBalanced && hl is >= 13 and <= 15) return new(6, Denomination.NoTrump);
        if (hand.IsBalanced && hl == 16) return new(5, Denomination.NoTrump);
        if (hand.IsBalanced && hl == 12) return new(4, Denomination.NoTrump);
        if (hand.IsBalanced && hl >= 4) return new(3, Denomination.NoTrump);
        return null;
    }

    // Réponses en As sur 2♦ forcing de manche.
    private static Bid RespondToGameForce(HandEvaluation hand)
    {
        if (hand.AceCount >= 2)
        {
            var aces = hand.Cards.Where(card => card.Rank == Rank.Ace)
                .Select(card => card.Suit)
                .Take(2)
                .ToArray();
            if (aces.Length == 2)
            {
                var sameColor = aces.All(suit => suit is Suit.Clubs or Suit.Spades) ||
                    aces.All(suit => suit is Suit.Diamonds or Suit.Hearts);
                if (sameColor) return new(3, Denomination.Hearts);
                var sameRank = aces.All(suit => suit is Suit.Clubs or Suit.Diamonds) ||
                    aces.All(suit => suit is Suit.Hearts or Suit.Spades);
                if (sameRank) return new(3, Denomination.Spades);
            }
            return new(3, Denomination.NoTrump);
        }
        if (hand.AceCount == 1)
        {
            if (hand.Cards.Contains(new Card(Suit.Clubs, Rank.Ace))) return new(3, Denomination.Clubs);
            if (hand.Cards.Contains(new Card(Suit.Diamonds, Rank.Ace))) return new(3, Denomination.Diamonds);
            return new(2, Denomination.Spades); // As majeur
        }
        var hasNoAceAndShortness = Enum.GetValues<Suit>().Any(suit => hand.SuitLength(suit) <= 1);
        return hand.HighCardPoints >= 8 && !hasNoAceAndShortness
            ? new(2, Denomination.NoTrump)
            : new(2, Denomination.Hearts);
    }

    private static Bid? RespondToWeakTwo(HandEvaluation hand, Bid opening)
    {
        var support = hand.SuitLength((Suit)opening.Denomination);
        var strength = support >= 3
            ? FitPoints(hand, (Suit)opening.Denomination, 6)
            : hand.HonorLengthPoints;
        // Relais 2SA forcing : espoir de manche (15HLD et plus).
        if (strength >= 15) return new(2, Denomination.NoTrump);
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

        var jumpSuit = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 6)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
        if (hand.HonorLengthPoints >= 18 && hand.SuitLength(jumpSuit) >= 6)
            return new(3, ToDenomination(jumpSuit));

        Suit? singleton = Enum.GetValues<Suit>()
            .Where(suit => suit != (Suit)opening.Denomination && hand.SuitLength(suit) <= 1)
            .OrderBy(suit => hand.SuitLength(suit))
            .Cast<Suit?>().FirstOrDefault();
        var fitPoints = support >= 3 ? MajorSupportPoints(hand, (Suit)opening.Denomination, 5) : 0;

        if (support >= 5 && hand.HighCardPoints is >= 8 and <= 10)
            return new(4, opening.Denomination);
        if (support >= 3 && fitPoints <= 10)
            return new(2, opening.Denomination);
        if (support == 3 && fitPoints <= 12)
            return new(2, Denomination.NoTrump);
        if (support >= 4 && fitPoints <= 12)
            return new(3, opening.Denomination);
        if (support == 4 && fitPoints is >= 13 and <= 15 && singleton.HasValue)
            return new(4, ToDenomination(singleton.Value));
        if (support == 4 && fitPoints <= 15 && hand.IsBalanced)
            return new(3, Denomination.NoTrump);

        var newSuit = BestResponseSuitAfterMajor(hand, opening);
        if (newSuit.HasValue) return newSuit.Value;
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 13 and <= 15)
            return new(3, Denomination.NoTrump);
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 11 and <= 12)
            return new(2, Denomination.NoTrump);
        return hand.IsBalanced ? new(1, Denomination.NoTrump) : null;
    }

    private static int MajorSupportPoints(HandEvaluation hand, Suit trump, int partnerLength)
    {
        var combined = hand.SuitLength(trump) + partnerLength;
        return hand.HonorLengthPoints + (combined >= 9 ? combined - 7 : 0);
    }

    private static Bid? BestResponseSuitAfterMajor(HandEvaluation hand, Bid opening, bool thirdOrFourthSeat = false)
    {
        var candidates = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 4)
            .ToList();
        var five = candidates.Where(suit => hand.SuitLength(suit) >= 5)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => ToDenomination(suit)).FirstOrDefault();
        if (hand.SuitLength(five) >= 5 && hand.HonorLengthPoints >= (thirdOrFourthSeat ? 8 : 11))
            return new(2, ToDenomination(five));
        var four = candidates.Where(suit => hand.SuitLength(suit) == 4)
            .Where(suit => !(opening.Denomination == Denomination.Spades && suit == Suit.Hearts))
            .OrderBy(suit => ToDenomination(suit)).FirstOrDefault();
        if (hand.SuitLength(four) == 4 && hand.HonorLengthPoints >= (thirdOrFourthSeat ? 6 : 6))
        {
            var bid = new Bid(1, ToDenomination(four));
            return Auction.Compare(bid, opening) > 0 ? bid : new(2, ToDenomination(four));
        }
        return null;
    }

    private static Bid? RespondToMinor(HandEvaluation hand, Bid opening)
    {
        if (hand.HonorLengthPoints < 6) return null;
        if (opening.Denomination == Denomination.Diamonds &&
            hand.SuitLength(Suit.Clubs) >= 5 && hand.HonorLengthPoints is >= 6 and <= 10 &&
            hand.IsBalanced == false)
            return new(1, Denomination.NoTrump);
        var candidates = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 4).ToList();

        var barrageSuit = candidates.Where(suit => hand.SuitLength(suit) >= 7)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
        if (hand.HighCardPoints <= 10 && hand.HonorLengthPoints < 13 && hand.SuitLength(barrageSuit) >= 7)
        {
            var barrage = new Bid(hand.SuitLength(barrageSuit) >= 8 ? 4 : 3, ToDenomination(barrageSuit));
            if (Auction.Compare(barrage, opening) > 0) return barrage;
        }

        var jumpSuit = candidates.Where(suit => hand.SuitLength(suit) >= 6)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
        if (hand.HonorLengthPoints >= 18 && hand.SuitLength(jumpSuit) >= 6)
            return new(2, ToDenomination(jumpSuit));

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
            return new(2, ToDenomination(chosen));
        }
        return RespondToMinorSupportAndNoTrump(hand, opening);
    }

    /// <summary>Ouverture mineure en 3e/4e position : seuls les changements de couleur diffèrent du silence adverse.</summary>
    private static Bid? RespondToMinorThirdSeat(HandEvaluation hand, Bid opening)
    {
        if (hand.HonorLengthPoints < 6) return null;

        var support = hand.SuitLength((Suit)opening.Denomination);
        // À saut ("rencontre") : cinq belles cartes dans la couleur annoncée, quatre cartes ou plus
        // dans la mineure d'ouverture et 8-11H — remplace les sauts habituels (barrage, unicolore fort).
        if (support >= 4 && hand.HighCardPoints is >= 8 and <= 11)
        {
            var meetingSuit = Enum.GetValues<Suit>()
                .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 5)
                .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
            if (hand.SuitLength(meetingSuit) >= 5)
            {
                var natural = new Bid(1, ToDenomination(meetingSuit));
                var cheapest = Auction.Compare(natural, opening) > 0 ? natural : new Bid(2, ToDenomination(meetingSuit));
                return new Bid(cheapest.Level + 1, ToDenomination(meetingSuit));
            }
        }

        // Sans saut : non forcing, même sélection de couleur qu'en silence adverse standard.
        var candidates = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 4).ToList();
        if (hand.HonorLengthPoints < 13 && candidates.Any(suit => suit is Suit.Hearts or Suit.Spades))
            candidates.RemoveAll(suit => suit is Suit.Clubs or Suit.Diamonds);

        if (candidates.Count > 0)
        {
            var maxLength = candidates.Max(hand.SuitLength);
            var longest = candidates.Where(suit => hand.SuitLength(suit) == maxLength);
            var chosen = maxLength >= 5
                ? longest.Max()
                : longest.OrderBy(suit => ToDenomination(suit) > opening.Denomination ? 0 : 1)
                    .ThenBy(suit => suit).First();
            var bid = new Bid(1, ToDenomination(chosen));
            if (Auction.Compare(bid, opening) > 0) return bid;
            return new(2, ToDenomination(chosen));
        }

        return RespondToMinorSupportAndNoTrump(hand, opening);
    }

    // Soutiens et enchères à Sans-Atout : inchangés, qu'il y ait silence simple ou ouverture 3e/4e position.
    private static Bid? RespondToMinorSupportAndNoTrump(HandEvaluation hand, Bid opening)
    {
        var support = hand.SuitLength((Suit)opening.Denomination);
        var minimumSupport = opening.Denomination == Denomination.Diamonds ? 4 : 5;
        if (support >= minimumSupport && hand.HighCardPoints is >= 6 and <= 8) return new(2, opening.Denomination);
        if (support >= minimumSupport && hand.HighCardPoints is >= 9 and <= 11) return new(3, opening.Denomination);
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 13 and <= 15) return new(3, Denomination.NoTrump);
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 11 and <= 12) return new(2, Denomination.NoTrump);
        if ((hand.IsBalanced || opening.Denomination == Denomination.Diamonds &&
                hand.SuitLength(Suit.Clubs) >= 5) && hand.HonorLengthPoints is >= 6 and <= 10)
            return new(1, Denomination.NoTrump);
        return null;
    }

    private static Bid? RespondToPreempt(HandEvaluation hand, Bid opening)
    {
        if (opening.Denomination == Denomination.NoTrump) return null;
        var support = hand.SuitLength((Suit)opening.Denomination);
        var partnerLength = opening.Level == 3 ? 7 : 8;
        var fitPoints = support >= 3
            ? FitPoints(hand, (Suit)opening.Denomination, partnerLength)
            : 0;
        // Loi des atouts : allonger le barrage majeur à la manche avec le fit troisième.
        if (opening.Level == 3 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            support >= 3)
            return new(4, opening.Denomination);
        if (opening.Level == 3 && hand.IsBalanced && support >= 2 && hand.HonorLengthPoints >= 8)
            return new(3, Denomination.NoTrump);
        if (opening.Level == 3 && hand.HonorLengthPoints >= 13)
        {
            var major = new[] { Suit.Hearts, Suit.Spades }
                .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 5)
                .OrderByDescending(hand.SuitLength)
                .ThenByDescending(suit => suit)
                .Select(ToDenomination)
                .Cast<Denomination?>()
                .FirstOrDefault();
            if (major.HasValue) return CheapestBid(major.Value, opening);
        }
        return support >= 3 && fitPoints >= 14 && opening.Level < 4
            ? new(Math.Min(4, opening.Level + 1), opening.Denomination) : null;
    }
}
