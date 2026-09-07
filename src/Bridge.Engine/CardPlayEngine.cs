namespace Bridge.Engine;

using Bridge.Core.Models;
using Bridge.Core.Strategies;
using Bridge.Core.Logging;

/// <summary>
/// Moteur de jeu de cartes au Bridge.
/// </summary>
public sealed class CardPlayEngine : ICardPlayEngine
{
    private readonly Deal _deal;
    private readonly Contract _contract;
    private readonly List<PlayedCard> _currentTrick = [];
    private readonly List<PlayedCard> _completedCards = [];
    private readonly IReadOnlyList<AuctionCall> _auctionCalls;
    private readonly ILogger _logger;
    private Seat _nextToPlay;

    public CardPlayEngine(Deal deal, Contract contract, ILogger? logger = null,
        IReadOnlyList<AuctionCall>? auctionCalls = null)
    {
        _deal = deal ?? throw new ArgumentNullException(nameof(deal));
        _contract = contract;
        _logger = logger ?? LoggerProvider.GetLogger();
        _auctionCalls = auctionCalls?.ToList() ?? [];
        _nextToPlay = contract.Declarer.Next();

        _logger.Information($"[Engine] Contrat: {contract} - Entame: {_nextToPlay}");
    }

    public Seat NextToPlay => _nextToPlay;
    public int DeclarerTricks { get; private set; }
    public int DefenderTricks { get; private set; }
    public int TrickNumber => _completedCards.Count / 4 + 1;
    public bool IsComplete => _completedCards.Count == 52;
    public IReadOnlyList<PlayedCard> CurrentTrick => _currentTrick;
    public IReadOnlyList<PlayedCard> PlayedCards => _completedCards;
    public IReadOnlyList<AuctionCall> AuctionCalls => _auctionCalls;

    public IReadOnlyList<Card> Hand(Seat seat) => _deal[seat];

    public IReadOnlyList<Card> LegalCards(Seat seat)
    {
        var hand = _deal[seat];
        if (_currentTrick.Count == 0)
        {
            return hand;
        }

        var leadSuit = _currentTrick[0].Card.Suit;
        var followingSuit = hand.Where(card => card.Suit == leadSuit).ToList();
        return followingSuit.Count > 0 ? followingSuit : hand;
    }

    public Seat Play(Seat seat, Card card)
    {
        if (IsComplete || seat != _nextToPlay)
        {
            throw new InvalidOperationException("Ce n'est pas le tour de ce joueur.");
        }

        var legalCards = LegalCards(seat);
        if (!legalCards.Contains(card))
        {
            throw new ArgumentException("Cette carte n'est pas légale.", nameof(card));
        }

        _deal.GetMutableHand(seat).Remove(card);
        _currentTrick.Add(new PlayedCard(seat, card));
        _logger.Trace($"[Engine] Levée {TrickNumber}: {seat} joue {card}");

        _nextToPlay = _nextToPlay.Next();

        if (_currentTrick.Count < 4)
        {
            return default;
        }

        var winner = DetermineWinner();
        if (winner.IsNorthSouth() == _contract.Declarer.IsNorthSouth())
        {
            DeclarerTricks++;
        }
        else
        {
            DefenderTricks++;
        }

        _logger.Information($"[Engine] Levée {TrickNumber-1} gagnée par {winner} (Déclarant: {DeclarerTricks}, Défense: {DefenderTricks})");

        _completedCards.AddRange(_currentTrick);
        _currentTrick.Clear();
        _nextToPlay = winner;
        return winner;
    }

    public Suit? GetTrumpSuit() => _contract.Bid.Denomination == Denomination.NoTrump
        ? null
        : (Suit)_contract.Bid.Denomination;

    public Seat GetDeclarer() => _contract.Declarer;

