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
            return BidCall(seat, FirstResponse(hand, opening, auction));

        // Une intervention au palier de 5 ou 6 est déjà un contrat de chelem :
        // une réponse automatique ne doit jamais fabriquer 7 dans la couleur.
        if (intervention.Level >= 5)
            return new(seat, AuctionAction.Pass);

        // Intervention par 1SA : chapitre dédié (contre punitif, 2♣ Cappelletti, Truscott...).
        if (intervention == new Bid(1, Denomination.NoTrump))
            return RespondToMinorOpeningNoTrumpIntervention(seat, hand, opening, intervention, auction);

        // Intervention bicolore : cuebid de la couleur d'ouverture ou SA inhabituel à saut.
        if (intervention.Denomination == opening.Denomination ||
            (intervention.Denomination == Denomination.NoTrump && intervention.Level >= 2))
            return RespondToMinorOpeningTwoSuitedIntervention(seat, hand, opening, intervention, auction);

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

        if (hand.HonorLengthPoints >= (intervention.Level >= 3 ? 11 : 8) &&
            AllowsSputnikDouble(intervention, hand))
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

    private static AuctionCall RespondToMajorOpeningIntervention(
        Seat seat, HandEvaluation hand, Bid opening, Auction auction)
    {
        var opponentCall = auction.Calls.LastOrDefault(call =>
            !SameSide(call.Seat, seat) && call.Action is AuctionAction.Bid or AuctionAction.Double);

        if (opponentCall.Action == AuctionAction.Double)
            return RespondToMajorOpeningDouble(seat, hand, opening, auction);
        if (opponentCall.Bid is not { } intervention)
            return BidCall(seat, FirstResponse(hand, opening, auction));
        if (intervention == new Bid(1, Denomination.NoTrump))
            return RespondToMajorOpeningNoTrump(seat, hand, opening, auction);
        if (intervention.Denomination == opening.Denomination ||
            intervention is { Level: 2, Denomination: Denomination.NoTrump })
            return RespondToMajorOpeningTwoSuited(seat, hand, opening, intervention, auction);

        var support = hand.SuitLength((Suit)opening.Denomination);
        var fitPoints = support >= 3 ? MajorSupportPoints(hand, (Suit)opening.Denomination, 5) : 0;
        if (support >= 5 && hand.HighCardPoints is >= 8 and <= 10)
            return BidCall(seat, new Bid(4, opening.Denomination));
        if (support >= 3 && fitPoints <= 10)
            return BidCall(seat, new Bid(2, opening.Denomination));
        if (support == 3 && fitPoints is >= 11 and <= 12)
            return BidCall(seat, new Bid(2, Denomination.NoTrump));
        if (support >= 4 && fitPoints is >= 11 and <= 12)
            return BidCall(seat, new Bid(3, opening.Denomination));
        if (support >= 4 && fitPoints is >= 13 and <= 15)
        {
            if (intervention.Denomination != opening.Denomination &&
                hand.SuitLength((Suit)intervention.Denomination) <= 1 &&
                auction.IsLegal(new Bid(4, intervention.Denomination)))
                return BidCall(seat, new Bid(4, intervention.Denomination));
            if (hand.IsBalanced) return BidCall(seat, new Bid(3, Denomination.NoTrump));
            var singleton = Enum.GetValues<Suit>()
                .Where(suit => suit != (Suit)opening.Denomination && hand.SuitLength(suit) <= 1)
                .OrderBy(suit => hand.SuitLength(suit)).FirstOrDefault();
            return BidCall(seat, new Bid(4, ToDenomination(singleton)));
        }
        if (support >= 3 && fitPoints >= 16)
        {
            var forcingSuit = BestResponseSuitAfterMajor(hand, opening);
            if (forcingSuit.HasValue) return BidCall(seat, forcingSuit.Value);
            return BidCall(seat, new Bid(3, opening.Denomination));
        }

        var oneLevel = BestResponseSuitAfterMajor(hand, opening);
        if (oneLevel.HasValue && oneLevel.Value.Level == 1 && hand.HonorLengthPoints >= 6)
            return BidCall(seat, oneLevel.Value);
        var twoLevel = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 5)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
        if (hand.HighCardPoints is >= 8 and <= 10 && hand.SuitLength(twoLevel) >= 5)
            return BidCall(seat, CheapestBid(ToDenomination(twoLevel), intervention));
        if (hand.HonorLengthPoints >= (intervention.Level == 1 ? 8 : 10) && auction.CanDouble(seat))
            return new(seat, AuctionAction.Double);
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 6 and <= 10 &&
            auction.IsLegal(new Bid(1, Denomination.NoTrump)))
            return BidCall(seat, new Bid(1, Denomination.NoTrump));
        return new(seat, AuctionAction.Pass);
    }

    private static AuctionCall RespondToMajorOpeningDouble(
        Seat seat, HandEvaluation hand, Bid opening, Auction auction)
    {
        var support = hand.SuitLength((Suit)opening.Denomination);
        var fitPoints = support >= 3 ? MajorSupportPoints(hand, (Suit)opening.Denomination, 5) : 0;
        if (support == 3 && fitPoints is >= 11 and <= 12 && auction.CanRedouble(seat))
            return new(seat, AuctionAction.Redouble);
        if (support == 4 && fitPoints is >= 11 and <= 12 &&
            auction.IsLegal(new Bid(2, Denomination.NoTrump)))
            return BidCall(seat, new Bid(2, Denomination.NoTrump));
        if (support >= 3 && fitPoints is >= 11 and <= 12)
            return BidCall(seat, new Bid(3, opening.Denomination));
        if (support >= 4 && fitPoints is >= 13 and <= 15)
            return BidCall(seat, hand.IsBalanced
                ? new Bid(3, Denomination.NoTrump)
                : new Bid(4, opening.Denomination));
        if (support >= 3 && fitPoints >= 16)
            return BidCall(seat, new Bid(3, opening.Denomination));

        var newSuit = BestResponseSuitAfterMajor(hand, opening);
        if (newSuit.HasValue && hand.HonorLengthPoints >= 6)
            return BidCall(seat, newSuit.Value);
        if (hand.HighCardPoints >= 10 && auction.CanRedouble(seat))
            return new(seat, AuctionAction.Redouble);
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 8 and <= 10 &&
            auction.IsLegal(new Bid(1, Denomination.NoTrump)))
            return BidCall(seat, new Bid(1, Denomination.NoTrump));
        return new(seat, AuctionAction.Pass);
    }

    private static AuctionCall RespondToMajorOpeningNoTrump(
        Seat seat, HandEvaluation hand, Bid opening, Auction auction)
    {
        if (hand.HighCardPoints >= 9 && auction.CanDouble(seat))
            return new(seat, AuctionAction.Double);
        var support = hand.SuitLength((Suit)opening.Denomination);
        if (support >= 4 && FitPoints(hand, (Suit)opening.Denomination, 5) >= 11 &&
            auction.IsLegal(new Bid(2, Denomination.NoTrump)))
            return BidCall(seat, new Bid(2, Denomination.NoTrump));
        if (support >= 3 && hand.HighCardPoints is >= 6 and <= 10)
            return BidCall(seat, new Bid(2, opening.Denomination));
        if (support >= 4 && hand.HighCardPoints is >= 7 and <= 8)
            return BidCall(seat, new Bid(3, opening.Denomination));
        var suit = Enum.GetValues<Suit>()
            .Where(candidate => candidate != (Suit)opening.Denomination && hand.SuitLength(candidate) >= 6)
            .OrderByDescending(hand.SuitLength).ThenByDescending(candidate => candidate).FirstOrDefault();
        if (hand.SuitLength(suit) >= 6)
        {
            var bid = new Bid(2, ToDenomination(suit));
            if (auction.IsLegal(bid)) return BidCall(seat, bid);
        }
        return new(seat, AuctionAction.Pass);
    }

    private static AuctionCall RespondToMajorOpeningTwoSuited(
        Seat seat, HandEvaluation hand, Bid opening, Bid intervention, Auction auction)
    {
        var support = hand.SuitLength((Suit)opening.Denomination);
        var fitPoints = support >= 3 ? MajorSupportPoints(hand, (Suit)opening.Denomination, 5) : 0;
        if (support >= 3 && fitPoints is >= 8 and <= 10)
            return BidCall(seat, new Bid(intervention.Level + 1, opening.Denomination));
        if (support >= 4 && fitPoints is >= 11 and <= 12 && auction.IsLegal(new Bid(2, Denomination.NoTrump)))
            return BidCall(seat, new Bid(2, Denomination.NoTrump));
        if (support >= 4 && fitPoints >= 13)
            return BidCall(seat, new Bid(4, opening.Denomination));
        var otherMajor = opening.Denomination == Denomination.Hearts ? Suit.Spades : Suit.Hearts;
        if (hand.SuitLength(otherMajor) >= 5 && hand.HonorLengthPoints >= 13)
            return BidCall(seat, CheapestBid(ToDenomination(otherMajor), intervention));
        if (hand.HonorLengthPoints >= 10 && auction.CanDouble(seat))
            return new(seat, AuctionAction.Double);
        return new(seat, AuctionAction.Pass);
    }

    /// <summary>Réponses après un contre d'appel adverse sur l'ouverture mineure.</summary>
    private static AuctionCall RespondToMinorOpeningDouble(
        Seat seat, HandEvaluation hand, Bid opening, Auction auction)
    {
        var support = hand.SuitLength((Suit)opening.Denomination);

        // Truscott : soutien caché de cinq atouts et plus avec 9H et plus.
        if (support >= 5 && hand.HighCardPoints >= 9 && auction.IsLegal(new Bid(2, Denomination.NoTrump)))
            return BidCall(seat, new Bid(2, Denomination.NoTrump));

        // Soutien à saut : barrage fondé sur la loi des atouts.
        if (support >= 5 && hand.HighCardPoints is >= 7 and <= 8 &&
            auction.IsLegal(new Bid(3, opening.Denomination)))
            return BidCall(seat, new Bid(3, opening.Denomination));

        // Changement de couleur à saut : barrage naturel de force inférieure.
        var barrageSuit = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 7)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
        if (hand.HighCardPoints <= 10 && hand.SuitLength(barrageSuit) >= 7)
        {
            var jump = new Bid(hand.SuitLength(barrageSuit) >= 8 ? 4 : 3, ToDenomination(barrageSuit));
            if (auction.IsLegal(jump)) return BidCall(seat, jump);
        }

        // Changement de couleur en 1 sur 1 : quatre cartes minimum et 7HL, forcing.
        var oneLevelSuit = BestNewSuit(hand, opening.Denomination, 4);
        if (oneLevelSuit.HasValue && hand.HonorLengthPoints >= 7 &&
            CheapestBid(oneLevelSuit.Value, opening).Level == 1)
            return BidCall(seat, CheapestBid(oneLevelSuit.Value, opening));

        // Changement de couleur en 2 sur 1 : six cartes minimum et 7-10H, non forcing.
        var twoLevelSuit = BestNewSuit(hand, opening.Denomination, 6);
        if (twoLevelSuit.HasValue && hand.HighCardPoints is >= 7 and <= 10)
            return BidCall(seat, CheapestBid(twoLevelSuit.Value, opening));

        // 1SA positive : jeu régulier, 8-10HL, dénie une majeure quatrième.
        if (hand.IsBalanced && hand.HonorLengthPoints is >= 8 and <= 10 && !hand.HasFourCardMajor &&
            auction.IsLegal(new Bid(1, Denomination.NoTrump)))
            return BidCall(seat, new Bid(1, Denomination.NoTrump));

        // Surcontre : 10H et plus sans description naturelle disponible.
        if (hand.HighCardPoints >= 10 && auction.CanRedouble(seat))
            return new(seat, AuctionAction.Redouble);

        return new(seat, AuctionAction.Pass);
    }

    /// <summary>Réponses après une intervention par 1SA sur l'ouverture mineure.</summary>
    private static AuctionCall RespondToMinorOpeningNoTrumpIntervention(
        Seat seat, HandEvaluation hand, Bid opening, Bid intervention, Auction auction)
    {
        // 2♣ : bicolore majeur 5-4, sur 2♦ le partenaire répond la majeure la plus longue.
        if (hand.SuitLength(Suit.Hearts) >= 4 && hand.SuitLength(Suit.Spades) >= 4 &&
            (hand.SuitLength(Suit.Hearts) >= 5 || hand.SuitLength(Suit.Spades) >= 5))
            return BidCall(seat, new Bid(2, Denomination.Clubs));

        var support = hand.SuitLength((Suit)opening.Denomination);

        // 2SA : cuebid promettant cinq atouts et au moins 13HLD.
        if (support >= 5 && hand.TotalPoints >= 13 && auction.IsLegal(new Bid(2, Denomination.NoTrump)))
            return BidCall(seat, new Bid(2, Denomination.NoTrump));

        // Soutien à saut : barrage de force inférieure au soutien direct.
        if (support >= 5 && hand.HighCardPoints is >= 7 and <= 8 &&
            auction.IsLegal(new Bid(3, opening.Denomination)))
            return BidCall(seat, new Bid(3, opening.Denomination));

        // Soutien simple : inchangé.
        if (support >= 5)
        {
            if (hand.HighCardPoints is >= 9 and <= 11) return BidCall(seat, new Bid(3, opening.Denomination));
            if (hand.HighCardPoints is >= 6 and <= 8) return BidCall(seat, new Bid(2, opening.Denomination));
        }

        // Changement de couleur à saut : barrage.
        var barrageSuitOverOneNoTrump = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 7)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
        if (hand.HighCardPoints <= 10 && hand.SuitLength(barrageSuitOverOneNoTrump) >= 7 &&
            auction.IsLegal(new Bid(3, ToDenomination(barrageSuitOverOneNoTrump))))
            return BidCall(seat, new Bid(3, ToDenomination(barrageSuitOverOneNoTrump)));

        // Changement de couleur sans saut : non forcing, six cartes.
        var sixCardSuit = Enum.GetValues<Suit>()
            .Where(suit => ToDenomination(suit) != opening.Denomination && hand.SuitLength(suit) >= 6)
            .OrderByDescending(hand.SuitLength).ThenByDescending(suit => suit).FirstOrDefault();
        if (hand.SuitLength(sixCardSuit) >= 6)
        {
            var bid = new Bid(2, ToDenomination(sixCardSuit));
            if (auction.IsLegal(bid)) return BidCall(seat, bid);
        }

        // Contre : punitif, 9H et plus, quand aucune autre description ne s'applique.
        if (hand.HighCardPoints >= 9 && auction.CanDouble(seat))
            return new(seat, AuctionAction.Double);

        return new(seat, AuctionAction.Pass);
    }

    /// <summary>Réponses après une intervention bicolore (cuebid ou SA inhabituel) montrant les deux majeures.</summary>
    private static AuctionCall RespondToMinorOpeningTwoSuitedIntervention(
        Seat seat, HandEvaluation hand, Bid opening, Bid intervention, Auction auction)
    {
        var otherMinor = opening.Denomination == Denomination.Clubs ? Denomination.Diamonds : Denomination.Clubs;
        var support = hand.SuitLength((Suit)opening.Denomination);

        // Soutien simple : 8-10HLD.
        var fitPoints = support >= 3 ? FitPoints(hand, (Suit)opening.Denomination, 3) : 0;
        if (support >= 3 && fitPoints is >= 8 and <= 10)
        {
            var raise = new Bid(intervention.Level + 1, opening.Denomination);
            if (auction.IsLegal(raise)) return BidCall(seat, raise);
        }

        // Annonce de la dernière couleur (l'autre mineure, seule couleur non annoncée) : non forcing, 8-10H.
        if (hand.SuitLength((Suit)otherMinor) >= 6 && hand.HighCardPoints is >= 8 and <= 10)
        {
            var bid = CheapestBid(otherMinor, intervention);
            if (auction.IsLegal(bid)) return BidCall(seat, bid);
        }

        // Enchère à Sans-Atout : naturelle.
        if (hand.IsBalanced && hand.HonorLengthPoints >= 8)
        {
            var bid = CheapestBid(Denomination.NoTrump, intervention);
            if (auction.IsLegal(bid)) return BidCall(seat, bid);
        }

        // Cuebid le plus économique (♥) : soutien forcing de manche de la mineure d'ouverture.
        if (support >= 3 && fitPoints >= 13)
        {
            var cheapCuebid = CheapestBid(Denomination.Hearts, intervention);
            if (auction.IsLegal(cheapCuebid)) return BidCall(seat, cheapCuebid);
        }

        // Cuebid le moins économique (♠) : cinq cartes dans la dernière couleur, forcing de manche.
        if (hand.SuitLength((Suit)otherMinor) >= 5 && hand.HonorLengthPoints >= 13)
        {
            var expensiveCuebid = CheapestBid(Denomination.Spades, intervention);
            if (auction.IsLegal(expensiveCuebid)) return BidCall(seat, expensiveCuebid);
        }

        // Contre : appel, au moins 10HL.
        if (hand.HonorLengthPoints >= 10 && auction.CanDouble(seat))
            return new(seat, AuctionAction.Double);

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

    private static AuctionCall RespondToPreemptIntervention(
        Seat seat, HandEvaluation hand, Bid opening, Auction auction)
    {
        var support = hand.SuitLength((Suit)opening.Denomination);
        var relayStrength = support >= 3
            ? FitPoints(hand, (Suit)opening.Denomination, 6)
            : hand.HonorLengthPoints;
        if (opening.Level == 2 && relayStrength >= 15 &&
            auction.IsLegal(new Bid(2, Denomination.NoTrump)))
            return BidCall(seat, new Bid(2, Denomination.NoTrump));

        if (opening.Level == 2 && hand.HonorLengthPoints >= 13)
        {
            var changeSuit = Enum.GetValues<Suit>()
                .Where(suit => ToDenomination(suit) != opening.Denomination &&
                    hand.SuitLength(suit) >= (suit is Suit.Hearts or Suit.Spades ? 5 : 6))
                .OrderByDescending(hand.SuitLength)
                .ThenByDescending(suit => suit)
                .Select(ToDenomination)
                .Cast<Denomination?>()
                .FirstOrDefault();
            if (changeSuit.HasValue)
            {
                var bid = CheapestBid(changeSuit.Value, opening);
                if (auction.IsLegal(bid)) return BidCall(seat, bid);
            }
        }

        if (hand.HighCardPoints >= 13 && auction.CanDouble(seat))
            return new AuctionCall(seat, AuctionAction.Double);
        return new AuctionCall(seat, AuctionAction.Pass);
    }

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

    private static AuctionCall RespondToOneNoTrumpIntervention(Seat seat, HandEvaluation hand, Auction auction)
    {
        var interventionCall = auction.Calls.Last(call => !SameSide(call.Seat, seat) &&
            call.Action is AuctionAction.Bid or AuctionAction.Double);
        if (interventionCall.Action == AuctionAction.Double)
        {
            if (hand.HighCardPoints >= 10 && auction.CanRedouble(seat))
                return new(seat, AuctionAction.Redouble);
            return BidCall(seat, RespondToOneNoTrump(hand));
        }

        var intervention = interventionCall.Bid!.Value;
        if (intervention == new Bid(2, Denomination.Clubs))
        {
            if (hand.HonorLengthPoints >= 13 && !HasGuard(hand, Suit.Hearts))
                return BidCall(seat, new Bid(2, Denomination.Hearts));
            if (hand.HonorLengthPoints >= 13 && !HasGuard(hand, Suit.Spades))
                return BidCall(seat, new Bid(2, Denomination.Spades));

            var landyTransfer = RubensohlTransfer(hand);
            if (landyTransfer.HasValue) return BidCall(seat, landyTransfer.Value);

            if (hand.IsBalanced && hand.HonorLengthPoints >= 13 && !HasGuard(hand, Suit.Clubs))
                return BidCall(seat, new Bid(3, Denomination.Spades));
            if (hand.HighCardPoints >= 9 && auction.CanDouble(seat))
                return new(seat, AuctionAction.Double);
        }

        if (intervention.Level == 2 && intervention.Denomination is not Denomination.NoTrump and not Denomination.Clubs)
        {
            var transfer = RubensohlTransfer(hand);
            if (transfer.HasValue) return BidCall(seat, transfer.Value);
            if (hand.IsBalanced && hand.HonorLengthPoints >= 13 &&
                !HasGuard(hand, (Suit)intervention.Denomination))
                return BidCall(seat, new Bid(3, Denomination.Spades));
        }

        if (intervention.Denomination == Denomination.NoTrump && intervention.Level == 2)
        {
            var transfer = hand.SuitLength(Suit.Hearts) >= 5
                ? new Bid(3, Denomination.Diamonds)
                : hand.SuitLength(Suit.Spades) >= 5
                    ? new Bid(3, Denomination.Hearts)
                    : hand.SuitLength(Suit.Clubs) >= 6
                        ? new Bid(3, Denomination.Spades)
                        : new Bid(3, Denomination.Clubs);
            if (auction.IsLegal(transfer)) return BidCall(seat, transfer);
        }

        if (intervention.Level == 3 && intervention.Denomination == Denomination.Spades &&
            hand.IsBalanced && HasGuard(hand, Suit.Spades) &&
            auction.IsLegal(new Bid(3, Denomination.NoTrump)))
            return BidCall(seat, new Bid(3, Denomination.NoTrump));

        if (intervention.Level == 2)
        {
            var six = Enum.GetValues<Suit>()
                .Where(suit => hand.SuitLength(suit) >= 6)
                .OrderByDescending(hand.SuitLength)
                .ThenByDescending(suit => suit)
                .FirstOrDefault();
            if (hand.SuitLength(six) >= 6)
            {
                var natural = CheapestBid(ToDenomination(six), intervention);
                if (auction.IsLegal(natural)) return BidCall(seat, natural);
            }
        }

        if (hand.HighCardPoints >= 8 && auction.CanDouble(seat))
            return new(seat, AuctionAction.Double);
        return new(seat, AuctionAction.Pass);
    }

    private static Bid? RespondToLandyRubensohl(HandEvaluation hand, Bid response, Bid intervention) => response switch
    {
        { Level: 2, Denomination: Denomination.Hearts } when HasGuard(hand, Suit.Hearts) =>
            new Bid(2, Denomination.NoTrump),
        { Level: 2, Denomination: Denomination.Spades } when HasGuard(hand, Suit.Spades) =>
            new Bid(2, Denomination.NoTrump),
        { Level: 2, Denomination: Denomination.NoTrump } => new Bid(3, Denomination.Clubs),
        { Level: 3, Denomination: Denomination.Clubs } => new Bid(3, Denomination.Diamonds),
        { Level: 3, Denomination: Denomination.Diamonds } => new Bid(3, Denomination.Hearts),
        { Level: 3, Denomination: Denomination.Hearts } => new Bid(3, Denomination.Spades),
        { Level: 3, Denomination: Denomination.Spades } when HasGuard(hand, (Suit)intervention.Denomination) =>
            new Bid(3, Denomination.NoTrump),
        _ => null
    };

    private static Bid? RespondToRubensohl(HandEvaluation hand, Bid response, Bid intervention) => response switch
    {
        { Level: 2, Denomination: Denomination.NoTrump } => new Bid(3, Denomination.Clubs),
        { Level: 3, Denomination: Denomination.Clubs } => new Bid(3, Denomination.Diamonds),
        { Level: 3, Denomination: Denomination.Diamonds } => new Bid(3, Denomination.Hearts),
        { Level: 3, Denomination: Denomination.Hearts } => new Bid(3, Denomination.Spades),
        { Level: 3, Denomination: Denomination.Spades } when HasGuard(hand, (Suit)intervention.Denomination) =>
            new Bid(3, Denomination.NoTrump),
        _ => null
    };

    private static Bid? RubensohlTransfer(HandEvaluation hand)
    {
        if (hand.SuitLength(Suit.Clubs) >= 6) return new Bid(2, Denomination.NoTrump);
        if (hand.SuitLength(Suit.Diamonds) >= 6) return new Bid(3, Denomination.Clubs);
        if (hand.SuitLength(Suit.Hearts) >= 5) return new Bid(3, Denomination.Diamonds);
        if (hand.SuitLength(Suit.Spades) >= 5) return new Bid(3, Denomination.Hearts);
        return null;
    }

    private static AuctionCall RespondToOneNoTrumpConventionDouble(
        Seat seat, HandEvaluation hand, Bid response, Auction auction)
    {
        if (response == new Bid(2, Denomination.Clubs))
        {
            var hearts = hand.SuitLength(Suit.Hearts) == 4;
            var spades = hand.SuitLength(Suit.Spades) == 4;
            if (hearts && spades) return BidCall(seat, new Bid(3, Denomination.Clubs));
            if (hearts) return BidCall(seat, new Bid(2, Denomination.Hearts));
            if (spades) return BidCall(seat, new Bid(2, Denomination.Spades));
            if (hand.HighCardPoints is >= 16 and <= 17 && auction.IsLegal(new Bid(2, Denomination.NoTrump)))
                return BidCall(seat, new Bid(2, Denomination.NoTrump));
            if (hand.SuitLength(Suit.Diamonds) >= 5 && auction.IsLegal(new Bid(2, Denomination.Diamonds)))
                return BidCall(seat, new Bid(2, Denomination.Diamonds));
            return new(seat, AuctionAction.Pass);
        }

        var target = response == new Bid(2, Denomination.Diamonds)
            ? Suit.Hearts
            : response == new Bid(2, Denomination.Hearts) ? Suit.Spades : (Suit?)null;
        if (target.HasValue && hand.SuitLength(target.Value) >= 4)
            return BidCall(seat, new Bid(response.Level, ToDenomination(target.Value)));
        if (target.HasValue && hand.SuitLength(target.Value) >= 3 && auction.CanRedouble(seat))
            return new(seat, AuctionAction.Redouble);
        return new(seat, AuctionAction.Pass);
    }

    private static Bid RespondToLandy(HandEvaluation hand)
    {
        if (hand.SuitLength(Suit.Spades) >= 5) return new Bid(2, Denomination.Spades);
        if (hand.SuitLength(Suit.Hearts) >= 5) return new Bid(2, Denomination.Hearts);
        if (hand.SuitLength(Suit.Hearts) >= 4 && hand.SuitLength(Suit.Spades) >= 4)
            return new Bid(2, Denomination.Diamonds);
        return new Bid(2, Denomination.NoTrump);
    }

    private static AuctionCall RespondToStrongOpeningIntervention(
        Seat seat, HandEvaluation hand, Bid opening, Auction auction)
    {
        var opponentCall = auction.Calls.Last(call => !SameSide(call.Seat, seat) &&
            call.Action is AuctionAction.Bid or AuctionAction.Double);
        var naturalSuit = Enum.GetValues<Suit>()
            .Where(suit => hand.SuitLength(suit) >= 5 && ToDenomination(suit) != opponentCall.Bid?.Denomination)
            .OrderByDescending(hand.SuitLength)
            .ThenByDescending(suit => suit)
            .Select(suit => (Denomination?)ToDenomination(suit))
            .FirstOrDefault();

        if (naturalSuit.HasValue && hand.HighCardPoints >= 5)
        {
            var bid = CheapestBid(naturalSuit.Value, opponentCall.Bid ?? opening);
            if (auction.IsLegal(bid)) return BidCall(seat, bid);
        }

        if (hand.HighCardPoints >= 5)
        {
            if (opponentCall.Action == AuctionAction.Double && auction.CanRedouble(seat))
                return new(seat, AuctionAction.Redouble);
            if (opponentCall.Action == AuctionAction.Bid && auction.CanDouble(seat))
                return new(seat, AuctionAction.Double);
        }

        return new(seat, AuctionAction.Pass);
    }

    private static AuctionCall RespondToStrongTwoClubsFourthSeat(
        Seat seat, HandEvaluation hand, AuctionCall intervention, Auction auction)
    {
        if (intervention.Action == AuctionAction.Double)
            return hand.HighCardPoints >= 5 && auction.CanRedouble(seat)
                ? new(seat, AuctionAction.Redouble)
                : new(seat, AuctionAction.Pass);

        var interventionSuit = (Suit)intervention.Bid!.Value.Denomination;
        if (hand.IsBalanced && !HasGuard(interventionSuit, hand))
            return new(seat, AuctionAction.Pass);

        var naturalSuit = Enum.GetValues<Suit>()
            .Where(suit => suit != interventionSuit && hand.SuitLength(suit) >= 5)
            .OrderByDescending(hand.SuitLength)
            .ThenByDescending(suit => suit)
            .Select(suit => (Suit?)suit)
            .FirstOrDefault();
        if (naturalSuit.HasValue && hand.HonorLengthPoints >= 5)
        {
            var bid = CheapestBid(ToDenomination(naturalSuit.Value), intervention.Bid!.Value);
            if (auction.IsLegal(bid)) return BidCall(seat, bid);
        }

        if (hand.IsBalanced && auction.CanDouble(seat))
            return new(seat, AuctionAction.Double);
        return new(seat, AuctionAction.Pass);
    }

    private static AuctionCall RespondToTakeoutDouble(Seat seat, HandEvaluation hand, Auction auction)
    {
        // Surcontre adverse après le contre du partenaire : le contre devient punitif, on passe sauf main exceptionnelle.
        if (auction.Calls.Last(call => !call.IsPass).Action == AuctionAction.Redouble &&
            hand.HighCardPoints < 10)
            return new(seat, AuctionAction.Pass);

        // L'adversaire a déjà surenchéri par-dessus le contre : celui-ci n'oblige plus à parler,
        // on répond alors comme à une intervention ordinaire sur la première enchère de notre camp.
        var doubleIndex = auction.Calls.ToList().FindLastIndex(call =>
            SameSide(call.Seat, seat) && call.Action == AuctionAction.Double);
        var rebidAfterDouble = auction.Calls.Skip(doubleIndex + 1)
            .FirstOrDefault(call => call.Action == AuctionAction.Bid);
        var ownSideOpening = auction.Calls.FirstOrDefault(call =>
            call.Action == AuctionAction.Bid && SameSide(call.Seat, seat));
        if (rebidAfterDouble.Action == AuctionAction.Bid && ownSideOpening.Action == AuctionAction.Bid)
            return BidCall(seat, RespondToIntervention(hand, ownSideOpening.Bid!.Value, auction));

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
        if (auction.HighestBid is { Level: >= 4 })
            return CompetitiveFallback(seat, hand, auction);
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
            if (auction.CanDouble(seat) && hand.HonorLengthPoints >= 10)
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
            if (auction.CanDouble(seat) &&
                (hand.HighCardPoints >= 19 || hand.HonorLengthPoints >= 14 && suitableShape))
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
