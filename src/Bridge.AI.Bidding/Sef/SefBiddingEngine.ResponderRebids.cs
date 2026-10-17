namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

/// <summary>Chapitre SEF : deuxième enchère du répondant (dont suites de Stayman et Texas).</summary>
public sealed partial class SefBiddingEngine
{
    private static Bid? ResponderRebid(HandEvaluation hand, Bid opening, Bid response, Bid openerRebid,
        bool thirdOrFourthSeat = false)
    {
        if (opening.Denomination == Denomination.NoTrump && opening.Level is 1 or 2)
        {
            var staymanLevel = opening.Level == 1 ? 2 : 3;
            if (response == new Bid(staymanLevel, Denomination.Clubs))
                return AfterStayman(hand, openerRebid, staymanLevel, NoTrumpOpenerMinimum(opening));
            if (opening.Level == 2 && openerRebid == new Bid(3, Denomination.NoTrump) &&
                TexasTarget(response, 2).HasValue)
                return null;
            return AfterTexas(hand, response, opening.Level);
        }
        // SEF ouvertures fortes : développements après 2♣ fort indéterminé et 2♦ forcing de manche.
        if (opening == new Bid(2, Denomination.Clubs) || opening == new Bid(2, Denomination.Diamonds))
        {
            var gameForcing = opening.Denomination == Denomination.Diamonds;
            if (openerRebid.Denomination == Denomination.NoTrump)
            {
                var bid = RespondToTwoNoTrump(hand, gameForcing);
                // 2♦ est forcing de manche : jamais de passe avant la manche.
                if (bid is null && gameForcing) return new Bid(3, Denomination.NoTrump);
                return bid;
            }
            var trump = (Suit)openerRebid.Denomination;
            var isMajor = openerRebid.Denomination is Denomination.Hearts or Denomination.Spades;
            if (!gameForcing && opening == new Bid(2, Denomination.Clubs) &&
                response == new Bid(2, Denomination.Diamonds) && isMajor && openerRebid.Level == 2)
            {
                var support = hand.SuitLength(trump);
                var shortSuit = Enum.GetValues<Suit>()
                    .Where(suit => suit != trump && hand.SuitLength(suit) <= 1)
                    .OrderBy(hand.SuitLength)
                    .Select(suit => (Suit?)suit)
                    .FirstOrDefault();
                if (support >= 4 && shortSuit.HasValue)
                {
                    var splinter = CheapestBid(ToDenomination(shortSuit.Value), openerRebid);
                    return new Bid(splinter.Level + 1, splinter.Denomination);
                }
                if (support >= 3) return new Bid(3, openerRebid.Denomination);
                var positiveSuit = Enum.GetValues<Suit>()
                    .Where(suit => suit != trump && hand.SuitLength(suit) >= 5 && hand.HonorLengthPoints >= 7)
                    .OrderByDescending(hand.SuitLength)
                    .ThenByDescending(suit => suit)
                    .Select(suit => (Denomination?)ToDenomination(suit))
                    .FirstOrDefault();
                if (positiveSuit.HasValue)
                    return new Bid(3, positiveSuit.Value);
                if (hand.HighCardPoints >= 5) return new Bid(2, Denomination.NoTrump);
                return new Bid(2, Denomination.NoTrump);
            }

            if (!gameForcing && opening == new Bid(2, Denomination.Clubs) &&
                response == new Bid(2, Denomination.Diamonds) && openerRebid.Level == 3 &&
                openerRebid.Denomination is Denomination.Clubs or Denomination.Diamonds)
            {
                if (hand.SuitLength(trump) >= 3)
                    return new Bid(4, openerRebid.Denomination);
                var suitForce = Enum.GetValues<Suit>()
                    .Where(suit => suit != trump && hand.Cards.Any(card => card.Suit == suit &&
                        card.Rank is Rank.Ace or Rank.King or Rank.Queen))
                    .OrderByDescending(suit => hand.Cards.Where(card => card.Suit == suit)
                        .Sum(card => card.Rank is Rank.Ace or Rank.King or Rank.Queen ? 1 : 0))
                    .ThenByDescending(suit => suit)
                    .Select(suit => (Denomination?)ToDenomination(suit))
                    .FirstOrDefault();
                if (suitForce.HasValue) return new Bid(3, suitForce.Value);
            }

            if (!gameForcing && opening == new Bid(2, Denomination.Clubs) &&
                response == new Bid(2, Denomination.Diamonds) && openerRebid.Level == 3 && isMajor)
            {
                var support = hand.SuitLength(trump);
                var controlSuit = Enum.GetValues<Suit>()
                    .Where(suit => suit != trump && hand.Cards.Any(card => card.Suit == suit &&
                        card.Rank is Rank.Ace or Rank.King))
                    .OrderBy(suit => ToDenomination(suit))
                    .Select(suit => (Denomination?)ToDenomination(suit))
                    .FirstOrDefault();
                if (support >= 3 && controlSuit.HasValue)
                    return CheapestBid(controlSuit.Value, openerRebid);
                if (support >= 3) return new Bid(4, openerRebid.Denomination);
            }

            if (gameForcing)
            {
                if (isMajor)
                {
                    var support = hand.SuitLength(trump);
                    var shortSuit = Enum.GetValues<Suit>()
                        .Where(suit => suit != trump && hand.SuitLength(suit) <= 1)
                        .OrderBy(hand.SuitLength)
                        .Select(suit => (Suit?)suit)
                        .FirstOrDefault();
                    if (support >= 4 && shortSuit.HasValue)
                    {
                        var splinter = CheapestBid(ToDenomination(shortSuit.Value), openerRebid);
                        splinter = new Bid(splinter.Level + 1, splinter.Denomination);
                        if (splinter.Level <= 7) return splinter;
                    }
                    if (support >= 2)
                        return openerRebid.Level == 2 && hand.HighCardPoints >= 8
                            ? new Bid(3, openerRebid.Denomination)
                            : support >= 3 ? new Bid(4, openerRebid.Denomination) :
                                CheapestBid(Denomination.NoTrump, openerRebid);
                }

                var positiveSuit = Enum.GetValues<Suit>()
                    .Where(suit => hand.SuitLength(suit) >= 5 && ToDenomination(suit) != openerRebid.Denomination)
                    .OrderByDescending(hand.SuitLength)
                    .ThenByDescending(suit => suit)
                    .Select(suit => (Denomination?)ToDenomination(suit))
                    .FirstOrDefault();
                if (positiveSuit.HasValue && hand.HonorLengthPoints >= 8)
                {
                    return CheapestBid(positiveSuit.Value, openerRebid);
                }

                if (openerRebid.Level == 2 && hand.IsBalanced)
                    return CheapestBid(Denomination.NoTrump, openerRebid);
                var five = new[] { Suit.Spades, Suit.Hearts }
                    .Where(major => hand.SuitLength(major) >= 5).Cast<Suit?>().FirstOrDefault();
                if (five.HasValue) return CheapestBid(ToDenomination(five.Value), openerRebid);
                return openerRebid.Level < 3
                    ? CheapestBid(Denomination.NoTrump, openerRebid)
                    : new Bid(3, Denomination.NoTrump);
            }
            // 2♣ : réponses forcing sur la redemande descriptive de l'ouvreur.
            if (hand.SuitLength(trump) >= 3)
            {
                var shortSuit = Enum.GetValues<Suit>()
                    .Where(suit => suit != trump && hand.SuitLength(suit) <= 1)
                    .OrderBy(hand.SuitLength)
                    .Select(suit => (Suit?)suit)
                    .FirstOrDefault();
                if (hand.SuitLength(trump) >= 4 && shortSuit.HasValue)
                {
                    var splinter = CheapestBid(ToDenomination(shortSuit.Value), openerRebid);
                    splinter = new Bid(splinter.Level + 1, splinter.Denomination);
                    if (splinter.Level <= 7) return splinter;
                }
                if (isMajor && openerRebid.Level == 2 && hand.HighCardPoints <= 4 &&
                    hand.SuitLength(trump) >= 4)
                    return new Bid(4, openerRebid.Denomination);
                if (isMajor)
                    return new Bid(3, openerRebid.Denomination);
                if (hand.HighCardPoints >= 6) return new Bid(5, openerRebid.Denomination);
                return CheapestBid(openerRebid.Denomination, openerRebid);
            }

            var forcingSuit = Enum.GetValues<Suit>()
                .Where(suit => hand.SuitLength(suit) >= 5 && ToDenomination(suit) != openerRebid.Denomination)
                .OrderByDescending(hand.SuitLength)
                .ThenByDescending(suit => suit)
                .Select(suit => (Denomination?)ToDenomination(suit))
                .FirstOrDefault();
            if (forcingSuit.HasValue && hand.HonorLengthPoints >= 7)
                return CheapestBid(forcingSuit.Value, openerRebid);
            if (hand.HighCardPoints >= 5)
                return CheapestBid(Denomination.NoTrump, openerRebid);

            var fiveMajor = new[] { Suit.Spades, Suit.Hearts }
                .Where(major => hand.SuitLength(major) >= 5).Cast<Suit?>().FirstOrDefault();
            if (fiveMajor.HasValue) return CheapestBid(ToDenomination(fiveMajor.Value), openerRebid);
            if (hand.HighCardPoints >= 5) return new Bid(3, Denomination.NoTrump);
            return CheapestBid(Denomination.NoTrump, openerRebid);
        }

        // SEF barrages : suite du relais 2SA sur un 2 faible.
        if (opening.Level == 2 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response == new Bid(2, Denomination.NoTrump))
        {
            if (openerRebid == new Bid(3, opening.Denomination))
            {
                var strength = hand.SuitLength((Suit)opening.Denomination) >= 3
                    ? FitPoints(hand, (Suit)opening.Denomination, 6)
                    : hand.HonorLengthPoints;
                return strength >= 17 ? new Bid(4, opening.Denomination) : null; // minimum
            }
            if (openerRebid == new Bid(3, Denomination.NoTrump)) return null;
            // Maximum avec honneur annexe : manche dans la majeure, ou 3SA sans fit.
            return hand.SuitLength((Suit)opening.Denomination) >= 2
                ? new Bid(4, opening.Denomination)
                : new Bid(3, Denomination.NoTrump);
        }

        if (opening.Level == 1 && opening.Denomination is Denomination.Clubs or Denomination.Diamonds &&
            response.Level == 1 && response.Denomination is Denomination.Hearts or Denomination.Spades &&
            openerRebid == new Bid(1, Denomination.NoTrump) &&
            hand.SuitLength((Suit)response.Denomination) >= 5 && hand.HonorLengthPoints >= 11)
            return new Bid(2, Denomination.Clubs);

        // SEF redemandes : après soutien mineur simple et annonce de garde, 3SA avec les arrêts restants.
        if (opening.Level == 1 && opening.Denomination is Denomination.Clubs or Denomination.Diamonds &&
            response.Denomination == opening.Denomination && response.Level == 2 &&
            openerRebid.Denomination != Denomination.NoTrump &&
            openerRebid.Denomination != opening.Denomination)
        {
            var missing = Enum.GetValues<Suit>().Where(suit =>
                ToDenomination(suit) != opening.Denomination &&
                ToDenomination(suit) != openerRebid.Denomination);
            return missing.All(suit => HasGuard(hand, suit))
                ? new Bid(3, Denomination.NoTrump)
                : CheapestBid(opening.Denomination, openerRebid); // retour à la mineure, non forcing
        }

        if (opening.Level == 1 && opening.Denomination is Denomination.Clubs or Denomination.Diamonds &&
            response.Level == 1 && response.Denomination is Denomination.Hearts or Denomination.Spades)
        {
            if (openerRebid == new Bid(2, opening.Denomination) && hand.HonorLengthPoints >= 11)
                return ThirdSuitForcing(hand, opening, response, openerRebid);

            if (openerRebid == new Bid(1, Denomination.NoTrump) &&
                hand.SuitLength((Suit)response.Denomination) >= 5 && hand.HonorLengthPoints < 11)
                return PingPong(response, openerRebid);

            if (openerRebid.Denomination != response.Denomination &&
                hand.SuitLength((Suit)response.Denomination) >= 6)
                return CheapestBid(response.Denomination, openerRebid);
        }

        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response.Level == 2 && response.Denomination != opening.Denomination &&
            openerRebid == new Bid(2, opening.Denomination) &&
            hand.SuitLength((Suit)opening.Denomination) >= 3 && hand.HonorLengthPoints >= 11)
            return new Bid(3, opening.Denomination);

        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response == new Bid(2, Denomination.Clubs) && openerRebid == new Bid(2, Denomination.Diamonds) &&
            thirdOrFourthSeat)
        {
            if (hand.SuitLength((Suit)opening.Denomination) >= 4)
                return new Bid(3, opening.Denomination);
            if (hand.SuitLength((Suit)opening.Denomination) >= 3)
                return new Bid(2, opening.Denomination);
            return new Bid(2, Denomination.NoTrump);
        }

        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response == new Bid(2, Denomination.NoTrump) &&
            openerRebid.Denomination == opening.Denomination)
            return null;

        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response == new Bid(2, Denomination.NoTrump))
        {
            if (openerRebid.Denomination == opening.Denomination)
                return null;
            if (openerRebid.Denomination is Denomination.Clubs or Denomination.Diamonds)
                return new(4, openerRebid.Denomination);
            if (openerRebid.Denomination is Denomination.Hearts or Denomination.Spades)
                return openerRebid.Denomination == opening.Denomination
                    ? null
                    : CheapestBid(openerRebid.Denomination, openerRebid);
            return null;
        }

        // SEF ouvertures mineures : développements après une réponse de 1M.
        if (opening.Level == 1 && opening.Denomination is Denomination.Clubs or Denomination.Diamonds &&
            response.Level == 1 && response.Denomination is Denomination.Hearts or Denomination.Spades)
        {
            var fitPoints = FitPoints(hand, (Suit)response.Denomination, 4);
            // Soutien de l'ouvreur : sur 2M passe/3M essai/4M ; sur 3M passe/4M.
            if (openerRebid.Denomination == response.Denomination)
            {
                if (fitPoints >= 17) return new Bid(4, Denomination.NoTrump);
                if (openerRebid.Level == 2)
                    return fitPoints switch
                    {
                        >= 13 => new Bid(4, response.Denomination),
                        >= 11 => new Bid(3, response.Denomination),
                        _ => null
                    };
                if (openerRebid.Level == 3)
                    return fitPoints >= 9 ? new Bid(4, response.Denomination) : null;
                return null;
            }
            // Majeure quatrième de l'ouvreur au palier de 1 : soutien avec quatre atouts.
            if (openerRebid.Level == 1 && openerRebid.Denomination is Denomination.Hearts or Denomination.Spades &&
                hand.SuitLength((Suit)openerRebid.Denomination) >= 4)
                return FitPoints(hand, (Suit)openerRebid.Denomination, 4) switch
                {
                    >= 13 => new Bid(4, openerRebid.Denomination),
                    >= 11 => new Bid(3, openerRebid.Denomination),
                    _ => new Bid(2, openerRebid.Denomination)
                };
        }

        // SEF majeure cinquième : après soutien direct, réponses aux enchères d'essai de l'ouvreur.
        if (opening.Level == 1 && opening.Denomination is Denomination.Hearts or Denomination.Spades &&
            response.Denomination == opening.Denomination)
        {
            var points = FitPoints(hand, (Suit)opening.Denomination, 5);
            if (points >= 17) return new Bid(4, Denomination.NoTrump);
            // Enchère d'essai 3M après 2M : manche avec 9-10 HLD.
            if (response.Level == 2 && openerRebid == new Bid(3, opening.Denomination))
                return points >= 9 ? new Bid(4, opening.Denomination) : null;
            // Essai de chelem 5M après 4M : chelem avec 14-15 HLD.
            if (openerRebid == new Bid(5, opening.Denomination))
                return points >= 14 ? new Bid(6, opening.Denomination) : null;
            return null;
        }

        var naturalSequence = new[] { opening.Denomination, response.Denomination, openerRebid.Denomination };
        if (!naturalSequence.Contains(Denomination.NoTrump) && naturalSequence.Distinct().Count() == 3 &&
            hand.HonorLengthPoints >= 11)
        {
            var fourthSuit = Enum.GetValues<Denomination>()
                .First(denomination => denomination != Denomination.NoTrump && !naturalSequence.Contains(denomination));
            return CheapestBid(fourthSuit, openerRebid);
        }

        var agreedTrump = FindAgreedTrump(new[] { opening, response, openerRebid });
        if (agreedTrump.HasValue && hand.TotalPoints >= 21)
            return new Bid(4, Denomination.NoTrump);

        if (agreedTrump.HasValue && hand.TotalPoints >= 13 &&
            auctionLegalMajorGame(agreedTrump.Value, openerRebid))
            return new Bid(4, agreedTrump.Value);

        var combinedEstimate = hand.HighCardPoints + 13;
        if (openerRebid.Denomination != Denomination.NoTrump &&
            hand.SuitLength((Suit)openerRebid.Denomination) >= 3 && combinedEstimate >= 25)
            return new(4, openerRebid.Denomination);
        return combinedEstimate >= 25 ? new(3, Denomination.NoTrump) : null;
    }

    private static bool auctionLegalMajorGame(Denomination trump, Bid over) =>
        trump is Denomination.Hearts or Denomination.Spades &&
        Auction.Compare(new Bid(4, trump), over) > 0;

    private static Bid ThirdSuitForcing(HandEvaluation hand, Bid opening, Bid response, Bid openerRebid)
    {
        var thirdSuit = Enum.GetValues<Denomination>()
            .Where(denomination => denomination != Denomination.NoTrump &&
                denomination != opening.Denomination && denomination != response.Denomination)
            .Select(denomination => new Bid(2, denomination))
            .Where(bid => Auction.Compare(bid, openerRebid) > 0)
            .OrderBy(bid => bid.Denomination)
            .First();
        return thirdSuit;
    }

    private static Bid PingPong(Bid response, Bid openerRebid)
    {
        var otherMajor = response.Denomination == Denomination.Hearts
            ? Denomination.Spades : Denomination.Hearts;
        return CheapestBid(otherMajor, openerRebid);
    }

    private static Bid AfterStayman(HandEvaluation hand, Bid answer, int level, int openerMinimum)
    {
        var h = hand.HighCardPoints;
        if (answer == new Bid(2, Denomination.NoTrump))
        {
            if (hand.SuitLength(Suit.Hearts) >= 5) return new(level + 1, Denomination.Clubs);
            if (hand.SuitLength(Suit.Spades) >= 5) return new(level + 1, Denomination.Diamonds);
            if (hand.SuitLength(Suit.Hearts) >= 4 && hand.HonorLengthPoints >= 13)
                return new(level + 1, Denomination.Hearts);
            if (hand.SuitLength(Suit.Spades) >= 4 && hand.HonorLengthPoints >= 13)
                return new(level + 1, Denomination.Spades);
        }

        if (answer is { Level: 2, Denomination: Denomination.Hearts or Denomination.Spades } &&
            hand.SuitLength((Suit)answer.Denomination) >= 4 &&
            FitPoints(hand, (Suit)answer.Denomination, 4) >= 15)
        {
            Suit? shortSuit = Enum.GetValues<Suit>()
                .Where(suit => ToDenomination(suit) != answer.Denomination && hand.SuitLength(suit) <= 1)
                .OrderBy(hand.SuitLength)
                .Cast<Suit?>()
                .FirstOrDefault();
            if (shortSuit.HasValue) return new(4, ToDenomination(shortSuit.Value));
            var otherMajor = answer.Denomination == Denomination.Hearts ? Suit.Spades : Suit.Hearts;
            if (hand.SuitLength(otherMajor) >= 3 && hand.HonorLengthPoints >= 16)
                return new(3, ToDenomination(otherMajor));
        }

        Suit? fit = answer.Denomination switch
        {
            Denomination.Hearts when hand.SuitLength(Suit.Hearts) >= 4 => Suit.Hearts,
            Denomination.Spades when hand.SuitLength(Suit.Spades) >= 4 => Suit.Spades,
            Denomination.NoTrump when hand.SuitLength(Suit.Hearts) >= 4 => Suit.Hearts,
            Denomination.NoTrump when hand.SuitLength(Suit.Spades) >= 4 => Suit.Spades,
            _ => null
        };

        var denomination = fit.HasValue ? ToDenomination(fit.Value) : Denomination.NoTrump;
        var combined = openerMinimum + (fit.HasValue ? hand.TotalPoints : hand.HonorLengthPoints);
        // Un grand chelem ne doit jamais être conclu automatiquement sur un simple
        // total de points : il exige une séquence de contrôles explicite.
        if (combined >= 33) return new(6, denomination);

        // Chassé-croisé : bicolore majeur 5-4 quand l'ouvreur n'a pas de majeure quatrième.
        if (level == 2 && answer.Denomination == Denomination.Diamonds)
        {
            Suit? five = hand.SuitLength(Suit.Hearts) >= 5 ? Suit.Hearts
                : hand.SuitLength(Suit.Spades) >= 5 ? Suit.Spades : null;
            if (five.HasValue)
            {
                var four = five == Suit.Hearts ? Suit.Spades : Suit.Hearts;
                if (hand.SuitLength(four) >= 4)
                    return h >= 10
                        ? new(3, ToDenomination(four))       // saut forcing, recherche du fit 5-3
                        : new(2, ToDenomination(five.Value)); // majeure cinquième, non forcing
            }
        }

        if (fit.HasValue) return new(h >= 10 || level == 3 ? 4 : 3, denomination);
        return new(h >= 10 || level == 3 ? 3 : 2, Denomination.NoTrump);
    }

    private static Bid? AfterTexas(HandEvaluation hand, Bid response, int openingLevel)
    {
        var target = TexasTarget(response, openingLevel);
        if (!target.HasValue) return null;
        if (openingLevel == 2) return new(4, target.Value);
        var hl = hand.HonorLengthPoints;
        if (hl <= 8) return null;

        if (target is Denomination.Hearts or Denomination.Spades)
        {
            var major = (Suit)target.Value;
            if (hand.SuitLength(major) >= 6)
            {
                Suit? shortSuit = Enum.GetValues<Suit>()
                    .Where(suit => suit != major && hand.SuitLength(suit) <= 1)
                    .OrderBy(hand.SuitLength)
                    .Cast<Suit?>()
                    .FirstOrDefault();
                if (shortSuit.HasValue && FitPoints(hand, major, 2) is >= 15 and <= 17)
                    return new(4, ToDenomination(shortSuit.Value));
                if (hl >= 10) return new(4, target.Value);
                return new(3, target.Value);
            }

            if (hl is >= 16 and <= 17)
            {
                Suit? shortSuit = Enum.GetValues<Suit>()
                    .Where(suit => suit != major && hand.SuitLength(suit) <= 1)
                    .OrderBy(hand.SuitLength)
                    .Cast<Suit?>()
                    .FirstOrDefault();
                if (shortSuit.HasValue) return new(4, ToDenomination(shortSuit.Value));
            }
        }

        if (hl is >= 16 and <= 17 && target is Denomination.Clubs or Denomination.Diamonds)
            return new(4, Denomination.NoTrump);

        var noTrump = RespondToOneNoTrump(hand);
        if (noTrump is { Denomination: Denomination.NoTrump }) return noTrump;

        // Bicolore majeur 5-5 après Texas ♠ : 4♥ en manche, 3♥ en zone de chelem.
        if (target == Denomination.Spades && hand.SuitLength(Suit.Hearts) >= 5)
            return new(hl >= 16 ? 3 : 4, Denomination.Hearts);
        if (target is Denomination.Clubs or Denomination.Diamonds)
        {
            Suit? shortSuit = Enum.GetValues<Suit>()
                .Where(suit => ToDenomination(suit) != target.Value && hand.SuitLength(suit) <= 1)
                .OrderBy(hand.SuitLength)
                .Cast<Suit?>()
                .FirstOrDefault();
            if (hand.SuitLength((Suit)target.Value) >= 5 && shortSuit.HasValue)
                return new(3, ToDenomination(shortSuit.Value));
            return new(3, Denomination.NoTrump);
        }
        return new(3, Denomination.NoTrump);
    }

    private static Denomination? TexasTarget(Bid response, int openingLevel) => (openingLevel, response) switch
    {
        (1, { Level: 2, Denomination: Denomination.Diamonds }) => Denomination.Hearts,
        (1, { Level: 2, Denomination: Denomination.Hearts }) => Denomination.Spades,
        (1, { Level: 2, Denomination: Denomination.Spades }) => Denomination.Clubs,
        (1, { Level: 3, Denomination: Denomination.Clubs }) => Denomination.Diamonds,
        (2, { Level: 3, Denomination: Denomination.Diamonds }) => Denomination.Hearts,
        (2, { Level: 3, Denomination: Denomination.Hearts }) => Denomination.Spades,
        (2, { Level: 3, Denomination: Denomination.Spades }) => Denomination.Clubs,
        (2, { Level: 4, Denomination: Denomination.Clubs }) => Denomination.Diamonds,
        _ => null
    };
}
