namespace Bridge.AI.Bidding;

using Bridge.Core.Logging;
using Bridge.Core.Models;
using Bridge.Core.Strategies;

/// <summary>
/// Façade historique conservée pour compatibilité. La stratégie appliquée est le SEF.
/// </summary>
public sealed class FifthMajorBiddingEngine : ICompetitiveBiddingStrategy
{
    private readonly SefBiddingEngine _sef;

    public FifthMajorBiddingEngine(ILogger? logger = null) => _sef = new SefBiddingEngine(logger);

    public Bid? DecideBid(Seat seat, IReadOnlyList<Card> hand, Auction auction) =>
        _sef.DecideBid(seat, hand, auction);

    public AuctionCall DecideCall(Seat seat, IReadOnlyList<Card> hand, Auction auction) =>
        _sef.DecideCall(seat, hand, auction);
}
