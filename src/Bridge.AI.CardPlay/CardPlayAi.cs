namespace Bridge.AI.CardPlay;

using Bridge.Core.Models;
using Bridge.Core.Strategies;
using Bridge.Core.Logging;

/// <summary>
/// IA avancée pour le jeu de cartes au Bridge.
/// </summary>
public sealed class AdvancedCardPlayAi : ICardPlayStrategy
{
    private readonly ILogger _logger;

    public AdvancedCardPlayAi(ILogger? logger = null)
    {
        _logger = logger ?? LoggerProvider.GetLogger();
    }

    public Card ChooseCard(ICardPlayEngine game, Seat seat)
    {
        var legalCards = game.LegalCards(seat);
        if (legalCards.Count == 0)
            throw new InvalidOperationException("Aucune carte légale disponible.");

        var decision = game.CurrentTrick.Count == 0
            ? ChooseLead(legalCards, game, seat)
            : ChooseFollow(legalCards, game, seat);
        _logger.Debug($"[CardPlay] {seat} joue {decision.Card} — {decision.Reason}");
        return decision.Card;
    }

    private static Decision ChooseLead(IReadOnlyList<Card> legalCards, ICardPlayEngine game, Seat seat)
    {
        var declarerSide = IsDeclarerSide(game, seat);
        var trump = game.GetTrumpSuit();
        if (declarerSide)
        {
            var finesseLead = LeadTowardTenace(legalCards, game, seat);
            if (finesseLead.HasValue)
                return new(finesseLead.Value, "impasse vers la tenace du partenaire");

            var shortHandRuff = LeadToShortHandRuff(legalCards, game, seat, trump);
            if (shortHandRuff.HasValue)
                return new(shortHandRuff.Value, "coupe de la main courte avant le retrait des atouts");

            if (trump.HasValue && CombinedSuitLength(game, seat, trump.Value) >= 8 &&
                CountSureWinners(game, seat) >= RequiredTricks(game))
            {
                var lowTrump = legalCards.Where(card => card.Suit == trump).OrderBy(card => card.Rank).FirstOrDefault();
                if (legalCards.Contains(lowTrump))
                    return new(lowTrump, "retrait des atouts après compte des levées sûres");
            }

            var enoughSureWinners = !trump.HasValue || CountSureWinners(game, seat) >= RequiredTricks(game);
            if (enoughSureWinners)
            {
                var sequence = TopOfSequence(legalCards);
                if (sequence.HasValue)
                    return new(sequence.Value, "affranchissement d'une séquence d'honneurs");
            }

            var leadSuit = DeclarerLeadSuit(legalCards, game, seat, trump);
            return LeadFromSuit(leadSuit, trump, "plan d'affranchissement de la couleur longue");
        }

        var requestedSuit = PartnerRequestedSuit(game, seat);
        if (requestedSuit.HasValue)
        {
            var returns = legalCards.Where(card => card.Suit == requestedSuit).OrderBy(card => card.Rank).ToList();
            if (returns.Count > 0)
                return new(returns[0], "retour dans la couleur appelée par le partenaire");
        }

            var partnerBidSuits = NaturalBidSuits(game, seat.Partner()).ToList();
            if (partnerBidSuits.Count > 0)
            {
                var partnerSuitCards = legalCards.Where(card => card.Suit == partnerBidSuits[^1]).ToList();
                if (partnerSuitCards.Count > 0)
                return LeadFromSuit(partnerSuitCards, trump, "entame dans la couleur naturelle du partenaire");
            }

        var candidates = trump.HasValue
            ? legalCards.Where(card => card.Suit != trump).ToList()
            : legalCards.ToList();
        if (candidates.Count == 0)
            candidates = legalCards.ToList();

        if (trump.HasValue)
        {
            var singleton = candidates.GroupBy(card => card.Suit).FirstOrDefault(group => group.Count() == 1);
            if (singleton is not null)
                return new(singleton.Single(), "entame singleton en contrat à la couleur");
        }

        var declarerSuits = NaturalBidSuits(game, game.GetDeclarer()).ToHashSet();
        var dummySuits = NaturalBidSuits(game, game.GetDeclarer().Partner()).ToHashSet();
        var allBidSuits = game.AuctionCalls.SelectMany(call => NaturalBidSuit(game.AuctionCalls, call)).ToHashSet();
        var selected = candidates.GroupBy(card => card.Suit)
            .OrderByDescending(group => OpeningLeadScore(group, declarerSuits, dummySuits, allBidSuits, trump))
            .First().ToList();
        var context = allBidSuits.Contains(selected[0].Suit)
            ? "entame dans la couleur la plus prometteuse selon les enchères"
            : "entame dans une couleur non nommée par les adversaires";
        return LeadFromSuit(selected, trump, context);
    }

