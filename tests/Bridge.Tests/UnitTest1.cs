using Bridge.Core.Models;
using Bridge.Core.Logging;
using Bridge.AI.Bidding;
using Bridge.AI.CardPlay;
using Bridge.Engine;

namespace Bridge.Tests;

/// <summary>
/// Tests pour les modèles Bridge.
/// </summary>
public class ModelTests
{
    [Fact]
    public void Card_ToString_ReturnsCorrectFormat()
    {
        var card = new Card(Suit.Hearts, Rank.Ace);
        Assert.Equal("A♥", card.ToString());
    }

    [Fact]
    public void Bid_ToString_ReturnsCorrectFormat()
    {
        var bid = new Bid(1, Denomination.Hearts);
        Assert.Equal("1♥", bid.ToString());
    }

    [Fact]
    public void Seat_Partner_ReturnsCorrectPartner()
    {
        Assert.Equal(Seat.South, Seat.North.Partner());
        Assert.Equal(Seat.West, Seat.East.Partner());
    }

    [Fact]
    public void Deal_Create_Distributes52Cards()
    {
        var random = new Random(42);
        var deal = Deal.Create(random);

        int totalCards = 0;
        foreach (var seat in Enum.GetValues<Seat>())
        {
            totalCards += deal[seat].Count;
        }

        Assert.Equal(52, totalCards);
    }

    [Fact]
    public void HandEvaluation_SeparatesHonorLengthAndDistributionPoints()
    {
        var hand = new List<Card>
        {
            new(Suit.Spades, Rank.Ace), new(Suit.Spades, Rank.King), new(Suit.Spades, Rank.Five),
            new(Suit.Spades, Rank.Four), new(Suit.Spades, Rank.Three), new(Suit.Spades, Rank.Two),
            new(Suit.Hearts, Rank.Queen), new(Suit.Hearts, Rank.Two),
            new(Suit.Diamonds, Rank.Jack), new(Suit.Diamonds, Rank.Three), new(Suit.Diamonds, Rank.Two),
            new(Suit.Clubs, Rank.Four), new(Suit.Clubs, Rank.Two)
        };

        var evaluation = new HandEvaluation(hand);

        Assert.Equal(10, evaluation.HighCardPoints);
        Assert.Equal(2, evaluation.LengthPoints);
        Assert.Equal(12, evaluation.HonorLengthPoints);
        Assert.False(evaluation.IsNoTrumpOpeningShape);
    }

    [Fact]
    public void Auction_DoubleAndRedouble_EnforceOpposingSidesAndReachContract()
    {
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));

        Assert.False(auction.CanDouble(Seat.South));
        Assert.True(auction.CanDouble(Seat.East));

        auction.AddCall(Seat.East, AuctionAction.Double);
        Assert.False(auction.CanRedouble(Seat.West));
        Assert.True(auction.CanRedouble(Seat.South));

        auction.AddCall(Seat.South, AuctionAction.Redouble);
        auction.AddCall(Seat.West, null);
        auction.AddCall(Seat.North, null);
        auction.AddCall(Seat.East, null);

        var contract = Assert.IsType<Contract>(auction.GetContract());
        Assert.Equal(Doubling.Redoubled, contract.Doubling);
        Assert.Equal("1♠ XX par North", contract.ToString());
    }

    [Fact]
    public void Auction_NewBid_CancelsPreviousDouble()
    {
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Hearts));
        auction.AddCall(Seat.East, AuctionAction.Double);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Hearts));
        auction.AddCall(Seat.West, null);
        auction.AddCall(Seat.North, null);
        auction.AddCall(Seat.East, null);

        Assert.Equal(Doubling.None, auction.GetContract()!.Value.Doubling);
    }

    [Theory]
    [InlineData(Doubling.None, 7, 80)]
    [InlineData(Doubling.Doubled, 7, 160)]
    [InlineData(Doubling.Redoubled, 7, 520)]
    [InlineData(Doubling.Doubled, 8, 260)]
    public void BridgeScoring_MadeContract_AppliesMultiplierAndBonuses(
        Doubling doubling, int tricks, int expectedScore)
    {
        var contract = new Contract(new Bid(1, Denomination.Spades), Seat.North, doubling);

        Assert.Equal(expectedScore, BridgeScoring.Score(contract, tricks));
    }

    [Theory]
    [InlineData(Doubling.None, 5, 100)]
    [InlineData(Doubling.Doubled, 5, 300)]
    [InlineData(Doubling.Redoubled, 5, 600)]
    public void BridgeScoring_DefeatedContract_AppliesUndertrickPenalty(
        Doubling doubling, int tricks, int expectedPenalty)
    {
        var contract = new Contract(new Bid(1, Denomination.Spades), Seat.North, doubling);

        Assert.Equal(expectedPenalty, BridgeScoring.Score(contract, tricks));
    }

    [Theory]
    [InlineData(1, Vulnerability.None)]
    [InlineData(2, Vulnerability.NorthSouth)]
    [InlineData(3, Vulnerability.EastWest)]
    [InlineData(4, Vulnerability.Both)]
    [InlineData(17, Vulnerability.None)]
    public void Vulnerability_ForBoard_FollowsOfficialCycle(int board, Vulnerability expected)
    {
        Assert.Equal(expected, VulnerabilityExtensions.ForBoard(board));
    }

    [Fact]
    public void BridgeScoring_VulnerableGame_UsesFiveHundredPointBonus()
    {
        var contract = new Contract(new Bid(4, Denomination.Hearts), Seat.North);

        Assert.Equal(420, BridgeScoring.Score(contract, 10, Vulnerability.None));
        Assert.Equal(620, BridgeScoring.Score(contract, 10, Vulnerability.NorthSouth));
    }

    [Fact]
    public void BridgeScoring_VulnerableDoubledUndertricks_UseVulnerableSchedule()
    {
        var contract = new Contract(new Bid(4, Denomination.Hearts), Seat.North, Doubling.Doubled);

        Assert.Equal(500, BridgeScoring.Score(contract, 7, Vulnerability.None));
        Assert.Equal(800, BridgeScoring.Score(contract, 7, Vulnerability.NorthSouth));
    }
}

/// <summary>
/// Tests pour l'IA d'enchères Fifth Major.
/// </summary>
public class FifthMajorBiddingTests
{
    private readonly FifthMajorBiddingEngine _engine;

    public FifthMajorBiddingTests()
    {
        _engine = new FifthMajorBiddingEngine();
    }

    [Fact]
    public void FifthMajor_WeakHand_ReturnsNull()
    {
        var hand = new List<Card>
        {
            new(Suit.Clubs, Rank.Two),
            new(Suit.Clubs, Rank.Three),
            new(Suit.Clubs, Rank.Four),
            new(Suit.Clubs, Rank.Five),
            new(Suit.Clubs, Rank.Six),
            new(Suit.Diamonds, Rank.Two),
            new(Suit.Diamonds, Rank.Three),
            new(Suit.Diamonds, Rank.Four),
            new(Suit.Hearts, Rank.Two),
            new(Suit.Hearts, Rank.Three),
            new(Suit.Spades, Rank.Two),
            new(Suit.Spades, Rank.Three),
            new(Suit.Spades, Rank.Four)
        };
        var auction = new Auction();

        var bid = _engine.DecideBid(Seat.North, hand, auction);

        Assert.Null(bid);
    }
}