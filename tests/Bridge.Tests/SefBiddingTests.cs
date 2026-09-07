using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

public sealed class SefBiddingTests
{
    private readonly SefBiddingEngine _engine = new();

    [Fact]
    public void Opening_BalancedFifteenPoints_IsOneNoTrump()
    {
        var hand = Cards(
            "SA", "SK", "S7", "S2",
            "HK", "HQ", "H4",
            "DK", "D8", "D3",
            "CQ", "C9", "C5");

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_SixCardMajorAndSevenPoints_IsWeakTwo()
    {
        var hand = Cards(
            "SA", "SK", "S9", "S7", "S4", "S2",
            "H8", "H3", "D9", "D6", "D2", "C8", "C3");

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_SixFourMajors_DoesNotUseWeakTwo()
    {
        var hand = Cards(
            "SA", "SK", "S9", "S7", "S4", "S2",
            "HA", "H8", "H3", "H2",
            "D9", "D6", "C4");

        Assert.NotEqual(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_BalancedTwentyOnePoints_IsTwoNoTrump()
    {
        var hand = Cards(
            "SA", "SK", "SQ", "S2",
            "HK", "HQ", "HJ",
            "DA", "D8", "D3",
            "CQ", "C5", "C2");

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void OneNoTrump_Stayman_FindsFourCardSpadeSuit()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S2",
            "HK", "HQ", "H4",
            "DK", "D8", "D3",
            "CQ", "C9", "C5");
        var responder = Cards(
            "SQ", "S8", "S3",
            "HA", "H9", "H7", "H4",
            "DQ", "D6", "D2",
            "C8", "C5", "C3");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);

        var stayman = _engine.DecideBid(Seat.South, responder, auction);
        Assert.Equal(new Bid(2, Denomination.Clubs), stayman);
        auction.AddCall(Seat.South, stayman);
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OneNoTrump_HeartTexas_IsRectifiedByOpener()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S2",
            "HK", "HQ", "H4",
            "DK", "D8", "D3",
            "CQ", "C9", "C5");
        var responder = Cards(
            "S8", "S3",
            "HQ", "HJ", "H9", "H7", "H4",
            "D9", "D6", "D2",
            "C8", "C5", "C3");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);

        var texas = _engine.DecideBid(Seat.South, responder, auction);
        Assert.Equal(new Bid(2, Denomination.Diamonds), texas);
        auction.AddCall(Seat.South, texas);
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void MinorOpening_WeakZoneResponseNamesFourCardMajorBeforeLongerDiamonds()
    {
        var responder = Cards(
            "SK", "S8", "S5", "S2",
            "H7", "H3",
            "DQ", "DJ", "D9", "D6", "D2",
            "C8", "C3");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Clubs));
        auction.AddCall(Seat.East, null);

        // SEF : en zone 6-12HL, la majeure quatrième prime sur une mineure plus longue.
        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Opening_BalancedTwentyFourPoints_IsGameForcingTwoDiamonds()
    {
        var hand = Cards(
            "SA", "SK", "SQ", "SJ",
            "HA", "HK", "H4",
            "DA", "DK", "D3",
            "CQ", "C5", "C2");

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void TwoNoTrump_Stayman_UsesThreeLevelFourResponses()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "S2",
            "HA", "HK", "H4",
            "DQ", "D8", "D3",
            "CJ", "C5", "C2");
        var responder = Cards(
            "S9", "S6", "S3",
            "HQ", "H9", "H7", "H4",
            "DK", "D6", "D2",
            "C8", "C5", "C3");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(2, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);