    private static Decision ChooseFollow(IReadOnlyList<Card> legalCards, ICardPlayEngine game, Seat seat)
    {
        var trick = game.CurrentTrick;
        var leadSuit = trick[0].Card.Suit;
        var trump = game.GetTrumpSuit();
        var partner = seat.Partner();
        var winner = WinningPlay(trick, trump);
        var partnerWinning = winner.Seat == partner;
        var declarerSide = IsDeclarerSide(game, seat);
        var following = legalCards.Where(card => card.Suit == leadSuit).OrderBy(card => card.Rank).ToList();

        if (following.Count == 0)
            return ChooseDiscardOrRuff(legalCards, game, seat, partnerWinning);

        if (declarerSide && ShouldBathCoup(game, seat, following))
            return new(following[0], "coup de Bath, Roi laissé passer avec A-V");

        if (declarerSide && ShouldHoldUp(game, seat, following, trick))
            return new(following[0], "laissez-passer pour couper les communications");

        var finesse = declarerSide ? FinesseCard(game, seat, following, winner.Card) : null;
        if (finesse.HasValue)
            return new(finesse.Value, "impasse en jouant l'honneur inférieur de la tenace");

        if (partnerWinning)
        {
            var unblock = UnblockHonor(following, winner.Card);
            if (unblock.HasValue)
                return new(unblock.Value, "déblocage d'honneur, coup du cachalot");
            return new(AttitudeSignal(following), "signalisation d'attitude au partenaire");
        }

        var honorCover = CoverHonor(following, winner.Card);
        if (honorCover.HasValue && IsOpponent(game, seat, winner.Seat))
            return new(honorCover.Value, "honneur sur honneur");

        var winners = following.Where(card => Beats(card, winner.Card, leadSuit, trump)).OrderBy(card => card.Rank).ToList();
        if (trick.Count == 2 && !declarerSide)
        {
            if (winners.Count > 0)
                return new(winners[0], "troisième main haute, en couvrant au plus juste");
            if (winner.Card.Rank == Rank.Ace)
                return new(following[0], "l'As adverse gagne déjà, honneur conservé");
            return new(following[^1], "troisième main haute pour forcer un honneur");
        }

        if (winners.Count > 0)
            return new(winners[0], "levée gagnée avec la plus petite carte suffisante");
        return new(following[0], "économie d'un honneur sans espoir de levée");
    }

