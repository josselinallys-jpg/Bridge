using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Deuxième enchère du répondant selon les fiches 05 et 06.</summary>
public sealed class SefResponderRebidTests
{
    private readonly SefBiddingEngine _engine = new();

    [Fact]
    public void AfterOpenerRepeatsMinor_ElevenHlUsesThirdSuitForcing()
    {
        var responder = Cards(
            "HA", "HQ", "H8", "H4",
            "DK", "D7", "D3",
            "S9", "S6", "S4",
            "CA", "CQ", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Hearts)),
            (Seat.North, new Bid(2, Denomination.Clubs)));

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void AfterOneNoTrumpRebid_FiveCardMajorUsesPingPong()
    {
        var responder = Cards(
            "SK", "S8", "S6", "S4", "S2",
            "HQ", "H7", "H3",
            "D9", "D6", "D3",
            "C8", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void RespondersSixCardMajor_IsRepeatedAfterOpenerChangesSuit()
    {
        var responder = Cards(
            "HA", "HQ", "H9", "H7", "H5", "H3",
            "DK", "D6", "D3",
            "S8", "S4",
            "C7", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Hearts)),
            (Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void AfterTwoOverOne_ThreeCardOpeningMajorFitIsShownAtThree()
    {
        var responder = Cards(
            "SA", "S8", "S4",
            "HK", "H7", "H3",
            "DQ", "D6", "D3",
            "CA", "C5", "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void MajorOpening_ThreeCardFitElevenHld_UsesTwoNoTrumpFit()
    {
        var responder = Cards(
            "SA", "S8", "S4",
            "HA", "H7", "H3",
            "DK", "D6", "D3",
            "C9", "C5", "C4", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void TwoNoTrumpFit_OpenerMinimumChoosesThreeMajor()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S3", "S2",
            "HQ", "H8", "H4",
            "D9", "D6", "D3",
            "C8", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void TwoNoTrumpFit_OpenerMaximumChoosesFourMajor()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "S7", "S3",
            "HA", "HK", "H4",
            "DA", "D6", "D3",
            "C8", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void TwoNoTrumpFit_OpenerShowsMinorSlamTry()
    {
        var responder = Cards(
            "SA", "S8", "S4",
            "HK", "H7", "H3",
            "DA", "D6", "D3",
            "C9", "C5", "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)),
            (Seat.North, new Bid(3, Denomination.Clubs)));

        Assert.Equal(new Bid(4, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void TwoNoTrumpFit_OpenerShowsOtherMajorForcing()
    {
        var responder = Cards(
            "SA", "S8", "S4",
            "HK", "H7", "H3", "H2",
            "DA", "D6", "D3",
            "C9", "C5", "C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)),
            (Seat.North, new Bid(3, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ThreeNoTrumpFit_OpenerConcludesMajorWithMinimum()
    {
        var opener = Cards(
            "SK", "S7", "S3", "S2",
            "HA", "H8", "H4",
            "D9", "D6", "D3",
            "C8", "C5", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(3, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
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
