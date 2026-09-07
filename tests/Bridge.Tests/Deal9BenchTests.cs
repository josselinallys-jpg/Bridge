using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

public sealed class Deal9BenchTests
{
    [Fact]
    public void Deal9_ReferenceAuction_IsMatched()
    {
        var hands = new Dictionary<Seat, List<Card>>
        {
            [Seat.North] = Cards("S9", "S3", "HJ", "H8", "DA", "D9", "D5", "D2", "CJ", "C8", "C4", "C3", "C2"),
            [Seat.East] = Cards("SK", "SQ", "S8", "HQ", "HT", "H5", "D T", "D8", "D6", "D4", "CT", "C7", "C5"),
            [Seat.South] = Cards("SA", "SJ", "ST", "S2", "HA", "HK", "H6", "DQ", "D7", "D3", "CA", "CK", "CQ"),
            [Seat.West] = Cards("S7", "S6", "S5", "S4", "H9", "H7", "H4", "H3", "H2", "DK", "DJ", "C9", "C6")
        };

        var expected = new[] { "P", "P", "2C", "P", "2D", "P", "2NT", "P", "3NT", "P", "P", "P" };
        var engine = new SefBiddingEngine();
        var auction = new Auction();
        var seat = Seat.North;

        while (!auction.IsComplete)
        {
            var call = engine.DecideCall(seat, hands[seat], auction);
            auction.AddCall(call);
            seat = seat.Next();
        }

        var actual = auction.Calls.Select(FormatCall).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Deal10_StickyResponseAndThreeCardFit_AreMatched()
    {
        var hands = new Dictionary<Seat, List<Card>>
        {
            [Seat.North] = Cards("SQ", "S5", "S3", "HJ", "HT", "H9", "DA", "D5", "D2", "CA", "CQ", "CT", "C4"),
            [Seat.East] = Cards("SJ", "S7", "HA", "HK", "HQ", "H8", "H5", "DT", "D9", "D4", "CJ", "C8", "C7"),
            [Seat.South] = Cards("SA", "SK", "S8", "S6", "S2", "H7", "H6", "H3", "H2", "DK", "DJ", "D6", "C6"),
            [Seat.West] = Cards("ST", "S9", "S4", "H4", "DQ", "D8", "D7", "D3", "CK", "C9", "C5", "C3", "C2")
        };
        var expected = new[] { "P", "P", "P", "1C", "1H", "1S", "P", "2S", "P", "4S", "P", "P", "P" };
        var engine = new SefBiddingEngine();
        var auction = new Auction();
        var seat = Seat.East;

        while (!auction.IsComplete)
        {
            var call = engine.DecideCall(seat, hands[seat], auction);
            auction.AddCall(call);
            seat = seat.Next();
        }

        Assert.Equal(expected, auction.Calls.Select(FormatCall).ToArray());
    }

    private static string FormatCall(AuctionCall call) => call.Action == AuctionAction.Pass
        ? "P"
        : call.Bid?.ToString() switch
        {
            "1♣" => "1C",
            "1♥" => "1H",
            "1♠" => "1S",
            "2♠" => "2S",
            "2♣" => "2C",
            "2♦" => "2D",
            "4♠" => "4S",
            "2SA" => "2NT",
            "3SA" => "3NT",
            var bid => bid ?? call.Action.ToString()
        };

    private static List<Card> Cards(params string[] values) => values.Select(Parse).ToList();

    private static Card Parse(string value)
    {
        value = value.Replace(" ", string.Empty);
        var suit = value[0] switch
        {
            'C' => Suit.Clubs,
            'D' => Suit.Diamonds,
            'H' => Suit.Hearts,
            'S' => Suit.Spades,
            _ => throw new ArgumentException(value)
        };
        var rank = value[1..] switch
        {
            "A" => Rank.Ace,
            "K" => Rank.King,
            "Q" => Rank.Queen,
            "J" => Rank.Jack,
            "T" or "10" => Rank.Ten,
            _ => (Rank)int.Parse(value[1..])
        };
        return new Card(suit, rank);
    }
}