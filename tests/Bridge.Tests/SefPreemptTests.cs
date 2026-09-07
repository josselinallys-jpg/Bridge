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
