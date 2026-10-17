using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Chapitre SEF : les ouvertures mineures et leurs développements.</summary>
public sealed class SefMinorOpeningTests
{
    private readonly SefBiddingEngine _engine = new();

    // --- Conditions d'ouverture ---

    [Fact]
    public void Opening_ElevenPointsThirteenHl_OpensOneDiamond()
    {
        var hand = Cards(
            "DA", "DK", "DQ", "D6", "D5", "D2",
            "HQ", "H4", "S8", "S7", "S3", "C5", "C4");

        Assert.Equal(new Bid(1, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_ElevenPointsTwelveHl_Passes()
    {
        var hand = Cards(
            "DA", "DK", "DQ", "D6", "D5",
            "HQ", "H4", "H2", "S8", "S7", "S3", "C4", "C2");

        Assert.Null(_engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_ThreeThreeMinors_OpensOneClub()
    {
        var hand = Cards(
            "SA", "SK", "S7", "S5",
            "HK", "H4", "H2",
            "DQ", "D6", "D3",
            "CJ", "C5", "C4");

        Assert.Equal(new Bid(1, Denomination.Clubs),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_FourFourMinors_OpensOneDiamond()
    {
        var hand = Cards(
            "SA", "S7",
            "HK", "H5", "H4",
            "DQ", "D8", "D6", "D2",
            "CK", "CJ", "C4", "C3");

        Assert.Equal(new Bid(1, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Response_MinorOpening_BalancedSixToTenHl_BidsOneNoTrump()
    {
        var responder = Cards(
            "SK", "S7", "S3",
            "HQ", "H7", "H3",
            "DQ", "D6", "D3",
            "C8", "C6", "C4", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_MinorOpening_BalancedElevenToTwelveHl_BidsTwoNoTrump()
    {
        var responder = Cards(
            "SK", "SQ", "S3",
            "HK", "H7", "H3",
            "DQ", "D6", "D3",
            "CJ", "C6", "C4", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_MinorOpening_BalancedThirteenToFifteenH_BidsThreeNoTrump()
    {
        var responder = Cards(
            "SA", "SK", "S3",
            "HK", "H7", "H3",
            "DQ", "D6", "D3",
            "CJ", "C6", "C4", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_DiamondOpening_IrregularLongClubsMayBidOneNoTrump()
    {
        var responder = Cards(
            "SK", "S7",
            "HQ", "H7", "H3",
            "D6", "D3",
            "CK", "C8", "C6", "C4", "C2", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Diamonds)));

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Réponses ---

    [Fact]
    public void ThirdSeatOpening_ChangeOfSuitWithoutRencontre_SameChoiceAsSilence()
    {
        var responder = Cards(
            "HK", "HQ", "H7", "H4",
            "DQ", "DJ", "D6", "D5", "D2",
            "S9", "S3", "C8", "C4");
        var auction = AuctionWithBids((Seat.South, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(1, Denomination.Hearts),
            _engine.DecideBid(Seat.North, responder, auction));
    }

    [Fact]
    public void ThirdSeatOpening_RencontreFiveCardSuitWithMinorSupport_JumpsNewSuit()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S6", "S3",
            "CK", "C8", "C6", "C4",
            "H8", "H6",
            "D8", "D6");
        var auction = AuctionWithBids((Seat.South, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, responder, auction));
    }

    [Fact]
    public void SilenceAdverse_SameRencontreHand_BidsNewSuitWithoutJump()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S6", "S3",
            "CK", "C8", "C6", "C4",
            "H8", "H6",
            "D8", "D6");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_WeakZoneFourCardMajorLongerDiamonds_BidsMajor()
    {
        var responder = Cards(
            "HK", "HQ", "H7", "H4",
            "DQ", "DJ", "D6", "D5", "D2",
            "S9", "S3", "C8", "C4");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(1, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_ThirteenHlFiveDiamondsFourHearts_BidsDiamondsFirst()
    {
        var responder = Cards(
            "HA", "HQ", "H7", "H4",
            "DK", "DQ", "DJ", "D6", "D5",
            "S9", "S3", "C8", "C4");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(1, Denomination.Diamonds),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_TwoFourCardMajors_BidsCheapestHearts()
    {
        var responder = Cards(
            "SQ", "SJ", "S4", "S2",
            "HK", "HQ", "H8", "H5",
            "D7", "D6", "D3", "C9", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(1, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_TwoFiveCardMajors_BidsHighestSpades()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S5", "S2",
            "HA", "HJ", "H7", "H4", "H3",
            "D6", "D2", "C4");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_FourCardMinorSupport_RaisesMinorBeforeBidOneNoTrump()
    {
        var responder = Cards(
            "SA", "S6", "S4",
            "HK", "H7",
            "DJ", "D8", "D5", "D4",
            "C9", "C5", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_BalancedThirteenPointsAfterMinorOpening_BidsThreeNoTrump()
    {
        var responder = Cards(
            "SA", "S7", "S3",
            "HK", "H8", "H2",
            "DA", "DQ", "D3",
            "CJ", "C9", "C5");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Redemandes de l'ouvreur ---

    [Fact]
    public void OpenerRebid_FourteenHldWithFit_RaisesToTwo()
    {
        var opener = Cards(
            "SK", "S9", "S6", "S3",
            "CA", "CQ", "CJ", "C4", "C2",
            "H8", "H5", "DQ", "D4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_SeventeenHldWithFit_RaisesToThree()
    {
        var opener = Cards(
            "SK", "SQ", "S9", "S6",
            "CA", "CQ", "CJ", "C4", "C2",
            "H8", "DK", "D7", "D4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_SeventeenHldBalancedWithMajorFit_RaisesSimply()
    {
        var opener = Cards(
            "SA", "S9", "S6", "S3",
            "HK", "H6", "H3",
            "DA", "D5", "D2",
            "CK", "C9", "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_TwentyOneHldWithoutFit_BidsEconomicSecondSuit()
    {
        var opener = Cards(
            "SK", "SQ", "S9", "S6",
            "CA", "CK", "CQ", "CJ", "C4",
            "DK", "D7", "D4", "D2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_NoFitOwnFourCardMajor_BidsItAtLevelOne()
    {
        var opener = Cards(
            "DA", "DQ", "D7", "D4",
            "SK", "SJ", "S8", "S5",
            "H6", "H2", "CK", "C9", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Diamonds)),
            (Seat.South, new Bid(1, Denomination.Hearts)));

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_BalancedTwelveToFourteen_BidsOneNoTrump()
    {
        var opener = Cards(
            "DA", "DQ", "D7", "D4",
            "HK", "H6", "H2",
            "SQ", "S9", "S3",
            "CK", "C9", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Diamonds)),
            (Seat.South, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_BalancedEighteenNineteen_BidsTwoNoTrump()
    {
        var opener = Cards(
            "DA", "DQ", "D7", "D4",
            "HK", "HQ", "H6",
            "SA", "SJ", "S3",
            "CK", "C9", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Diamonds)),
            (Seat.South, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    // --- Deuxième enchère du répondant ---

    [Fact]
    public void ResponderRebid_AfterSimpleRaiseWithSixHld_Passes()
    {
        var responder = Cards(
            "SQ", "SJ", "S8", "S5",
            "H7", "H6", "H3",
            "DK", "D8", "D4",
            "C9", "C5", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Null(_engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ResponderRebid_AfterSimpleRaiseWithElevenHld_TrialsAtThree()
    {
        var responder = Cards(
            "SQ", "SJ", "S8", "S5",
            "HK", "H6", "H3",
            "DK", "DQ", "D4",
            "C9", "C5", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ResponderRebid_AfterSimpleRaiseWithThirteenHld_BidsGame()
    {
        var responder = Cards(
            "SA", "SJ", "S8", "S5",
            "HK", "HQ", "H4",
            "DK", "D7", "D4",
            "C9", "C5", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ResponderRebid_SupportsOpenersMajorWithElevenHld_RaisesToThree()
    {
        var responder = Cards(
            "SK", "S9", "S7", "S4",
            "HA", "HJ", "H8", "H5",
            "D6", "D3", "CQ", "C7", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Diamonds)),
            (Seat.South, new Bid(1, Denomination.Hearts)),
            (Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
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
