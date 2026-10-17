using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Chapitre SEF : les ouvertures majeures (majeure cinquième, fit).</summary>
public sealed class SefMajorOpeningTests
{
    private readonly SefBiddingEngine _engine = new();

    [Fact]
    public void Opening_ThirdSeatEightToNineHlWithGoodFiveCardMajor_OpensOneSpade()
    {
        var hand = Cards(
            "SK", "SQ", "S8", "S6", "S3",
            "H8", "H6",
            "D8", "D6", "D3",
            "CA", "C6", "C4");
        var auction = AuctionWithPassesBefore(Seat.South);

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void Opening_FourthSeatEightToNineHlWithGoodFiveCardMajor_OpensOneHeart()
    {
        var hand = Cards(
            "HK", "HQ", "H8", "H6", "H3",
            "S8", "S6",
            "D8", "D6", "D3",
            "CA", "C6", "C4");
        var auction = AuctionWithPassesBefore(Seat.West);

        Assert.Equal(new Bid(1, Denomination.Hearts),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void Opening_FiveFiveMajors_PrefersSpades()
    {
        var hand = Cards(
            "SA", "SK", "S8", "S6", "S3",
            "HK", "H8", "H6", "H3", "H2",
            "DQ", "D6", "C4");

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Response_FourCardFitElevenToTwelveHld_RaisesToThreeMajor()
    {
        var responder = Cards(
            "SK", "S8", "S4", "S3",
            "HA", "H6", "H3",
            "DQ", "D6", "D3",
            "C8", "C6", "C4");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_FourCardFitThirteenToFifteenHldBalanced_BidsThreeNoTrump()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S3",
            "HA", "H6", "H3",
            "DA", "D6", "D3",
            "C8", "C6", "C4");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_FourCardFitThirteenToFifteenHldWithSingleton_UsesSplinter()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S3",
            "H2",
            "DA", "D6", "D3", "D2",
            "CK", "C6", "C4", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ThirdSeatMajorOpening_ThreeCardFitElevenHld_UsesDrury()
    {
        var responder = Cards(
            "SK", "S8", "S4",
            "HA", "H7", "H5",
            "DQ", "D6", "D3",
            "CK", "C5", "C3", "C2");
        var auction = ThirdSeatMajorSequence(new Bid(1, Denomination.Spades));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.North, responder, auction));
    }

    [Fact]
    public void ThirdSeatMajorOpening_OpenerWithSixteenToSeventeenHl_AnswersTwoDiamondsToDrury()
    {
        var opener = Cards(
            "SA", "SK", "S8", "S6", "S3",
            "HA", "HK",
            "DQ", "D6", "D3",
            "C8", "C6", "C4");
        var auction = ThirdSeatMajorSequence(new Bid(1, Denomination.Spades), new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.South, opener, auction));
    }

    [Fact]
    public void ThirdSeatMajorOpening_AfterDruryTwoDiamonds_FourCardFitRaisesToThree()
    {
        var responder = Cards(
            "SK", "S8", "S4", "S3",
            "HA", "H7", "H5",
            "DQ", "D6", "D3",
            "CQ", "C5", "C2");
        var auction = ThirdSeatMajorSequence(
            new Bid(1, Denomination.Spades),
            new Bid(2, Denomination.Clubs),
            new Bid(2, Denomination.Diamonds));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.North, responder, auction));
    }

    [Fact]
    public void ThirdSeatMajorOpening_FourCardFitWithSingletonAndElevenHld_UsesFittedTwoNoTrump()
    {
        var responder = Cards(
            "SK", "S8", "S4", "S3",
            "H2",
            "DA", "D6", "D3", "D2",
            "CQ", "C8", "C6", "C4");
        var auction = ThirdSeatMajorSequence(new Bid(1, Denomination.Spades));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, responder, auction));
    }

    [Fact]
    public void ThirdSeatMajorOpening_SixCardOtherMajorWithEightToTenH_BidsItNonForcing()
    {
        var responder = Cards(
            "HA", "HK", "H8", "H6", "H3", "H2",
            "S8", "S6",
            "DQ", "D6",
            "C8", "C6", "C4");
        var auction = ThirdSeatMajorSequence(new Bid(1, Denomination.Spades));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.North, responder, auction));
    }

    [Fact]
    public void Response_OneSpadeTwoHearts_RequiresFiveHearts()
    {
        var responder = Cards(
            "HK", "H7", "H5", "H3",
            "S8", "S4",
            "DA", "D6", "D3", "D2",
            "CQ", "C5", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.NotEqual(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_FiveCardFitWithEightToTenH_BidsDirectMajorGame()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S6", "S3",
            "HA", "H6", "H3",
            "D8", "D6",
            "C8", "C6", "C4");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Soutiens du répondant (HLD revalorisés) ---

    [Fact]
    public void Response_ThreeCardSupportSevenHld_RaisesToTwo()
    {
        var responder = Cards(
            "SQ", "S8", "S4",
            "HK", "H7", "H5", "H3",
            "DQ", "D6", "D2",
            "C8", "C4", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_TwelveHld_UsesTwoNoTrumpFit()
    {
        var responder = Cards(
            "SK", "S8", "S4",
            "HA", "H7", "H5", "H3",
            "DJ", "D6", "D3",
            "CK", "C5", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_FourteenHldWithNinthTrump_UsesFittedThreeNoTrump()
    {
        // 12H + revalorisation de +2 pour le 9e atout = 14 HLD.
        var responder = Cards(
            "SK", "S8", "S4", "S3",
            "HA", "H7", "H5",
            "DQ", "D6", "D3",
            "CK", "C5", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_SixteenHldFourCardFit_StartsWithColorChange()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S4",
            "H2",
            "DA", "DK", "D6", "D3",
            "CQ", "C8", "C5", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_ElevenHldThreeCardFit_UsesFittedTwoNoTrump()
    {
        var responder = Cards(
            "SK", "S8", "S4",
            "HA", "H7", "H3",
            "DK", "D6", "D3",
            "CJ", "C5", "C4", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void OpenerRebid_FittedTwoNoTrumpWithMinimum_ReturnsToThreeMajor()
    {
        var opener = Cards(
            "SA", "SQ", "SJ", "S7", "S5",
            "HK", "H4", "H2",
            "D9", "D6", "D3",
            "C8", "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_FittedTwoNoTrumpWithEighteenBalancedH_BidsThreeNoTrump()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "S7", "S5",
            "HK", "HQ", "H3",
            "DA", "D3",
            "C8", "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    // --- Redemandes de l'ouvreur après soutien ---

    [Fact]
    public void OpenerRebid_FourteenHldAfterSimpleRaise_Passes()
    {
        var opener = Cards(
            "SA", "SQ", "SJ", "S7", "S5",
            "HK", "H4", "H2",
            "D9", "D6", "D3",
            "CK", "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Spades)));

        Assert.Null(_engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_SeventeenHldAfterSimpleRaise_UsesNaturalTrialSuit()
    {
        var opener = Cards(
            "SA", "SQ", "SJ", "S7", "S5",
            "HK", "HQ", "H2",
            "DA", "D6", "D3",
            "C9", "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_TwentyHldAfterSimpleRaise_BidsGame()
    {
        // 16H + 2D + revalorisation du 9e atout = 20 HLD.
        var opener = Cards(
            "SA", "SQ", "SJ", "S9", "S7", "S5",
            "HK", "HQ", "H2",
            "DA", "D6",
            "C9", "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_EighteenHldAfterGameRaise_TrialsSlamAtFive()
    {
        var opener = Cards(
            "SA", "SQ", "SJ", "S7", "S5",
            "HA", "HQ", "H2",
            "DK", "D6", "D3", "D2",
            "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(4, Denomination.Spades)));

        Assert.Equal(new Bid(5, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    // --- Réponses du répondant aux enchères d'essai ---

    [Fact]
    public void ResponderRebid_NineHldOnTrialBid_AcceptsGame()
    {
        var responder = Cards(
            "SQ", "S8", "S4",
            "HK", "H7", "H5", "H3",
            "DK", "D6",
            "C8", "C4", "C3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Spades)),
            (Seat.North, new Bid(3, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ResponderRebid_SevenHldOnTrialBid_Passes()
    {
        var responder = Cards(
            "SQ", "S8", "S4",
            "HK", "H7", "H5", "H3",
            "DQ", "D6", "D2",
            "C8", "C4", "C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Spades)),
            (Seat.North, new Bid(3, Denomination.Spades)));

        Assert.Null(_engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ResponderRebid_FourteenHldOnSlamTrial_BidsSlam()
    {
        // 12H + revalorisation du 9e atout = 14 HLD.
        var responder = Cards(
            "SK", "S8", "S4", "S3",
            "HA", "H7", "H5",
            "DQ", "D6", "D3",
            "CK", "C5", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(4, Denomination.Spades)),
            (Seat.North, new Bid(5, Denomination.Spades)));

        Assert.Equal(new Bid(6, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ControlBidAtFourLevel_UsesFourNoTrumpToAskForKeycards()
    {
        var opener = Cards(
            "SA", "SQ", "SJ", "S9", "S7",
            "HK", "H8", "H5", "H2",
            "DA", "D6", "D3",
            "C8", "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Spades)),
            (Seat.North, new Bid(3, Denomination.Spades)),
            (Seat.South, new Bid(4, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void OpenerRebid_TwentyOneHldAfterThreeLevelRaise_OnlyTriesSlam()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "S7", "S5",
            "HA", "HK", "H2",
            "DA", "D6", "D3",
            "CK", "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(3, Denomination.Spades)));

        Assert.Equal(new Bid(5, Denomination.Spades),
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

    private static Auction ThirdSeatMajorSequence(params Bid[] bids)
    {
        var auction = new Auction();
        auction.AddCall(Seat.North, null);
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, bids[0]);
        auction.AddCall(Seat.West, null);
        for (var index = 1; index < bids.Length; index++)
            auction.AddCall(index % 2 == 1 ? Seat.North : Seat.South, bids[index]);
        return auction;
    }

    private static Auction AuctionWithPassesBefore(Seat seat)
    {
        var auction = new Auction();
        while (auction.Calls.Count % 4 != (int)seat)
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
