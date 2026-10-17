using Bridge.Core.Models;
using Bridge.Core.Strategies;

namespace Bridge.AI.CardPlay;

/// <summary>Contraintes déduites des enchères et des cartes déjà visibles.</summary>
internal sealed class CardKnowledge
{
    private readonly ICardPlayEngine _game;
    private readonly Seat _observer;
    private readonly Dictionary<Seat, HashSet<Suit>> _voids = [];
    private readonly Dictionary<Seat, Dictionary<Suit, int>> _minimumLengths = [];

    private CardKnowledge(ICardPlayEngine game, Seat observer)
    {
        _game = game;
        _observer = observer;
        foreach (var seat in Enum.GetValues<Seat>())
        {
            _voids[seat] = [];
            _minimumLengths[seat] = [];
        }
        ReadPlayedCards();
        ReadNaturalBids();
    }

    public static CardKnowledge From(ICardPlayEngine game, Seat observer) => new(game, observer);

    public Suit? OpeningLeadSuit =>
        _game.PlayedCards.FirstOrDefault().Card.Suit is var suit &&
        (_game.PlayedCards.Count > 0 || _game.CurrentTrick.Count > 0)
            ? _game.PlayedCards.Count > 0
                ? _game.PlayedCards[0].Card.Suit
                : _game.CurrentTrick[0].Card.Suit
            : null;

    public Card? OpeningLead => _game.PlayedCards.Count > 0
        ? _game.PlayedCards[0].Card
        : _game.CurrentTrick.Count > 0 ? _game.CurrentTrick[0].Card : null;

    public bool IsVoid(Seat seat, Suit suit) => _voids[seat].Contains(suit);

    public int MinimumLength(Seat seat, Suit suit) =>
        _minimumLengths[seat].GetValueOrDefault(suit);

    public bool CanHold(Seat seat, Card card) =>
        !IsVoid(seat, card.Suit) &&
        !_game.PlayedCards.Any(play => play.Seat == seat && play.Card == card);

    public bool IsKnownToObserver(Card card) =>
        _game.Hand(_observer).Contains(card) ||
        (IsDeclarerSide() && _game.Hand(_game.GetDeclarer().Partner()).Contains(card)) ||
        _game.PlayedCards.Any(play => play.Card == card);

    public bool IsLikelyWithOpponent(Card card)
    {
        if (IsKnownToObserver(card))
            return false;
        var opponents = Enum.GetValues<Seat>().Where(seat => !SameSide(seat, _observer));
        return opponents.Any(seat => CanHold(seat, card));
    }

    private void ReadPlayedCards()
    {
        foreach (var trick in _game.PlayedCards.Chunk(4))
        {
            if (trick.Length < 2)
                continue;
            var leadSuit = trick[0].Card.Suit;
            foreach (var play in trick.Skip(1).Where(play => play.Card.Suit != leadSuit))
                _voids[play.Seat].Add(leadSuit);
        }

        foreach (var play in _game.CurrentTrick.Skip(1))
            if (play.Card.Suit != _game.CurrentTrick[0].Card.Suit)
                _voids[play.Seat].Add(_game.CurrentTrick[0].Card.Suit);
    }

    private void ReadNaturalBids()
    {
        foreach (var call in _game.AuctionCalls)
        {
            if (call.Action != AuctionAction.Bid || call.Bid is not { } bid ||
                bid.Denomination == Denomination.NoTrump)
                continue;

            var suit = (Suit)bid.Denomination;
            var minimum = bid.Denomination is Denomination.Hearts or Denomination.Spades
                ? bid.Level == 1 ? 5 : 6
                : bid.Level == 1 ? 3 : 5;
            _minimumLengths[call.Seat][suit] = Math.Max(MinimumLength(call.Seat, suit), minimum);
        }
    }

    private bool IsDeclarerSide() => _observer.IsNorthSouth() == _game.GetDeclarer().IsNorthSouth();

    private static bool SameSide(Seat left, Seat right) => left.IsNorthSouth() == right.IsNorthSouth();
}