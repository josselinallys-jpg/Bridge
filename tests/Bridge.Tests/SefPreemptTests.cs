using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Chapitre SEF : les ouvertures de barrage.</summary>
public sealed class SefPreemptTests
{
    private readonly SefBiddingEngine _engine = new();

    // --- Conditions d'ouverture ---

    [Fact]
    public void Opening_WeakTwoWithTwoSideKings_IsRejected()
    {
        // Six piques mais deux gros honneurs annexes : pas de 2 faible, et 8H trop faible pour 1♠.
        var hand = Cards(
            "SQ", "SJ", "S9", "S7", "S4", "S2",
            "HK", "H3", "DK", "D6", "D2", "C8", "C3");

        Assert.Null(_engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_SolidSevenCardMinor_IsGamblingThreeNoTrump()
    {
        var hand = Cards(
            "CA", "CK", "CQ", "C8", "C6", "C5", "C2",
            "D7", "D4", "D2",
            "H8", "H3", "S9");

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_SevenCardSuitWithoutTopHonors_IsThreeLevelPreempt()
    {
        var hand = Cards(
            "SK", "SQ", "SJ", "S9", "S7", "S4", "S2",
            "H8", "D10", "D4", "D2", "C9", "C5");

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_EightCardMinor_IsFourLevelPreempt()
    {
        var hand = Cards(
            "CQ", "C10", "C9", "C8", "C6", "C4", "C3", "C2",
            "HA", "H5", "DQ", "D4", "S6");

        Assert.Equal(new Bid(4, Denomination.Clubs),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_PreemptWithFourCardSideMajor_IsRejected()
    {
        var hand = Cards(
            "S10", "S9", "S8", "S7", "S6", "S4", "S2",
            "HQ", "HJ", "H9", "H8", "D7", "C2");

        Assert.Null(_engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_SevenCardSuitWithoutTwoHonors_IsNotPreempted()
    {
        var hand = Cards(
            "SK", "S9", "S8", "S7", "S6", "S4", "S2",
            "HQ", "HJ", "D7", "D4", "C8", "C3");

        Assert.Null(_engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_PreemptWithTwoSideBigHonors_IsRejected()
    {
        var hand = Cards(
            "S10", "S9", "S8", "S7", "S6", "S4", "S2",
            "HA", "HK", "H5", "H4", "D7", "C2");

        Assert.Null(_engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void GamblingThreeNoTrump_FourClubsRequest_CorrectsToDiamonds()
    {
        var opener = Cards(
            "DA", "DK", "DQ", "D9", "D8", "D7", "D6",
            "C5", "C4", "H8", "H3", "S9", "S2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.NoTrump)),
            (Seat.South, new Bid(4, Denomination.Clubs)));

        Assert.Equal(new Bid(4, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void GamblingThreeNoTrump_FourClubsRequest_PassesWithClubs()
    {
        var opener = Cards(
            "CA", "CK", "CQ", "C9", "C8", "C7", "C6",
            "D5", "D4", "H8", "S9", "S7", "S2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.NoTrump)),
            (Seat.South, new Bid(4, Denomination.Clubs)));

        Assert.Null(_engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void GamblingThreeNoTrump_FiveClubsRequest_CorrectsToDiamonds()
    {
        var opener = Cards(
            "DA", "DK", "DQ", "D9", "D8", "D7", "D6",
            "C5", "C4", "H8", "H3", "S9", "S2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.NoTrump)),
            (Seat.South, new Bid(5, Denomination.Clubs)));

        Assert.Equal(new Bid(5, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void GamblingThreeNoTrump_FourDiamondsRequest_ShowsSingleton()
    {
        var opener = Cards(
            "CA", "CK", "CQ", "C9", "C8", "C7", "C6",
            "D5", "D4", "H8", "S9", "S7", "S2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.NoTrump)),
            (Seat.South, new Bid(4, Denomination.Diamonds)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void GamblingThreeNoTrump_FourDiamondsRequestWithoutSingleton_BidsFourNoTrump()
    {
        var opener = Cards(
            "CA", "CK", "CQ", "C9", "C8", "C7", "C6",
            "D5", "D4", "H8", "H3", "S9", "S2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.NoTrump)),
            (Seat.South, new Bid(4, Denomination.Diamonds)));

        Assert.Equal(new Bid(4, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Theory]
    [InlineData(Denomination.Hearts)]
    [InlineData(Denomination.Spades)]
    public void GamblingThreeNoTrump_FormalStop_Passes(Denomination stop)
    {
        var opener = Cards(
            "CA", "CK", "CQ", "C9", "C8", "C7", "C6",
            "D5", "D4", "H8", "H3", "S9", "S2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.NoTrump)),
            (Seat.South, new Bid(4, stop)));

        Assert.Null(_engine.DecideBid(Seat.North, opener, auction));
    }

    // --- Réponses au 2 faible ---

    [Fact]
    public void WeakTwo_ThreeCardSupport_ExtendsBarrageToThree()
    {
        var responder = Cards(
            "S9", "S7", "S3",
            "HK", "H9", "H4",
            "DA", "D10", "D6", "D5",
            "CQ", "C10", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void WeakTwo_FourCardSupport_ExtendsBarrageToFour()
    {
        var responder = Cards(
            "S9", "S7", "S5", "S3",
            "HK", "H9", "H4",
            "DA", "D10", "D6",
            "CQ", "C10", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void WeakTwo_AfterIntervention_WithGameInterest_UsesTwoNoTrumpRelay()
    {
        var responder = Cards(
            "SA", "SK", "S7", "S4",
            "HA", "HK", "H9", "H4",
            "D10", "D6", "D3",
            "C8", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Hearts)),
            (Seat.East, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ThreeLevelPreempt_AfterIntervention_WithStrongDefense_Doubles()
    {
        var responder = Cards(
            "SA", "SK", "SQ", "SJ",
            "HA", "HK", "HQ", "HJ",
            "DA", "DK", "DQ",
            "C8", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.Clubs)),
            (Seat.East, new Bid(3, Denomination.Diamonds)));

        Assert.Equal(AuctionAction.Double,
            _engine.DecideCall(Seat.South, responder, auction).Action);
    }

    [Fact]
    public void WeakTwo_FifteenHld_BidsForcingTwoNoTrump()
    {
        var responder = Cards(
            "SA", "S7",
            "HK", "HQ", "H9", "H4",
            "DA", "D10", "D6",
            "CQ", "CJ", "C10", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void WeakTwo_NoFitDoesNotCountDistributionPointsForRelay()
    {
        var responder = Cards(
            "CA", "CK", "C9", "C8", "C7", "C6", "C4", "C2",
            "DQ", "D6", "D4", "D2",
            "HJ");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Relais 2SA : redemandes de l'ouvreur ---

    [Fact]
    public void WeakTwoRebid_MinimumRepeatsSuit()
    {
        var opener = Cards(
            "SA", "SK", "S9", "S7", "S4", "S2",
            "H8", "H3", "D9", "D6", "D2", "C8", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void WeakTwoRebid_MaximumShowsSideKing()
    {
        var opener = Cards(
            "SA", "SQ", "SJ", "S9", "S4", "S2",
            "HK", "H3", "D9", "D6", "D2", "C8", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void WeakTwoRebid_MaximumWithoutSideHonorShowsSingletonAtFourLevel()
    {
        var opener = Cards(
            "SA", "SK", "SJ", "S9", "S7", "S2",
            "H2",
            "DQ", "D6", "D3",
            "C9", "C7", "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void WeakTwo_ResponderConcludesGameAfterMaximumRebid()
    {
        var responder = Cards(
            "SA", "S7",
            "HK", "HQ", "H9", "H4",
            "DA", "D10", "D6",
            "CQ", "CJ", "C10", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)),
            (Seat.North, new Bid(3, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Barrage au palier 3 ---

    [Fact]
    public void ThreeLevelPreempt_ThreeCardSupport_ExtendsToGame()
    {
        // Loi des atouts : dix atouts dans la ligne.
        var responder = Cards(
            "S9", "S7", "S3",
            "HK", "H9", "H4",
            "DA", "D10", "D6", "D5",
            "CQ", "C10", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(3, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ThreeLevelPreempt_BalancedSmallFit_BidsThreeNoTrump()
    {
        var responder = Cards(
            "SA", "SQ", "S8", "S3",
            "HK", "H8", "H3",
            "DK", "D8", "D3",
            "C8", "C6", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(3, Denomination.Clubs)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ThreeLevelPreempt_FiveCardMajorWithGameValues_IsForcingChange()
    {
        var responder = Cards(
            "H10", "H9", "H8", "H7", "H6", "H2",
            "SA", "SK", "SQ", "SJ",
            "DK", "D3", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(3, Denomination.Clubs)));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ThreeLevelPreemptOpener_WithTwoCardMajorFit_SupportsAtFour()
    {
        var opener = Cards(
            "CA", "CK", "CQ", "CJ", "C10", "C9", "C8",
            "HK", "H2", "D8", "D4", "S7", "S5");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.Clubs)),
            (Seat.South, new Bid(3, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void ThreeLevelPreemptOpener_WithoutMajorFit_StopsAtThreeNoTrump()
    {
        var opener = Cards(
            "CA", "CK", "CQ", "CJ", "C10", "C9", "C8",
            "HQ", "DA", "D7", "D2", "SK", "S8");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.Clubs)),
            (Seat.South, new Bid(3, Denomination.Hearts)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    // --- Aides ---

    private static List<Card> Cards(params string[] cards) => cards.Select(Parse).ToList();

    private static Auction AuctionWithBids(params (Seat Seat, Bid Bid)[] bids)
    {
        var auction = new Auction();
        foreach (var (seat, bid) in bids)
        {
            while (auction.Calls.Count % 4 != (int)seat)
                auction.AddCall((Seat)(auction.Calls.Count % 4), null);
            auction.AddCall(seat, bid);
        }
        var nextPartner = bids[^1].Seat.Partner();
        while (auction.Calls.Count % 4 != (int)nextPartner)
            auction.AddCall((Seat)(auction.Calls.Count % 4), null);
        return auction;
    }

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
