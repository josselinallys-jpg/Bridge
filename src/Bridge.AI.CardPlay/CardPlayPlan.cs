using Bridge.Core.Models;
using Bridge.Core.Strategies;

namespace Bridge.AI.CardPlay;

internal sealed record CardPlayPlan(
    int RequiredTricks,
    int SureWinners,
    Suit? DevelopmentSuit,
    bool ContractAssured,
    bool ShortHandRuffAvailable,
    Suit? OpeningLeadSuit)
{
    public static CardPlayPlan Build(ICardPlayEngine game, Seat seat, CardKnowledge knowledge)
    {
        var trump = game.GetTrumpSuit();
        var required = game.AuctionCalls.LastOrDefault(call => call.Bid.HasValue).Bid?.Level + 6 ?? 7;
        var cards = game.Hand(seat).Concat(game.Hand(seat.Partner())).ToList();
        var sureWinners = CountSureWinners(cards);
        var developmentSuit = cards
            .Where(card => !trump.HasValue || card.Suit != trump.Value)
            .GroupBy(card => card.Suit)
            .OrderByDescending(group => group.Count() * 5 +
                group.Count(card => card.Rank >= Rank.Queen) * 2 +
                knowledge.MinimumLength(seat, group.Key))
            .Select(group => (Suit?)group.Key)
            .FirstOrDefault();
        var ownTrumps = trump.HasValue ? game.Hand(seat).Count(card => card.Suit == trump.Value) : 0;
        var partnerTrumps = trump.HasValue ? game.Hand(seat.Partner()).Count(card => card.Suit == trump.Value) : 0;
        var shortHandRuff = trump.HasValue && partnerTrumps > ownTrumps &&
            game.Hand(seat).GroupBy(card => card.Suit).Any(group =>
                group.Key != trump.Value && group.Count() == 1 &&
                game.Hand(seat.Partner()).Count(card => card.Suit == group.Key) >= 2);

        return new(required, sureWinners, developmentSuit, sureWinners >= required,
            shortHandRuff, knowledge.OpeningLeadSuit);
    }

    private static int CountSureWinners(IReadOnlyList<Card> cards)
    {
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
}