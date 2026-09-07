using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Chapitre SEF : les redemandes de l'ouvreur.</summary>
public sealed class SefOpenerRebidTests
{
    private readonly SefBiddingEngine _engine = new();

    // --- Après un 2 sur 1 en majeure (1♠ - 2♥) ---

    [Fact]
    public void TwoOverOne_ThreeCardFitFifteenHld_SupportsAtThree()
    {
        var opener = Cards(
            "SA", "SQ", "SJ", "S7", "S5",
            "HK", "H8", "H4",
            "DA", "D6", "D3",
            "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void TwoOverOne_FourCardFitSeventeenHld_JumpsToGame()
    {
        var opener = Cards(
            "SA", "SQ", "SJ", "S7", "S5",
            "HK", "HQ", "H8", "H4",
            "DA", "D6",
            "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void TwoOverOne_MinimumWithThreeCardFit_RepeatsSuitFirst()
    {
        // 12-14 HLD : répétition, le soutien différé viendra au tour suivant.
        var opener = Cards(
            "SA", "SQ", "SJ", "S7", "S5",
            "HK", "H8", "H4",
            "DQ", "D6", "D3",
            "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void TwoOverOne_BalancedFifteenToSeventeen_BidsTwoNoTrump()
    {
        var opener = Cards(
            "SA", "SQ", "SJ", "S7", "S5",
            "HK", "H8", "H4",
            "DA", "DQ", "D3",
            "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Clubs)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    // --- Après un soutien mineur simple (1♣ - 2♣) ---

    [Fact]
    public void MinorRaise_SeventeenHl_AnnouncesCheapestGuard()
    {
        var opener = Cards(
            "CA", "CK", "CJ", "C4",
            "DK", "D8", "D5",
            "HA", "HQ", "H7",
            "S4", "S2", "S3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Clubs)));

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void MinorRaise_MinimumOpener_Passes()
    {
        var opener = Cards(
            "CA", "CQ", "CJ", "C4",
            "DK", "D8", "D5",
            "HQ", "H9", "H7",
            "SQ", "S4", "S2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Clubs)));

        Assert.Null(_engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void MinorRaise_ResponderWithRemainingGuards_ConcludesThreeNoTrump()
    {
        // 1♣ - 2♣ - 2♦ (garde ♦) : le répondant garde ♥ et ♠ → 3SA.
        var responder = Cards(
            "CK", "C9", "C7", "C5", "C2",
            "HK", "H8", "H4",
            "SQ", "S7", "S3",
            "D6", "D2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void MinorRaise_ResponderWithoutGuard_ReturnsToMinor()
    {
        var responder = Cards(
            "CK", "C9", "C7", "C5", "C2",
            "HK", "H8", "H4",
            "S8", "S7", "S3",
            "D6", "D2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Réponse mineure : soutien simple à cinq cartes ---

    [Fact]
    public void MinorResponse_FiveCardSupportWeak_RaisesSimply()
    {
        var responder = Cards(
            "CQ", "C9", "C7", "C5", "C2",
            "HK", "H8", "H4",
            "S8", "S7", "S3",
            "D6", "D2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void MinorOpening_OneNoTrumpResponseWithSixCardMinor_RepeatsAtThree()
    {
        var opener = Cards(
            "SA", "SK", "S3",
            "HA", "H8",
            "DK", "DQ", "DJ", "D8", "D7", "D2",
            "C6", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Diamonds)),
            (Seat.South, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void Rebid_EconomicTwoSuitIsMandatoryAfterTwoLevelResponse()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S3",
            "HQ", "H8", "H4", "H2",
            "D9", "D6", "D3", "D2",
            "C8");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Clubs)));

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void Rebid_ExpensiveTwoSuitShowsStrongHand()
    {
        var opener = Cards(
            "CA", "CK", "C7", "C3",
            "HA", "HK", "HQ", "H4",
            "D9", "D6", "D3",
            "SJ", "S2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void Rebid_JumpTwoSuitIsForcingWithFiveFiveShape()
    {
        var opener = Cards(
            "CA", "CK", "CQ", "C7", "C3",
            "HA", "HK", "HQ", "H4", "H2",
            "D9", "D6", "S3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void Rebid_SimpleRepeatAfterOneLevelResponse_ShowsSixCards()
    {
        var opener = Cards(
            "CA", "CK", "CQ", "C9", "C7", "C3",
            "HK", "H8", "H4",
            "D9", "D6", "S2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    // --- Barrages : l'ouvreur ne reparle pas ---

    [Fact]
    public void PreemptOpener_NeverBidsAgainAfterRaise()
    {
        var opener = Cards(
            "SK", "SQ", "SJ", "S9", "S7", "S4", "S2",
            "HA", "D10", "D4", "D2", "C9", "C5");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(3, Denomination.Spades)),
            (Seat.South, new Bid(4, Denomination.Spades)));

        Assert.Null(_engine.DecideBid(Seat.North, opener, auction));
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
