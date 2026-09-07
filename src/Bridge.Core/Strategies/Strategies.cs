namespace Bridge.Core.Strategies;

using Bridge.Core.Models;

/// <summary>
/// Interface pour les stratégies d'enchères au Bridge.
/// </summary>
public interface IBiddingStrategy
{
    /// <summary>
    /// Décide une enchère basée sur la main et l'historique des enchères.
    /// </summary>
    Bid? DecideBid(Seat seat, IReadOnlyList<Card> hand, Auction auction);
}

public interface ICompetitiveBiddingStrategy : IBiddingStrategy
{
    AuctionCall DecideCall(Seat seat, IReadOnlyList<Card> hand, Auction auction);
}

/// <summary>
/// Interface pour les stratégies de jeu de cartes au Bridge.
/// </summary>
public interface ICardPlayStrategy
{
    /// <summary>
    /// Choisit la carte à jouer pour le siège donné.
    /// </summary>
    Card ChooseCard(ICardPlayEngine game, Seat seat);
}

/// <summary>
/// Interface pour le moteur de jeu de cartes.
/// </summary>
public interface ICardPlayEngine
{
    Seat NextToPlay { get; }
    int DeclarerTricks { get; }
    int DefenderTricks { get; }
    int TrickNumber { get; }
    bool IsComplete { get; }
    IReadOnlyList<PlayedCard> CurrentTrick { get; }
    IReadOnlyList<PlayedCard> PlayedCards { get; }
    IReadOnlyList<AuctionCall> AuctionCalls { get; }

    IReadOnlyList<Card> Hand(Seat seat);
    IReadOnlyList<Card> LegalCards(Seat seat);
    Seat Play(Seat seat, Card card);
    Suit? GetTrumpSuit();
    Seat GetDeclarer();
}