        var stayman = _engine.DecideBid(Seat.South, responder, auction);
        Assert.Equal(new Bid(3, Denomination.Clubs), stayman);
        auction.AddCall(Seat.South, stayman);
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void StrongTwoClubs_NoTrumpRebid_ThreeClubsIsStaymanNotClubFit()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "S2",
            "HA", "HK", "H4",
            "DA", "D8", "D3",
            "CQ", "C5", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void StrongTwoClubs_StaymanWithoutMajor_ResponderBidsGameInNoTrump()
    {
        var responder = Cards(
            "S9", "S6", "S3",
            "HQ", "H9", "H7", "H4",
            "D9", "D6", "D2",
            "C8", "C5", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)),
            (Seat.North, new Bid(3, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubs_StaymanWithoutMajor_ResponderBidsSlamWithThirtyThreePoints()
    {
        var responder = Cards(
            "SA", "S6", "S3",
            "HQ", "H9", "H7", "H4",
            "DK", "D6", "D2",
            "CQ", "C5", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)),
            (Seat.North, new Bid(3, Denomination.Diamonds)));

        Assert.Equal(new Bid(6, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubs_StaymanWithMajor_ResponderBidsGameInTheFit()
    {
        var responder = Cards(
            "S9", "S6", "S3",
            "HQ", "H9", "H7", "H4",
            "D9", "D6", "D2",
            "C8", "C5", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)),
            (Seat.North, new Bid(3, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubs_Texas_OpenerRectifiesThenResponderBidsGame()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "S2",
            "HA", "HK", "H4",
            "DA", "DK", "D3",
            "CA", "C5", "C2");
        var responder = Cards(
            "S9", "S6", "S3",
            "HQ", "HJ", "H9", "H7", "H4",
            "D9", "D6", "D2",
            "C8", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Diamonds)));

        var rectification = _engine.DecideBid(Seat.North, opener, auction);
        Assert.Equal(new Bid(3, Denomination.Hearts), rectification);
        AddRound(auction, Seat.North, rectification!.Value);

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Roudi_AsksThenShowsMinimumThreeCardSupport()
    {
        var responder = Cards(
            "SA", "SK", "SQ", "S7", "S2",
            "HA", "H8", "H3",
            "D7", "D4", "D2", "C8", "C3");
        var opener = Cards(
            "S9", "S6", "S3",
            "HK", "HQ", "H4", "H2",
            "DA", "D8", "D3",
            "CK", "C5", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(1, Denomination.NoTrump)));

        var roudi = _engine.DecideBid(Seat.South, responder, auction);
        Assert.Equal(new Bid(2, Denomination.Clubs), roudi);
        AddRound(auction, Seat.South, roudi!.Value);

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
        AddRound(auction, Seat.North, new Bid(2, Denomination.Spades));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void FourthSuitForcing_AsksThenOpenerShowsThreeCardSupport()
    {
        var responder = Cards(
            "SA", "S8", "S3",
            "HK", "HQ", "H9", "H7", "H4",
            "DA", "D6", "D2", "C8", "C3");
        var opener = Cards(
            "SK", "SQ", "S8", "S4",
            "HA", "H6", "H2",
            "DK", "DQ", "D9", "D5", "D3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Diamonds)),
            (Seat.South, new Bid(1, Denomination.Hearts)),
            (Seat.North, new Bid(1, Denomination.Spades)));

        var fourthSuit = _engine.DecideBid(Seat.South, responder, auction);
        Assert.Equal(new Bid(2, Denomination.Clubs), fourthSuit);
        AddRound(auction, Seat.South, fourthSuit!.Value);

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
        AddRound(auction, Seat.North, new Bid(2, Denomination.Hearts));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void BlackwoodFiveKeys_UsesThirtyFortyOneAndConcludesSmallSlam()
    {
        var asker = Cards(
            "SA", "SK", "SQ",
            "HA", "HK", "H7", "H4",
            "DA", "DK", "D2",
            "CQ", "C5", "C3");
        var responder = Cards(
            "S9", "S6", "S3",
            "HK", "HQ", "H8", "H5", "H2",
            "D9", "D4", "D3",
            "CA", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Hearts)),
            (Seat.South, new Bid(2, Denomination.Hearts)),
            (Seat.North, new Bid(3, Denomination.Hearts)));

        var blackwood = _engine.DecideBid(Seat.South, asker, auction);
        Assert.Equal(new Bid(4, Denomination.NoTrump), blackwood);
        AddRound(auction, Seat.South, blackwood!.Value);

        var keyResponse = _engine.DecideBid(Seat.North, responder, auction);
        Assert.Equal(new Bid(5, Denomination.Spades), keyResponse);
        AddRound(auction, Seat.North, keyResponse!.Value);

        Assert.Equal(new Bid(6, Denomination.Hearts),
            _engine.DecideBid(Seat.South, asker, auction));
    }

    [Fact]
    public void StaymanContinuation_NeverConcludesGrandSlamAutomatically()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "SJ",
            "HA", "HK", "HQ", "H4",
            "DA", "DK", "D2",
            "CA", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Hearts)));

        var response = _engine.DecideBid(Seat.South, opener, auction);

        Assert.NotEqual(new Bid(7, Denomination.Hearts), response);
        Assert.NotEqual(new Bid(7, Denomination.Spades), response);
    }

    [Fact]
    public void TexasToMajorGame_DoesNotLaunchBlackwoodAutomatically()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "SJ",
            "HA", "HK",
            "DA", "DQ",
            "C9", "C7", "C5", "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Diamonds)),
            (Seat.North, new Bid(3, Denomination.Hearts)),
            (Seat.South, new Bid(4, Denomination.Hearts)));

        Assert.Null(_engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void BlackwoodFiveKeys_EncodesAllThirtyFortyOneResponses()
    {
        AssertBlackwoodResponse(new Bid(5, Denomination.Clubs));
        AssertBlackwoodResponse(new Bid(5, Denomination.Diamonds), "CA");
        AssertBlackwoodResponse(new Bid(5, Denomination.Hearts), "CA", "DA");
        AssertBlackwoodResponse(new Bid(5, Denomination.Spades), "CA", "HK", "HQ");
        AssertBlackwoodResponse(new Bid(5, Denomination.Clubs), "CA", "DA", "SA");
        AssertBlackwoodResponse(new Bid(5, Denomination.Diamonds), "CA", "DA", "HA", "SA");
        AssertBlackwoodResponse(new Bid(5, Denomination.Hearts), "CA", "DA", "HA", "SA", "HK");
    }

    [Fact]
    public void BlackwoodFiveKeys_ShowsUsefulVoid()
    {
        AssertBlackwoodResponse(new Bid(5, Denomination.NoTrump),
            "CA", "C9", "C8", "C7",
            "HK", "HQ", "H5", "H2",
            "S9", "S8", "S7", "S5", "S2");
        AssertBlackwoodResponse(new Bid(6, Denomination.Clubs),
            "HK", "HQ", "H5", "H2",
            "S9", "S8", "S7", "S5", "S2",
            "D9", "D8", "D5", "D2");
    }

    [Fact]
    public void BlackwoodFiveKeys_RespondsAfterOpponentInterventionAndDirectFit()
    {
        var responder = Cards(
            "S9", "S6", "S3",
            "HK", "HQ", "H8", "H5", "H2",
            "D9", "D4", "D3",
            "CA", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Hearts));
        auction.AddCall(Seat.East, new Bid(2, Denomination.Diamonds));
        auction.AddCall(Seat.South, new Bid(3, Denomination.Hearts));
        auction.AddCall(Seat.West, null);
        auction.AddCall(Seat.North, new Bid(4, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);

        Assert.Equal(new Bid(5, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void BlackwoodResponseAtSix_DoesNotForceGrandSlam()
    {
        var hand = Cards(
            "S9", "S6", "S3",
            "HK", "H8", "H5",
            "D9", "D4", "D3",
            "CA", "C2", "C4", "C5");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Hearts)),
            (Seat.South, new Bid(2, Denomination.Hearts)),
            (Seat.North, new Bid(4, Denomination.NoTrump)),
            (Seat.South, new Bid(6, Denomination.Hearts)));

        Assert.Null(_engine.DecideBid(Seat.North, hand, auction));
    }

    [Fact]
    public void AutomatedSefAuctions_AlwaysProduceLegalTerminatingSequences()
    {
        var random = new Random(20260829);
        for (var dealIndex = 0; dealIndex < 100; dealIndex++)
        {
            var deal = Deal.Create(random);
            var auction = new Auction();
            var seat = Seat.North;
            var callCount = 0;
            while (!auction.IsComplete && callCount++ < 40)
            {
                var call = _engine.DecideCall(seat, deal[seat], auction);
                auction.AddCall(call);
                seat = seat.Next();
            }

            Assert.True(auction.IsComplete);
            Assert.True(callCount < 40);
        }
    }

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

    private static void AddRound(Auction auction, Seat seat, Bid bid)
    {
        auction.AddCall(seat, bid);
        auction.AddCall(seat.Next(), null);
    }

    private void AssertBlackwoodResponse(Bid expected, params string[] cards)
    {
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Hearts)),
            (Seat.South, new Bid(2, Denomination.Hearts)),
            (Seat.North, new Bid(3, Denomination.Hearts)),
            (Seat.South, new Bid(4, Denomination.NoTrump)));

        Assert.Equal(expected, _engine.DecideBid(Seat.North, Cards(cards), auction));
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