    private static Decision ChooseDiscardOrRuff(IReadOnlyList<Card> legalCards, ICardPlayEngine game,
        Seat seat, bool partnerWinning)
    {
        var trump = game.GetTrumpSuit();
        if (!partnerWinning && trump.HasValue)
        {
            var winner = WinningPlay(game.CurrentTrick, trump);
            var trumps = legalCards
                .Where(card => card.Suit == trump && Beats(card, winner.Card, game.CurrentTrick[0].Card.Suit, trump))
                .OrderBy(card => card.Rank)
                .ToList();
            if (trumps.Count > 0)
                return new(trumps[0], "coupe avec le plus petit atout gagnant");
        }

        var discardSuits = legalCards.Where(card => !trump.HasValue || card.Suit != trump)
            .GroupBy(card => card.Suit)
            .OrderBy(group => HasTopHonor(group) ? 1 : 0)
            .ThenBy(group => group.Count())
            .ToList();
        var selectedSuit = discardSuits.Count > 0 ? discardSuits[0].ToList() : legalCards.ToList();
        if (selectedSuit.All(card => card.Rank >= Rank.Queen))
        {
            var safeDiscard = legalCards
                .Where(card => (!trump.HasValue || card.Suit != trump) && card.Rank < Rank.Queen)
                .OrderBy(card => card.Rank)
                .FirstOrDefault();
            if (safeDiscard != default)
                selectedSuit = [safeDiscard];
        }
        var discard = HasTopHonor(selectedSuit) && selectedSuit.Count >= 3
            ? selectedSuit.Where(card => card.Rank < Rank.Ten).OrderByDescending(card => card.Rank).FirstOrDefault()
            : selectedSuit.OrderBy(card => card.Rank).First();
        if (!selectedSuit.Contains(discard))
            discard = selectedSuit.OrderBy(card => card.Rank).First();
        return new(discard, partnerWinning
            ? "défausse signalant une couleur à conserver"
            : "défausse dans la couleur la moins prometteuse");
    }

    private static Card? LeadTowardTenace(IReadOnlyList<Card> legalCards, ICardPlayEngine game, Seat seat)
    {
        var partnerHand = game.Hand(seat.Partner());
        foreach (var suit in Enum.GetValues<Suit>())
        {
            var own = legalCards.Where(card => card.Suit == suit).OrderBy(card => card.Rank).ToList();
            var partner = partnerHand.Where(card => card.Suit == suit).ToList();
            var hasAceQueen = partner.Any(card => card.Rank == Rank.Ace) &&
                partner.Any(card => card.Rank == Rank.Queen) &&
                !KnownByDeclarer(game, seat, new Card(suit, Rank.King));
            var hasKingJack = partner.Any(card => card.Rank == Rank.King) &&
                partner.Any(card => card.Rank == Rank.Jack) &&
                !KnownByDeclarer(game, seat, new Card(suit, Rank.Queen));
            if (own.Count > 0 && (hasAceQueen || hasKingJack))
                return own[0];
        }
        return null;
    }

    private static Card? LeadToShortHandRuff(
        IReadOnlyList<Card> legalCards, ICardPlayEngine game, Seat seat, Suit? trump)
    {
        if (!trump.HasValue)
            return null;

        var ownTrumpCount = game.Hand(seat).Count(card => card.Suit == trump.Value);
        var partnerTrumpCount = game.Hand(seat.Partner()).Count(card => card.Suit == trump.Value);
        if (ownTrumpCount >= partnerTrumpCount)
            return null;

        var partnerSuits = game.Hand(seat.Partner())
            .GroupBy(card => card.Suit)
            .ToDictionary(group => group.Key, group => group.Count());
        var singleton = legalCards
            .Where(card => card.Suit != trump.Value)
            .GroupBy(card => card.Suit)
            .Where(group => group.Count() == 1 && partnerSuits.GetValueOrDefault(group.Key) >= 2)
            .SelectMany(group => group)
            .FirstOrDefault();
        return singleton == default ? null : singleton;
    }

    private static Card? FinesseCard(ICardPlayEngine game, Seat seat, IReadOnlyList<Card> following, Card winner)
    {
        if (game.CurrentTrick[0].Seat != seat.Partner())
            return null;
        var suit = winner.Suit;
        var hasAceQueen = following.Any(card => card.Rank == Rank.Ace) &&
            following.Any(card => card.Rank == Rank.Queen) &&
            !game.PlayedCards.Any(play => play.Card == new Card(suit, Rank.King));
        if (hasAceQueen && winner.Rank < Rank.Queen)
            return following.First(card => card.Rank == Rank.Queen);
        var hasKingJack = following.Any(card => card.Rank == Rank.King) &&
            following.Any(card => card.Rank == Rank.Jack) &&
            !game.PlayedCards.Any(play => play.Card == new Card(suit, Rank.Queen));
        return hasKingJack && winner.Rank < Rank.Jack
            ? following.First(card => card.Rank == Rank.Jack)
            : null;
    }

