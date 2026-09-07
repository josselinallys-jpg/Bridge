using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Chapitre SEF : les ouvertures majeures (majeure cinquième, fit).</summary>
public sealed class SefMajorOpeningTests
{
    private readonly SefBiddingEngine _engine = new();

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
    public void Response_FourteenHldWithNinthTrump_BidsGame()
    {
        // 12H + revalorisation de +2 pour le 9e atout = 14 HLD.
        var responder = Cards(
            "SK", "S8", "S4", "S3",
            "HA", "H7", "H5",
            "DQ", "D6", "D3",
            "CK", "C5", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_FourCardFitAndSingleton_UsesSplinter()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S4",
            "H2",
            "DA", "DK", "D6", "D3",
            "CQ", "C8", "C5", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
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
