using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Chapitre SEF : les ouvertures fortes (2♣, 2♦, 2SA).</summary>
public sealed class SefStrongOpeningTests
{
    private readonly SefBiddingEngine _engine = new();

    // --- Ouvertures ---

    [Fact]
    public void Opening_StrongFiveCardSuitTwentyHl_OpensTwoClubs()
    {
        var hand = Cards(
            "SA", "SK", "SQ", "SJ", "S5",
            "HA", "HK", "H4",
            "DQ", "D7", "D3",
            "C4", "C2");

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    // --- Réponses en As sur 2♦ ---

    [Fact]
    public void GameForce_MinorAce_BidsThreeOfThatMinor()
    {
        var responder = Cards(
            "CA", "C7", "C5", "C2",
            "D9", "D6", "D4", "D3",
            "H8", "H2", "SJ", "S5", "S3");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void GameForce_MajorAce_BidsTwoSpades()
    {
        var responder = Cards(
            "HA", "H7", "H5", "H2",
            "D9", "D6", "D4", "D3",
            "C8", "C2", "SJ", "S5", "S3");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void GameForce_EightPointsNoAce_BidsTwoNoTrump()
    {
        var responder = Cards(
            "SK", "SQ", "S5",
            "HK", "H8", "H4",
            "D8", "D6", "D4", "D3",
            "C6", "C5", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void GameForce_TwoAces_BidsThreeNoTrump()
    {
        var responder = Cards(
            "SA", "S5", "S3",
            "HA", "H8", "H4",
            "D8", "D6", "D4", "D3",
            "C6", "C5", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Développements après 2♦ ---

    [Fact]
    public void GameForce_WeakFit_ConcludesGameInOpenersMajor()
    {
        // 2♦ - 2♥ (0-7 sans As) - 2♠ : fit deux cartes sans espoir de chelem → 4♠.
        var responder = Cards(
            "S9", "S7",
            "HQ", "H8", "H4", "H2",
            "D8", "D6", "D4",
            "C8", "C5", "C3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Diamonds)),
            (Seat.South, new Bid(2, Denomination.Hearts)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void GameForce_FitAfterLevelThreeRebid_ConcludesGame()
    {
        // 2♦ - 2SA (8+ sans As) - 3♠ : fit, conclusion à la manche (palier 3 indisponible).
        var responder = Cards(
            "SK", "S8", "S4",
            "HQ", "HJ", "H7",
            "DK", "D6", "D4",
            "CQ", "C5", "C3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Diamonds)),
            (Seat.South, new Bid(2, Denomination.NoTrump)),
            (Seat.North, new Bid(3, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void GameForce_NoTrumpRebid_BustStillBidsGame()
    {
        // 2♦ forcing de manche : même avec zéro point, jamais de passe avant la manche.
        var responder = Cards(
            "S9", "S7", "S5",
            "H8", "H6", "H4",
            "D8", "D6", "D4",
            "C8", "C5", "C3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Diamonds)),
            (Seat.South, new Bid(2, Denomination.Hearts)),
            (Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Développements après 2♣ ---

    [Fact]
    public void StrongTwoClubs_FitAndValues_ConcludesMajorGame()
    {
        var responder = Cards(
            "SQ", "S8", "S4",
            "H7", "H6", "H4",
            "DQ", "D6", "D4",
            "C8", "C5", "C3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubs_FitWithoutValues_StopsInPartial()
    {
        var responder = Cards(
            "S9", "S8", "S4",
            "H7", "H6", "H4",
            "DJ", "D6", "D4",
            "C8", "C5", "C3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- 2SA (20-21) ---

    [Fact]
    public void TwoNoTrump_ThreeHlBalanced_Passes()
    {
        var responder = Cards(
            "S9", "S7", "S5",
            "HQ", "H6", "H4",
            "DJ", "D6", "D4",
            "C8", "C5", "C3", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Null(_engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void TwoNoTrump_ElevenPoints_BidsQuantitativeFourNoTrump()
    {
        var responder = Cards(
            "SK", "S7", "S5",
            "HQ", "HJ", "H4",
            "DK", "D6", "D4",
            "CQ", "C5", "C3", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void TwoNoTrump_OpenerAcceptsQuantitativeWithTwentyOne()
    {
        var opener = Cards(
            "SA", "SK", "S5",
            "HA", "HQ", "H4",
            "DA", "DQ", "D4",
            "CK", "C5", "C3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(4, Denomination.NoTrump)));

        Assert.Equal(new Bid(5, Denomination.NoTrump),
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