    private static bool ShouldBathCoup(ICardPlayEngine game, Seat seat, IReadOnlyList<Card> following) =>
        game.GetTrumpSuit() is null && game.TrickNumber == 1 && game.CurrentTrick[0].Card.Rank == Rank.King &&
        game.CurrentTrick[0].Seat != seat.Partner() && following.Count >= 3 &&
        following.Any(card => card.Rank == Rank.Ace) && following.Any(card => card.Rank == Rank.Jack) &&
        following.Any(card => card.Rank < Rank.Jack);

    private static bool ShouldHoldUp(ICardPlayEngine game, Seat seat, IReadOnlyList<Card> following,
        IReadOnlyList<PlayedCard> trick) => game.GetTrumpSuit() is null && game.TrickNumber <= 2 &&
        trick.Count < 3 && IsOpponent(game, seat, trick[0].Seat) && trick[0].Card.Rank >= Rank.Queen &&
        following.Count >= 3 && following.Any(card => card.Rank == Rank.Ace) &&
        following.Any(card => card.Rank < Rank.Ten);

    private static Card? CoverHonor(IReadOnlyList<Card> following, Card winningCard)
    {
        if (winningCard.Rank < Rank.Jack)
            return null;
        return following.Where(card => card.Rank > winningCard.Rank && card.Rank >= Rank.Queen)
            .OrderBy(card => card.Rank).Cast<Card?>().FirstOrDefault();
    }

    private static Card? UnblockHonor(IReadOnlyList<Card> following, Card partnerWinner)
    {
        if (following.Count > 2)
            return null;
        var target = partnerWinner.Rank switch
        {
            Rank.Ace => Rank.King,
            Rank.King => Rank.Queen,
            Rank.Queen => Rank.Jack,
            _ => (Rank?)null
        };
        return target.HasValue && following.Any(card => card.Rank == target)
            ? following.First(card => card.Rank == target)
            : null;
    }

    private static Card AttitudeSignal(IReadOnlyList<Card> cards)
    {
        var spots = cards.Where(card => card.Rank < Rank.Ten).OrderBy(card => card.Rank).ToList();
        if (spots.Count == 0)
            return cards[0];
        return HasTopHonor(cards) && cards.Count >= 3 ? spots[^1] : spots[0];
    }

    private static Suit? PartnerRequestedSuit(ICardPlayEngine game, Seat seat)
    {
        var partnerCards = game.PlayedCards.Where(play => play.Seat == seat.Partner()).ToList();
        foreach (var suitGroup in partnerCards.GroupBy(play => play.Card.Suit))
        {
            var cards = suitGroup.Select(play => play.Card).ToList();
            if (cards.Count >= 2 && cards[^2].Rank > cards[^1].Rank && game.Hand(seat).Any(card => card.Suit == suitGroup.Key))
                return suitGroup.Key;
        }
        if (game.PlayedCards.Count >= 4)
        {
            var lastTrick = game.PlayedCards.TakeLast(4).ToList();
            var partnerCard = lastTrick.FirstOrDefault(play => play.Seat == seat.Partner());
            if (partnerCard != default && partnerCard.Card.Rank >= Rank.Eight &&
                game.Hand(seat).Any(card => card.Suit == partnerCard.Card.Suit))
                return partnerCard.Card.Suit;
        }
        return null;
    }

    private static Decision LeadFromSuit(IReadOnlyList<Card> cards, Suit? trump, string reason)
    {
        var sequence = TopOfSequence(cards);
        if (sequence.HasValue)
            return new(sequence.Value, $"{reason}, tête de séquence");
        var ordered = cards.OrderByDescending(card => card.Rank).ToList();
        if (!trump.HasValue && ordered.Count >= 4)
            return new(ordered[3], $"{reason}, quatrième meilleure");
        return new(ordered[^1], $"{reason}, petite carte");
    }