    private Seat DetermineWinner()
    {
        var leadSuit = _currentTrick[0].Card.Suit;
        var winningCard = _currentTrick[0];
        foreach (var candidate in _currentTrick.Skip(1))
        {
            if (Beats(candidate.Card, winningCard.Card, leadSuit))
            {
                winningCard = candidate;
            }
        }

        return winningCard.Seat;
    }

    private bool Beats(Card challenger, Card currentWinner, Suit leadSuit)
    {
        var trump = GetTrumpSuit();
        if (trump.HasValue && challenger.Suit == trump && currentWinner.Suit != trump)
        {
            return true;
        }

        return challenger.Suit == currentWinner.Suit && challenger.Rank > currentWinner.Rank ||
            challenger.Suit == leadSuit && currentWinner.Suit != leadSuit && currentWinner.Suit != trump;
    }
}

/// <summary>
/// Orchest rateur du moteur IA Bridge.
/// </summary>
public sealed class BridgeGameEngine
{
    private readonly IBiddingStrategy _biddingStrategy;
    private readonly ICardPlayStrategy _cardPlayStrategy;
    private readonly ILogger _logger;

    public BridgeGameEngine(IBiddingStrategy biddingStrategy, ICardPlayStrategy cardPlayStrategy, ILogger? logger = null)
    {
        _biddingStrategy = biddingStrategy ?? throw new ArgumentNullException(nameof(biddingStrategy));
        _cardPlayStrategy = cardPlayStrategy ?? throw new ArgumentNullException(nameof(cardPlayStrategy));
        _logger = logger ?? LoggerProvider.GetLogger();
    }

    public Bid? DecideBid(Seat seat, IReadOnlyList<Card> hand, Auction auction) =>
        _biddingStrategy.DecideBid(seat, hand, auction);

    public AuctionCall DecideCall(Seat seat, IReadOnlyList<Card> hand, Auction auction) =>
        _biddingStrategy is ICompetitiveBiddingStrategy competitive
            ? competitive.DecideCall(seat, hand, auction)
            : new AuctionCall(seat, AuctionAction.Pass);

    public Card ChooseCard(CardPlayEngine game, Seat seat) =>
        _cardPlayStrategy.ChooseCard(game, seat);

    public CardPlayAnalysis AnalyzeDeal(Deal initialDeal, Contract contract,
        IReadOnlyList<AuctionCall>? auctionCalls = null)
    {
        var game = new CardPlayEngine(initialDeal.Clone(), contract, _logger, auctionCalls);
        var plays = new List<PlayedCard>(52);
        while (!game.IsComplete)
        {
            var seat = game.NextToPlay;
            var card = _cardPlayStrategy.ChooseCard(game, seat);
            plays.Add(new PlayedCard(seat, card));
            game.Play(seat, card);
        }

        return new CardPlayAnalysis(contract, game.DeclarerTricks, game.DefenderTricks, plays);
    }

    public AiDealAnalysis AnalyzeFullDeal(Deal initialDeal, Seat dealer = Seat.North)
    {
        var auction = new Auction();
        var seat = dealer;
        var callCount = 0;
        while (!auction.IsComplete)
        {
            if (callCount++ >= 64)
                throw new InvalidOperationException("L'enchère IA ne se termine pas.");
            auction.AddCall(DecideCall(seat, initialDeal[seat], auction));
            seat = seat.Next();
        }

        var calls = auction.Calls.ToList();
        var contract = auction.GetContract();
        var cardPlay = contract.HasValue
            ? AnalyzeDeal(initialDeal, contract.Value, calls)
            : null;
        return new AiDealAnalysis(calls, contract, cardPlay);
    }
}

public sealed record AiDealAnalysis(
    IReadOnlyList<AuctionCall> AuctionCalls,
    Contract? Contract,
    CardPlayAnalysis? CardPlay);

public sealed record CardPlayAnalysis(
    Contract Contract,
    int DeclarerTricks,
    int DefenderTricks,
    IReadOnlyList<PlayedCard> Plays)
{
    public bool ContractMade => DeclarerTricks >= Contract.RequiredTricks;
}
