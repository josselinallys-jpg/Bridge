using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Interventions et réveils SEF.</summary>
public sealed class SefCompetitiveTests
{
    private readonly SefBiddingEngine _engine = new();

    [Fact]
    public void Intervention_OneNoTrumpWithSixteenPointsAndGuard_BidsOneNoTrump()
    {
        var hand = Cards(
            "SA", "S6", "S4",
            "HK", "HQ", "H4",
            "DA", "D8", "D3",
            "CK", "C4", "C3", "C2");
        var auction = AuctionAfterOpponent(new Bid(1, Denomination.Hearts));

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void Intervention_SixCardRobustSuitAtLevelTwo_BidsTwoSpades()
    {
        var hand = Cards(
            "SK", "SQ", "SJ", "S9", "S7", "S4",
            "H8", "H3", "D6", "D2", "C8", "C5", "C3");
        var auction = AuctionAfterOpponent(new Bid(1, Denomination.Diamonds));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void Intervention_WeakFiveCardSuit_Passes()
    {
        var hand = Cards(
            "S9", "S7", "S4", "S2",
            "HK", "H6", "H3",
            "D8", "D5", "D2",
            "C9", "C6", "C3");
        var auction = AuctionAfterOpponent(new Bid(1, Denomination.Diamonds));

        Assert.Null(_engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void Reopening_AfterTwoPasses_WithFiveCardSuit_BidsOneSpade()
    {
        var hand = Cards(
            "SK", "SQ", "S8", "S5", "S3",
            "HK", "H4", "H2",
            "D9", "D6", "D3",
            "C8", "C2");
        var auction = AuctionAfterTwoPasses(new Bid(1, Denomination.Diamonds));

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, auction));
    }

    [Fact]
    public void Reopening_AfterTwoPasses_WithTenPoints_CanDouble()
    {
        var hand = Cards(
            "SA", "SK", "S4",
            "HK", "HQ", "H4",
            "D8", "D5", "D2",
            "C9", "C6", "C3", "C2");
        var auction = AuctionAfterTwoPasses(new Bid(1, Denomination.Hearts));

        Assert.Equal(AuctionAction.Double,
            _engine.DecideCall(Seat.North, hand, auction).Action);
    }

    [Fact]
    public void TakeoutDouble_PartnerWithFourCardMajor_AnswersOneHeart()
    {
        var hand = Cards(
            "HK", "HQ", "H7", "H4",
            "S7", "S6", "S3",
            "D8", "D5", "D2",
            "C9", "C6", "C3");
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.Diamonds));
        auction.AddCall(Seat.South, AuctionAction.Double);
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(1, Denomination.Hearts),
            _engine.DecideBid(Seat.North, hand, auction));
    }

    [Fact]
    public void Landy_PartnerWithFiveSpades_AnswersTwoSpades()
    {
        var hand = Cards(
            "SK", "SQ", "S8", "S5", "S3",
            "H7", "H4", "H2",
            "D9", "D6", "D3",
            "C8", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.South, new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, auction));
    }

    [Fact]
    public void InterventionResponse_ThreeCardSupport_RaisesPartnerSuit()
    {
        var hand = Cards(
            "SK", "S7", "S3",
            "HQ", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C6", "C4", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.Diamonds));
        auction.AddCall(Seat.South, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, auction));
    }

    [Fact]
    public void InterventionResponse_ThreeCardSupportDoesNotRaiseSixLevelContract()
    {
        var hand = Cards(
            "S9", "S6", "S3",
            "HQ", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C6", "C4", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Clubs));
        auction.AddCall(Seat.East, new Bid(6, Denomination.Hearts));

        Assert.Null(_engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void MinorOpeningIntervention_OneOverOne_RequiresFourCardsAndSevenHl()
    {
        var hand = Cards(
            "SK", "S8", "S4", "S2",
            "HA", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C6", "C4");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Clubs), new Bid(1, Denomination.Hearts));

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningIntervention_TwoOverOne_RequiresFiveCardsAndElevenHl()
    {
        var hand = Cards(
            "SA", "SK", "S8", "S5", "S2",
            "HA", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C4");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Clubs), new Bid(2, Denomination.Hearts));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningIntervention_FiveCardSupport_UsesTwoOfMinorWithSixToEightH()
    {
        var hand = Cards(
            "SK", "S6", "S3",
            "HQ", "HJ",
            "D9", "D5", "D2",
            "C8", "C6", "C4", "C3", "C2");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Clubs), new Bid(1, Denomination.Hearts));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningIntervention_DoubleAfterOneDiamond_DeniesFourCardMajor()
    {
        var hand = Cards(
            "SA", "S7", "S3",
            "HQ", "H6", "H3",
            "DJ", "D5", "D2",
            "CJ", "C6", "C4", "C2");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Clubs), new Bid(1, Denomination.Diamonds));

        Assert.Equal(AuctionAction.Double,
            _engine.DecideCall(Seat.West, hand, auction).Action);
    }

    [Fact]
    public void Intervention_WeakNorthAfterWestOneClub_Passes()
    {
        var hand = Cards(
            "SK",
            "HA", "H8",
            "DK", "D8", "D5",
            "CQ", "C10", "C8", "C7", "C5", "C4", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.West, new Bid(1, Denomination.Clubs));

        Assert.Equal(AuctionAction.Pass,
            _engine.DecideCall(Seat.North, hand, auction).Action);
    }

    private static Auction AuctionAfterOpponent(Bid opening)
    {
        var auction = new Auction();
        auction.AddCall(Seat.East, opening);
        return auction;
    }

    private static Auction PartnerMinorWithIntervention(Bid opening, Bid intervention)
    {
        var auction = new Auction();
        auction.AddCall(Seat.East, opening);
        auction.AddCall(Seat.South, intervention);
        return auction;
    }

    private static Auction AuctionAfterTwoPasses(Bid opening)
    {
        var auction = new Auction();
        auction.AddCall(Seat.East, opening);
        auction.AddCall(Seat.South, null);
        auction.AddCall(Seat.West, null);
        return auction;
    }

    private static List<Card> Cards(params string[] cards) => cards.Select(Parse).ToList();

    private static Card Parse(string value)
    {
        var suit = value[0] switch
        {
            'C' => Suit.Clubs,
            'D' => Suit.Diamonds,
            'H' => Suit.Hearts,
            'S' => Suit.Spades,
            _ => throw new ArgumentException("Couleur inconnue.", nameof(value))
        };
        var rank = value[1..] switch
        {
            "A" => Rank.Ace,
            "K" => Rank.King,
            "Q" => Rank.Queen,
            "J" => Rank.Jack,
            "10" => Rank.Ten,
            _ => (Rank)int.Parse(value[1..])
        };
        return new Card(suit, rank);
    }
}