    private static int OpeningLeadScore(IEnumerable<Card> suitCards, IReadOnlySet<Suit> declarerSuits,
        IReadOnlySet<Suit> dummySuits, IReadOnlySet<Suit> allBidSuits, Suit? trump)
    {
        var cards = suitCards.ToList();
        var suit = cards[0].Suit;
        var score = cards.Count * 4;
        if (!allBidSuits.Contains(suit)) score += 8;
        if (declarerSuits.Contains(suit)) score -= 14;
        if (dummySuits.Contains(suit)) score -= 8;
        if (trump == suit) score -= 12;
        if (TopOfSequence(cards).HasValue) score += 10;
        if (cards.Count == 1 && trump.HasValue) score += 12;
        return score;
    }

    private static IEnumerable<Suit> NaturalBidSuits(ICardPlayEngine game, Seat seat) =>
        game.AuctionCalls.Where(call => call.Seat == seat)
            .SelectMany(call => NaturalBidSuit(game.AuctionCalls, call));

    private static IEnumerable<Suit> NaturalBidSuit(IReadOnlyList<AuctionCall> calls, AuctionCall call)
    {
        if (call.Action != AuctionAction.Bid || call.Bid is not { } bid || bid.Denomination == Denomination.NoTrump)
            return [];

        var index = IndexOf(calls, call);
        if (index < 0)
            return [];
        var previousPartnerBid = calls.Take(index).LastOrDefault(candidate =>
            candidate.Action == AuctionAction.Bid && candidate.Seat == call.Seat.Partner()).Bid;

        if (bid == new Bid(2, Denomination.Clubs) && previousPartnerBid == new Bid(1, Denomination.NoTrump))
            return [];
        if (previousPartnerBid == new Bid(1, Denomination.NoTrump) &&
            bid is { Level: 2, Denomination: Denomination.Diamonds or Denomination.Hearts or Denomination.Spades })
        {
            var target = bid.Denomination switch
            {
                Denomination.Diamonds => Suit.Hearts,
                Denomination.Hearts => Suit.Spades,
                Denomination.Spades => Suit.Clubs,
                _ => (Suit?)null
            };
            return target.HasValue ? [target.Value] : [];
        }
        if (index == 0 && bid.Level == 2 && bid.Denomination is Denomination.Clubs or Denomination.Diamonds)
            return [];
        return [(Suit)bid.Denomination];
    }

    private static int IndexOf(IReadOnlyList<AuctionCall> calls, AuctionCall target)
    {
        for (var index = 0; index < calls.Count; index++)
            if (calls[index].Equals(target)) return index;
        return -1;
    }

    private static Card? TopOfSequence(IEnumerable<Card> cards)
    {
        foreach (var suitGroup in cards.GroupBy(card => card.Suit)
                     .OrderByDescending(group => group.Count()))
        {
            var ranks = suitGroup.Select(card => card.Rank).ToHashSet();
            foreach (var rank in new[] { Rank.Ace, Rank.King, Rank.Queen, Rank.Jack })
            {
                if (ranks.Contains(rank) && ranks.Contains((Rank)((int)rank - 1)))
                    return new Card(suitGroup.Key, rank);
            }
        }
        return null;
    }

    private static Card LowFromLongestSuit(IEnumerable<Card> cards, Suit? trump) => cards
        .Where(card => !trump.HasValue || card.Suit != trump)
        .DefaultIfEmpty(cards.OrderBy(card => card.Rank).First())
        .GroupBy(card => card.Suit)
        .OrderByDescending(group => group.Count())
        .First().OrderBy(card => card.Rank).First();

    private static List<Card> DeclarerLeadSuit(
        IEnumerable<Card> cards, ICardPlayEngine game, Seat seat, Suit? trump)
    {
        var partnerCards = game.Hand(seat.Partner());
        var candidates = cards
            .Where(card => !trump.HasValue || card.Suit != trump)
            .GroupBy(card => card.Suit)
            .OrderByDescending(group => group.Count() * 5 +
                group.Count(card => card.Rank >= Rank.Queen) * 2 +
                partnerCards.Count(card => card.Suit == group.Key) * 2 +
                partnerCards.Count(card => card.Suit == group.Key && card.Rank >= Rank.Queen))
            .ThenByDescending(group => group.Max(card => (int)card.Rank))
            .FirstOrDefault();
        return candidates?.ToList() ?? cards.ToList();
    }

