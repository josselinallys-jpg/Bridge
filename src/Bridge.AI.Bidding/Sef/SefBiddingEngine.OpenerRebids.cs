namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

/// <summary>Chapitre SEF : redemandes de l'ouvreur.</summary>
public sealed partial class SefBiddingEngine
{
    private static Bid? OpenerRebid(HandEvaluation hand, Bid opening, Bid response, bool thirdOrFourthSeat = false)
    {
        if (opening == new Bid(1, Denomination.NoTrump)) return OneNoTrumpRebid(hand, response);
        if (opening == new Bid(2, Denomination.NoTrump)) return TwoNoTrumpRebid(hand, response);
        if (opening == new Bid(3, Denomination.NoTrump)) return GamblingThreeNoTrumpRebid(hand, response);
        if (opening == new Bid(2, Denomination.Clubs) && response == new Bid(2, Denomination.Diamonds))
            return StrongTwoClubsRebid(hand);
        if (opening == new Bid(2, Denomination.Diamonds)) return StrongTwoDiamondsRebid(hand, response);
        if (opening.Level == 2 && opening.Denomination is Denomination.Hearts or Denomination.Spades)
            return WeakTwoRebid(hand, opening, response);
        if (opening.Level == 3 && opening.Denomination != Denomination.NoTrump)
            return ThreeLevelPreemptRebid(hand, opening, response);
        if (opening.Level == 1 && opening.Denomination is Denomination.Clubs or Denomination.Diamonds)
            return MinorOpenerRebid(hand, opening, response);
        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response.Denomination == opening.Denomination)
            return MajorRaiseRebid(hand, opening, response);
        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response == new Bid(2, Denomination.Clubs) && thirdOrFourthSeat)
            return DruryRebid(hand, opening);
        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response == new Bid(2, Denomination.NoTrump))
            return FittedTwoNoTrumpRebid(hand, opening);
        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response == new Bid(2, Denomination.NoTrump))
            return new(hand.TotalPoints <= 14 ? 3 : 4, opening.Denomination);
        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response == new Bid(3, Denomination.NoTrump) &&
            hand.SuitLength((Suit)opening.Denomination) >= 3)
            return FitPoints(hand, (Suit)opening.Denomination, 4) >= 18
                ? FirstControl(hand, opening, response)
                : new(4, opening.Denomination);
        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response.Level == 4 && response.Denomination != opening.Denomination)
            return new(4, Denomination.NoTrump);
        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades)
            return MajorOpenerRebid(hand, opening, response);

        // Barrages et 3SA gambling : l'ouvreur ne reparle pas.
        return null;
    }

    private static Bid? ThreeLevelPreemptRebid(HandEvaluation hand, Bid opening, Bid response)
    {
        if (response.Denomination is Denomination.Hearts or Denomination.Spades &&
            hand.SuitLength((Suit)response.Denomination) >= 2)
            return new Bid(4, response.Denomination);
        if (response.Denomination is Denomination.Hearts or Denomination.Spades &&
            Auction.Compare(new Bid(3, Denomination.NoTrump), response) > 0)
            return new Bid(3, Denomination.NoTrump);
        return null;
    }

    private static Bid? GamblingThreeNoTrumpRebid(HandEvaluation hand, Bid response)
    {
        if (response.Denomination == Denomination.Clubs && response.Level is 4 or 5)
        {
            if (hand.SuitLength(Suit.Clubs) >= hand.SuitLength(Suit.Diamonds)) return null;
            return new Bid(response.Level, Denomination.Diamonds);
        }

        if (response == new Bid(4, Denomination.Diamonds))
        {
            var shortSuit = Enum.GetValues<Suit>()
                .Where(suit => hand.SuitLength(suit) <= 1)
                .OrderBy(hand.SuitLength)
                .ThenBy(suit => suit)
                .Cast<Suit?>()
                .FirstOrDefault();
            return shortSuit.HasValue
                ? new Bid(4, ToDenomination(shortSuit.Value))
                : new Bid(4, Denomination.NoTrump);
        }

        return null;
    }

    private static Bid DruryRebid(HandEvaluation hand, Bid opening)
    {
        var points = hand.HonorLengthPoints;
        if (points <= 14) return new(2, opening.Denomination);
        if (points <= 17) return new(2, Denomination.Diamonds);
        if (points <= 19) return new(3, opening.Denomination);
        return new(4, opening.Denomination);
    }

    private static Bid FittedTwoNoTrumpRebid(HandEvaluation hand, Bid opening)
    {
        var otherMajor = opening.Denomination == Denomination.Hearts ? Suit.Spades : Suit.Hearts;
        if (hand.SuitLength(otherMajor) >= 4)
            return new(3, ToDenomination(otherMajor));
        if (hand.IsBalanced && hand.HighCardPoints is >= 18 and <= 19)
            return new(3, Denomination.NoTrump);
        if (hand.HighCardPoints >= 15)
            return new(4, opening.Denomination);
        return new(3, opening.Denomination);
    }

    /// <summary>Redemande après une ouverture de 1♥/1♠ sans soutien direct (SEF : redemandes de l'ouvreur).</summary>
    private static Bid? MajorOpenerRebid(HandEvaluation hand, Bid opening, Bid response)
    {
        var secondSuit = NaturalSecondSuit(hand, opening, response);
        if (secondSuit.HasValue)
            return secondSuit.Value;

        // Réponse 2♥ sur 1♠ : cinq cartes promises, soutien direct dès trois cartes non minimum.
        if (response.Level == 2 && response.Denomination == Denomination.Hearts &&
            opening.Denomination == Denomination.Spades)
        {
            var points = FitPoints(hand, Suit.Hearts, 5);
            if (hand.SuitLength(Suit.Hearts) >= 4 && points >= 17) return new(4, Denomination.Hearts);
            if (hand.SuitLength(Suit.Hearts) >= 3 && points >= 15) return new(3, Denomination.Hearts);
            // 12-14 : répétition puis soutien différé.
        }
        else if (response.Denomination is Denomination.Hearts or Denomination.Spades &&
                 hand.SuitLength((Suit)response.Denomination) >= 4)
            return new(FitPoints(hand, (Suit)response.Denomination, 4) switch
            { >= 20 => 4, >= 17 => 3, _ => 2 }, response.Denomination);
        else if (response.Denomination is Denomination.Clubs or Denomination.Diamonds &&
                 hand.SuitLength((Suit)response.Denomination) >= 4 &&
                 FitPoints(hand, (Suit)response.Denomination, 4) >= 15)
            return CheapestBid(response.Denomination, response);

        if (hand.IsBalanced)
        {
            if (hand.HighCardPoints is >= 18 and <= 19)
                return new(response.Level == 1 ? 2 : 3, Denomination.NoTrump);
            if (hand.HighCardPoints is >= 15 and <= 17 && response.Level == 2)
                return new(2, Denomination.NoTrump);
            if (hand.HighCardPoints <= 14)
            {
                if (response.Denomination == Denomination.NoTrump) return null;
                if (response.Level == 1) return new(1, Denomination.NoTrump);
            }
        }
        if (hand.SuitLength((Suit)opening.Denomination) >= 6)
            return RepeatOpening(hand, opening, response);
        return CheapestBid(opening.Denomination, response); // répétition de la cinquième faute de mieux
    }

    private static Bid? NaturalSecondSuit(HandEvaluation hand, Bid opening, Bid response)
    {
        var candidates = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination &&
                ToDenomination(suit) != response.Denomination && hand.SuitLength(suit) >= 4)
            .OrderBy(suit => ToDenomination(suit))
            .ToList();
        if (candidates.Count == 0) return null;

        var chosen = candidates[0];
        var chosenDenomination = ToDenomination(chosen);
        var repeat = new Bid(2, opening.Denomination);
        var economic = response.Level == 2 &&
            Auction.Compare(new Bid(2, chosenDenomination), repeat) < 0;
        var jumpBicolour = hand.SuitLength(opening.Denomination is Denomination.Hearts or Denomination.Spades
            ? (Suit)opening.Denomination : chosen) >= 5 &&
            hand.SuitLength(chosen) >= 5;
        var jumpThreshold = response.Level == 1 ? 20 : 17;
        var jumpAllowed = hand.HonorLengthPoints is >= 17 and <= 23 &&
            hand.SuitLength(opening.Denomination is Denomination.Hearts or Denomination.Spades
                ? (Suit)opening.Denomination : chosen) >= 5 &&
            hand.SuitLength(chosen) >= 5 && hand.HonorLengthPoints >= jumpThreshold;
        var strongBicolour = response.Level == 1
            ? hand.HonorLengthPoints is >= 18 and <= 23
            : hand.HonorLengthPoints >= 17;

        if (jumpBicolour && jumpAllowed)
            return new(CheapestBid(chosenDenomination, response).Level + 1, chosenDenomination);
        if (economic || strongBicolour)
            return CheapestBid(chosenDenomination, response);
        return null;
    }

    private static Bid RepeatOpening(HandEvaluation hand, Bid opening, Bid response)
    {
        var level = response.Level == 1 && opening.Denomination is Denomination.Clubs or Denomination.Diamonds
            ? 2
            : opening.Level + (hand.HonorLengthPoints >= 17 ? 2 : 1);
        return new(level, opening.Denomination);
    }

    /// <summary>Redemande de l'ouvreur d'un 2 faible (SEF : barrages).</summary>
    private static Bid? WeakTwoRebid(HandEvaluation hand, Bid opening, Bid response)
    {
        if (response == new Bid(2, Denomination.NoTrump))
        {
            // Maximum (8-10H) : gros honneur annexe, sinon 3SA couleur pleine ; minimum : répétition.
            if (hand.HighCardPoints >= 8)
            {
                var side = Enum.GetValues<Suit>()
                    .Where(suit => ToDenomination(suit) != opening.Denomination &&
                        hand.Cards.Any(card => card.Suit == suit && card.Rank >= Rank.King))
                    .Cast<Suit?>().FirstOrDefault();
                if (side.HasValue) return new(3, ToDenomination(side.Value));
                var shortSuit = Enum.GetValues<Suit>()
                    .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) <= 1)
                    .OrderBy(hand.SuitLength)
                    .ThenBy(suit => suit)
                    .Cast<Suit?>()
                    .FirstOrDefault();
                return shortSuit.HasValue
                    ? new(4, ToDenomination(shortSuit.Value))
                    : new(3, Denomination.NoTrump);
            }
            return new(3, opening.Denomination);
        }
        // Changement de couleur forcing : soutien avec trois cartes, sinon répétition.
        if (response.Denomination != opening.Denomination && response.Denomination != Denomination.NoTrump)
            return hand.SuitLength((Suit)response.Denomination) >= 3
                ? CheapestBid(response.Denomination, response)
                : CheapestBid(opening.Denomination, response);
        // Soutiens barrage : l'ouvreur passe.
        return null;
    }

    /// <summary>Redemande après soutien direct d'une ouverture majeure (SEF : la majeure cinquième).</summary>
    private static Bid? MajorRaiseRebid(HandEvaluation hand, Bid opening, Bid response)
    {
        var points = FitPoints(hand, (Suit)opening.Denomination, 3);
        // Répondant 6-10 HLD (2M) : passe 14-16, enchère d'essai 17-19, manche 20+.
        if (response.Level == 2)
        {
            if (points >= 20) return new(4, opening.Denomination);
            if (points < 17) return null;
            var trialSuit = Enum.GetValues<Suit>()
                .Where(suit => suit != (Suit)opening.Denomination && hand.SuitLength(suit) >= 3)
                .OrderBy(suit => ToDenomination(suit))
                .Select(ToDenomination)
                .FirstOrDefault();
            return trialSuit != default ? new(3, trialSuit) : new(2, Denomination.NoTrump);
        }
        // Répondant 11-12 HLD (3M) : passe ≤14, manche 15+, puis essai de chelem.
        if (response.Level == 3)
            return points switch
            {
                >= 21 => new(5, opening.Denomination),
                >= 15 => new(4, opening.Denomination),
                _ => null
            };
        // Répondant 13-15 HLD (4M) : passe 14-17, essai de chelem 18+.
        if (response.Level == 4)
            return points switch
            {
                >= 18 => new(5, opening.Denomination),
                _ => null
            };
        return null;
    }

    private static Bid FirstControl(HandEvaluation hand, Bid opening, Bid over)
    {
        var control = Enum.GetValues<Suit>()
            .Where(suit => suit != (Suit)opening.Denomination &&
                hand.Cards.Any(card => card.Suit == suit && card.Rank is Rank.Ace or Rank.King))
            .OrderBy(suit => ToDenomination(suit))
            .Select(ToDenomination)
            .Select(denomination => new Bid(4, denomination))
            .FirstOrDefault(bid => Auction.Compare(bid, over) > 0);
        return control == default ? new(4, opening.Denomination) : control;
    }

    /// <summary>Redemande après une ouverture de 1♣/1♦ (SEF : ouvertures mineures).</summary>
    private static Bid? MinorOpenerRebid(HandEvaluation hand, Bid opening, Bid response)
    {
        if (response == new Bid(1, Denomination.NoTrump) &&
            hand.SuitLength((Suit)opening.Denomination) >= 6)
            return hand.HonorLengthPoints >= 17
                ? new(3, opening.Denomination)
                : hand.HonorLengthPoints is >= 13 and <= 16
                    ? new(2, opening.Denomination)
                    : null;

        // Soutien mineur : sous 3SA, une garde dirige vers 3SA ; au-dessus, elle montre un contrôle.
        if (response.Denomination == opening.Denomination && response.Level == 2)
        {
            if (hand.HonorLengthPoints < 17) return null;
            foreach (var suit in new[] { Suit.Diamonds, Suit.Hearts, Suit.Spades })
                if (ToDenomination(suit) != opening.Denomination && HasGuard(hand, suit))
                    return CheapestBid(ToDenomination(suit), response);
            return new(3, opening.Denomination);
        }

        var secondSuit = NaturalSecondSuit(hand, opening, response);
        if (secondSuit.HasValue)
            return secondSuit.Value;

        // Une majeure répondue au palier de 1 promet quatre cartes chez l'ouvreur.
        if (response.Denomination is Denomination.Hearts or Denomination.Spades &&
            hand.SuitLength((Suit)response.Denomination) >= 4)
        {
            var fitPoints = FitPoints(hand, (Suit)response.Denomination, 4);
            var level = fitPoints >= 20
                ? 4
                : fitPoints >= 17 && !hand.IsBalanced
                    ? 3
                    : 2;
            return new(level, response.Denomination);
        }

        // Sans fit : majeure quatrième de l'ouvreur nommable au palier de 1 (12-19HL, non forcing).
        if (response.Level == 1)
            foreach (var major in new[] { Suit.Hearts, Suit.Spades })
                if (hand.SuitLength(major) >= 4 && ToDenomination(major) > response.Denomination)
                    return new(1, ToDenomination(major));

        // Jeu régulier sans majeure nommable : 1SA 12-14H, 2SA 18-19H.
        if (hand.IsBalanced)
        {
            if (hand.HighCardPoints is >= 18 and <= 19)
                return new(response.Level == 1 ? 2 : 3, Denomination.NoTrump);
            if (hand.HighCardPoints <= 14 && response.Level == 1)
                return response.Denomination == Denomination.NoTrump ? null : new(1, Denomination.NoTrump);
        }

        if (response.Denomination is Denomination.Clubs or Denomination.Diamonds &&
            hand.SuitLength((Suit)response.Denomination) >= 4)
            return new(FitPoints(hand, (Suit)response.Denomination, 5) switch
            { >= 20 => 4, >= 17 => 3, _ => 2 }, response.Denomination);
        if (hand.SuitLength((Suit)opening.Denomination) >= 5 &&
            (response.Level == 2 || hand.HonorLengthPoints is >= 13 and <= 17))
            return RepeatOpening(hand, opening, response);
        return null;
    }

    private static Bid? OneNoTrumpRebid(HandEvaluation hand, Bid response) => response switch
    {
        { Level: 2, Denomination: Denomination.Clubs } => StaymanAnswer(hand, 2),
        { Level: 2, Denomination: Denomination.Diamonds } => new(2, Denomination.Hearts),
        { Level: 2, Denomination: Denomination.Hearts } => new(2, Denomination.Spades),
        { Level: 2, Denomination: Denomination.Spades } => new(3, Denomination.Clubs),
        { Level: 3, Denomination: Denomination.Clubs } => new(3, Denomination.Diamonds),
        { Level: 2, Denomination: Denomination.NoTrump } when hand.HighCardPoints >= 17 => new(3, Denomination.NoTrump),
        // 4♦ conventionnel (bicolore majeur 5-5 de manche) : l'ouvreur choisit sa majeure.
        { Level: 4, Denomination: Denomination.Diamonds } =>
            hand.SuitLength(Suit.Spades) > hand.SuitLength(Suit.Hearts)
                ? new(4, Denomination.Spades) : new(4, Denomination.Hearts),
        // 4SA quantitatif : l'ouvreur conclut à 6SA avec son maximum de 17H.
        { Level: 4, Denomination: Denomination.NoTrump } when hand.HighCardPoints >= 17 => new(6, Denomination.NoTrump),
        _ => null
    };

    private static Bid? TwoNoTrumpRebid(HandEvaluation hand, Bid response)
    {
        if (response == new Bid(4, Denomination.Clubs)) return new(4, Denomination.Diamonds);
        if (response == new Bid(4, Denomination.Diamonds))
            return hand.SuitLength(Suit.Hearts) >= hand.SuitLength(Suit.Spades)
                ? new(4, Denomination.Hearts) : new(4, Denomination.Spades);

        return response switch
        {
            { Level: 3, Denomination: Denomination.Clubs } => StaymanAnswer(hand, 3),
            { Level: 3, Denomination: Denomination.Diamonds } =>
                hand.SuitLength(Suit.Hearts) >= 3 ? new(3, Denomination.Hearts) : new(3, Denomination.NoTrump),
            { Level: 3, Denomination: Denomination.Hearts } =>
                hand.SuitLength(Suit.Spades) >= 3 ? new(3, Denomination.Spades) : new(3, Denomination.NoTrump),
            { Level: 3, Denomination: Denomination.Spades } => new(4, Denomination.Clubs),
            { Level: 4, Denomination: Denomination.NoTrump } when hand.HonorLengthPoints >= 21 =>
                new(5, Denomination.NoTrump),
            _ => null
        };
    }

    private static Bid StaymanAnswer(HandEvaluation hand, int level)
    {
        var hearts = hand.SuitLength(Suit.Hearts) == 4;
        var spades = hand.SuitLength(Suit.Spades) == 4;
        if (hearts && spades) return new(level, Denomination.NoTrump);
        if (spades) return new(level, Denomination.Spades);
        if (hearts) return new(level, Denomination.Hearts);
        return new(level, Denomination.Diamonds);
    }

    /// <summary>Deuxième redemande de l'ouvreur de 1SA après un Stayman (SEF : fiche 1SA).</summary>
    private static Bid? StaymanContinuation(HandEvaluation hand, IReadOnlyList<Bid> bids)
    {
        var answer = bids[2];
        var rebid = bids[3];
        if (answer == new Bid(2, Denomination.NoTrump))
        {
            if (rebid is { Level: 3, Denomination: Denomination.Clubs or Denomination.Diamonds })
            {
                var major = rebid.Denomination == Denomination.Clubs ? Suit.Hearts : Suit.Spades;
                if (hand.SuitLength(major) < 3) return new(3, Denomination.NoTrump);
                return hand.HighCardPoints >= 17
                    ? new(4, ToDenomination(major))
                    : new(3, ToDenomination(major));
            }
            if (rebid == new Bid(4, Denomination.Clubs))
                return hand.SuitLength(Suit.Hearts) >= 3
                    ? new(4, Denomination.Hearts) : new(3, Denomination.NoTrump);
            if (rebid == new Bid(4, Denomination.Diamonds))
                return hand.SuitLength(Suit.Spades) >= 3
                    ? new(4, Denomination.Spades) : new(3, Denomination.NoTrump);
            if (rebid is { Level: 3, Denomination: Denomination.Hearts or Denomination.Spades })
                return new(4, rebid.Denomination);
        }

        if (answer is { Level: 2, Denomination: Denomination.Hearts or Denomination.Spades })
        {
            // 4C/4D (et 4H sur 2S) sont des splinters; l'autre majeure à 3 est la convention 2012.
            if (rebid.Level == 4 && rebid.Denomination != Denomination.NoTrump)
                return new(4, answer.Denomination);
            if (rebid.Level == 3 &&
                rebid.Denomination is Denomination.Hearts or Denomination.Spades &&
                rebid.Denomination != answer.Denomination)
                return new(4, answer.Denomination);
        }

        // Invitations à SA : l'ouvreur accepte avec un maximum (17H).
        if (rebid == new Bid(2, Denomination.NoTrump))
            return hand.HighCardPoints >= 17 ? new(3, Denomination.NoTrump) : null;
        if (rebid == new Bid(4, Denomination.NoTrump))
            return hand.HighCardPoints >= 17 ? new(6, Denomination.NoTrump) : null;
        if (rebid.Denomination is Denomination.Hearts or Denomination.Spades)
        {
            // Soutien invitatif 3M après réponse 2M.
            if (answer.Denomination == rebid.Denomination && rebid.Level == 3)
                return hand.HighCardPoints >= 17 ? new(4, rebid.Denomination) : null;
            if (answer == new Bid(2, Denomination.Diamonds))
            {
                // Chassé-croisé : saut dans la majeure quatrième, la cinquième est l'autre majeure.
                if (rebid.Level == 3)
                {
                    var other = rebid.Denomination == Denomination.Hearts
                        ? Denomination.Spades : Denomination.Hearts;
                    return hand.SuitLength((Suit)other) >= 3
                        ? new(4, other) : new(3, Denomination.NoTrump);
                }
                // Majeure cinquième au niveau 2 (8-9H) : passe fitté, sinon retour à 2SA.
                if (rebid.Level == 2)
                    return hand.SuitLength((Suit)rebid.Denomination) >= 3
                        ? null : new(2, Denomination.NoTrump);
            }
        }
        return null;
    }

    /// <summary>Choix de la manche majeure quand le répondant a montré un bicolore 5-5.</summary>
    private static Bid? ChooseMajorGame(HandEvaluation hand, Bid heartsBid)
    {
        if (hand.SuitLength(Suit.Spades) > hand.SuitLength(Suit.Hearts))
            return new(4, Denomination.Spades);
        return heartsBid.Level >= 4 ? null : new(4, Denomination.Hearts);
    }

    private static Bid StrongTwoClubsRebid(HandEvaluation hand)
    {
        var hearts = hand.SuitLength(Suit.Hearts);
        var spades = hand.SuitLength(Suit.Spades);
        if (LosingTricks(hand) <= 3)
        {
            if (hearts == 6 && spades == 5) return new(4, Denomination.Clubs);
            if (spades == 6 && hearts == 5) return new(4, Denomination.Diamonds);
            if (hearts >= 5 && spades >= 5) return new(3, Denomination.NoTrump);
        }

        if (hand.IsBalanced && hand.HonorLengthPoints is >= 22 and <= 23)
            return new(2, Denomination.NoTrump);

        var suit = hand.LongestSuit;
        if (hand.SuitLength(suit) >= 7)
            return new(3, ToDenomination(suit));
        if (suit is Suit.Hearts or Suit.Spades && hand.SuitLength(suit) >= 5 &&
            hand.HonorLengthPoints is >= 21 and <= 23)
            return new(2, ToDenomination(suit));
        return new(suit is Suit.Hearts or Suit.Spades ? 2 : 3, ToDenomination(suit));
    }

    private static int LosingTricks(HandEvaluation hand) => Enum.GetValues<Suit>().Sum(suit =>
    {
        var length = hand.SuitLength(suit);
        var losers = 0;
        if (length >= 1 && !hand.Cards.Contains(new Card(suit, Rank.Ace))) losers++;
        if (length >= 2 && !hand.Cards.Contains(new Card(suit, Rank.King))) losers++;
        if (length >= 3 && !hand.Cards.Contains(new Card(suit, Rank.Queen))) losers++;
        return losers;
    });

    private static Bid? StrongTwoDiamondsRebid(HandEvaluation hand, Bid response)
    {
        if (response.Level >= 4 || response == new Bid(3, Denomination.NoTrump)) return null;
        if (hand.IsBalanced)
            return CheapestBid(Denomination.NoTrump, response);

        var suit = hand.LongestSuit;
        var length = hand.SuitLength(suit);
        var denomination = ToDenomination(suit);
        var natural = CheapestBid(denomination, response);
        if (length >= 6)
            return natural.Level <= 2 ? new Bid(3, denomination) : natural;
        if (length == 5 && (natural.Level == 2 || LosingTricks(hand) <= 4))
            return natural.Level <= 3 ? natural : new Bid(3, denomination);
        return CheapestBid(Denomination.NoTrump, response);
    }

    private static int NoTrumpOpenerMinimum(Bid opening) => opening switch
    {
        { Level: 1, Denomination: Denomination.NoTrump } => 15,
        { Level: 2, Denomination: Denomination.NoTrump } => 20,
        { Level: 2, Denomination: Denomination.Clubs } => 22,
        _ => 24
    };
}
