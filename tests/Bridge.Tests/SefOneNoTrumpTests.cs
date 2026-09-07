using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Chapitre SEF : l'ouverture de 1SA et ses développements.</summary>
public sealed class SefOneNoTrumpTests
{
    private readonly SefBiddingEngine _engine = new();

    // --- Forme de l'ouverture ---

    [Fact]
    public void Opening_TwoDoubletons_DoesNotOpenOneNoTrump()
    {
        // 5422 avec 16H : deux doubletons interdisent 1SA.
        var hand = Cards(
            "DA", "DK", "DQ", "D6", "D2",
            "CK", "CJ", "C5", "C4",
            "HA", "H3", "S8", "S7");

        Assert.Equal(new Bid(1, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    // --- Réponses ---

    [Fact]
    public void Response_EightPointsRuleOfEightSatisfied_BidsTwoNoTrump()
    {
        var responder = Cards(
            "SK", "S10", "S9",
            "HJ", "H9", "H8",
            "DA", "D10", "D9", "D8",
            "C9", "C8", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_EightPointsRuleOfEightFailed_Passes()
    {
        var responder = Cards(
            "SQ", "S5", "S2",
            "HQ", "H4", "H3",
            "DA", "D4", "D3", "D2",
            "C6", "C5", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Null(_engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_SixteenPointsBalanced_BidsQuantitativeFourNoTrump()
    {
        var responder = Cards(
            "SA", "SQ", "S5",
            "HK", "HJ", "H4",
            "DK", "DQ", "D3",
            "CJ", "C4", "C3", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_FiveFiveMajorsGameZone_BidsFourDiamonds()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S5", "S2",
            "HQ", "HJ", "H7", "H4", "H3",
            "D6", "D2", "C4");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.Diamonds),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Redemandes de l'ouvreur ---

    [Fact]
    public void Opener_AcceptsQuantitativeWithSeventeen()
    {
        var opener = Cards(
            "SA", "SK", "S4",
            "HQ", "H7", "H3",
            "DA", "DJ", "D5", "D2",
            "CK", "C7", "C6");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(4, Denomination.NoTrump)));

        Assert.Equal(new Bid(5, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void Opener_DeclinesQuantitativeWithFifteen()
    {
        var opener = Cards(
            "SA", "SK", "S4",
            "HQ", "H7", "H3",
            "DQ", "DJ", "D5", "D2",
            "CK", "C7", "C6");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(4, Denomination.NoTrump)));

        Assert.Null(_engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void Opener_ChoosesSpadeGameAfterFourDiamonds()
    {
        var opener = Cards(
            "SA", "S7", "S4",
            "HK", "H5",
            "DK", "DQ", "D8", "D3",
            "CA", "CJ", "C6", "C5");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(4, Denomination.Diamonds)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    // --- Chassé-croisé ---

    [Fact]
    public void ChasseCroise_TenPoints_JumpsInFourCardMajor()
    {
        // 5♠-4♥, 10H : après 2♦, saut à 3♥ (forcing).
        var responder = Cards(
            "SK", "SQ", "S8", "S5", "S2",
            "HA", "HJ", "H7", "H4",
            "D6", "D3", "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ChasseCroise_EightPoints_BidsFiveCardMajorAtTwo()
    {
        var responder = Cards(
            "SK", "SQ", "S8", "S5", "S2",
            "HQ", "HJ", "H7", "H4",
            "D6", "D3", "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void ChasseCroise_OpenerRectifiesWithThreeCardFit()
    {
        // Après 1SA-2♣-2♦-3♥ (5♠-4♥) : l'ouvreur a 3 piques → 4♠.
        var opener = Cards(
            "SA", "S7", "S4",
            "HK", "H5", "H2",
            "DK", "DQ", "D8", "D3",
            "CA", "CJ", "C6");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Diamonds)),
            (Seat.South, new Bid(3, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void ChasseCroise_OpenerWithoutFit_ReturnsToThreeNoTrump()
    {
        var opener = Cards(
            "SA", "S7",
            "HK", "H5", "H2",
            "DK", "DQ", "D8", "D3",
            "CA", "CJ", "C6", "C5");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Diamonds)),
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