    private static PlayedCard WinningPlay(IReadOnlyList<PlayedCard> trick, Suit? trump)
    {
        var winner = trick[0];
        foreach (var candidate in trick.Skip(1))
            if (Beats(candidate.Card, winner.Card, trick[0].Card.Suit, trump))
                winner = candidate;
        return winner;
    }

    private static bool Beats(Card challenger, Card winner, Suit leadSuit, Suit? trump)
    {
        if (trump.HasValue && challenger.Suit == trump && winner.Suit != trump)
            return true;
        if (challenger.Suit != winner.Suit)
            return challenger.Suit == leadSuit && winner.Suit != leadSuit && winner.Suit != trump;
        return challenger.Rank > winner.Rank;
    }

    private static bool KnownByDeclarer(ICardPlayEngine game, Seat seat, Card card) =>
        game.Hand(seat).Contains(card) || game.Hand(seat.Partner()).Contains(card) ||
        game.PlayedCards.Any(play => play.Card == card);

    private static bool IsDeclarerSide(ICardPlayEngine game, Seat seat) =>
        seat.IsNorthSouth() == game.GetDeclarer().IsNorthSouth();

    private static bool IsOpponent(ICardPlayEngine game, Seat seat, Seat other) =>
        seat.IsNorthSouth() != other.IsNorthSouth();

    private static int CombinedSuitLength(ICardPlayEngine game, Seat seat, Suit suit) =>
        game.Hand(seat).Count(card => card.Suit == suit) + game.Hand(seat.Partner()).Count(card => card.Suit == suit);

    private static int RequiredTricks(ICardPlayEngine game) =>
        game.AuctionCalls.LastOrDefault(call => call.Bid.HasValue).Bid?.Level + 6 ?? 7;

    private static int CountSureWinners(ICardPlayEngine game, Seat seat)
    {
        var cards = game.Hand(seat).Concat(game.Hand(seat.Partner())).ToList();
        var winners = 0;
        foreach (var suit in Enum.GetValues<Suit>())
        {
            var ranks = cards.Where(card => card.Suit == suit).Select(card => card.Rank).ToHashSet();
            if (ranks.Contains(Rank.Ace)) winners++;
            if (ranks.Contains(Rank.King) && ranks.Contains(Rank.Ace)) winners++;
            if (ranks.Contains(Rank.Queen) && ranks.Contains(Rank.King) && ranks.Contains(Rank.Ace)) winners++;
            if (ranks.Contains(Rank.Jack) && ranks.Contains(Rank.Queen) &&
                ranks.Contains(Rank.King) && ranks.Contains(Rank.Ace)) winners++;
        }
        return winners;
    }

    private static bool HasTopHonor(IEnumerable<Card> cards) => cards.Any(card => card.Rank >= Rank.Queen);

    private static int CardValue(Card card) => (int)card.Rank;

    private readonly record struct Decision(Card Card, string Reason);
}

/// <summary>
/// IA simple pour le jeu de cartes.
/// </summary>
public sealed class SimpleCardPlayAi : ICardPlayStrategy
{
    private readonly ILogger _logger;

    public SimpleCardPlayAi(ILogger? logger = null)
    {
        _logger = logger ?? LoggerProvider.GetLogger();
    }

    public Card ChooseCard(ICardPlayEngine game, Seat seat)
    {
        var legalCards = game.LegalCards(seat).OrderBy(card => card.Rank).ToList();

        if (game.CurrentTrick.Count == 0)
        {
            var card = legalCards[0];
            _logger.Debug($"[CardPlay] {seat} entame avec {card}");
            return card;
        }

        var leadSuit = game.CurrentTrick[0].Card.Suit;
        var following = legalCards.Where(card => card.Suit == leadSuit).ToList();
        var chosenCard = following.Count > 0 ? following[0] : legalCards[0];
        
        _logger.Trace($"[CardPlay] {seat} joue {chosenCard}");
        return chosenCard;
    }
}
