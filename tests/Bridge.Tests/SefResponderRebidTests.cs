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

    [Fact]
    public void StrictProfile_AfterSimpleMajorFitUsesOpeningSuitTrial()
    {
        var engine = StrictSefEngine();
        var responder = Cards("SA SK S2 CA C2 C3 D2 D8 D5 H9 H4 H3 H2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Clubs), engine.DecideBid(Seat.South, responder, auction));
        Assert.Equal(RuleForcing.OneRound, engine.LastForcing);
    }

    [Fact]
    public void StrictProfile_AfterSimpleMajorFitUsesNoTrumpForSlamTry()
    {
        var engine = StrictSefEngine();
        var responder = Cards("SA SK SQ SJ S2 CA CK CQ C2 DK DQ H2 H3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(2, Denomination.NoTrump), engine.DecideBid(Seat.South, responder, auction));
        Assert.Equal(RuleForcing.OneRound, engine.LastForcing);
    }

    [Fact]
    public void StrictProfile_AfterMajorJumpSupportShowsCheapestControl()
    {
        var engine = StrictSefEngine();
        var responder = Cards("SA S2 S3 H2 H3 H4 D2 D3 D4 CA C2 C3 C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(3, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Clubs), engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrictProfile_AfterMinorJumpSupportShowsCheapestStopperBelowGame()
    {
        var engine = StrictSefEngine();
        var responder = Cards("CA CK C2 D2 D3 HK H4 H3 SA S4 S3 S2 H2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Diamonds)),
            (Seat.North, new Bid(3, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.Hearts), engine.DecideBid(Seat.South, responder, auction));
        Assert.Equal(RuleForcing.OneRound, engine.LastForcing);
    }

    [Fact]
    public void StrictProfile_AfterJumpTwoNoTrumpShowsNaturalSuitGameForcing()
    {
        var engine = StrictSefEngine();
        var responder = Cards("CA CK C8 C7 C6 DA DK D3 HA H2 S2 S3 S4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Hearts)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(2, Denomination.NoTrump)));

        var call = engine.DecideCall(Seat.South, responder, auction);

        Assert.Equal("responder.after-two-nt.one-level-response", engine.LastConfiguredRuleId);
        Assert.Equal(new Bid(3, Denomination.Clubs), call.Bid);
        Assert.Equal(RuleForcing.GameForcing, engine.LastForcing);
    }

    [Fact]
    public void StrictProfile_AfterTwoOverOneShowsThreeCardMajorFitForcing()
    {
        var engine = StrictSefEngine();
        var responder = Cards("SA S8 S4 HK H7 H3 DQ D6 D3 CA C5 C4 C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Spades), engine.DecideBid(Seat.South, responder, auction));
        Assert.Equal(RuleForcing.GameForcing, engine.LastForcing);
    }

    [Fact]
    public void StrictProfile_AfterReverseUsesTwoNoTrumpModeratorWithMinimum()
    {
        var engine = StrictSefEngine();
        var responder = Cards("S2 S3 S4 H2 H3 H4 D2 D3 D4 C2 C3 C4 C5");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.NoTrump), engine.DecideBid(Seat.South, responder, auction));
        Assert.Equal(RuleForcing.OneRound, engine.LastForcing);
    }

    [Fact]
    public void StrictProfile_RoudiPrecedesNaturalRebidRules()
    {
        var engine = StrictSefEngine();
        var responder = Cards("SA S8 S7 S4 S2 HK H3 DA D3 C4 C3 C2 H2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(2, Denomination.Clubs), engine.DecideBid(Seat.South, responder, auction));
        Assert.Equal(RuleForcing.OneRound, engine.LastForcing);
    }

    [Fact]
    public void StrictProfile_AfterMajorRelayOpenerShowsHeartsBeforeSpades()
    {
        var engine = StrictSefEngine();
        var opener = Cards("HA HK H4 SA SK S7 S3 D9 D6 D3 C8 C5 C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Hearts)),
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Diamonds)));

        var call = engine.DecideCall(Seat.North, opener, auction);

        Assert.Equal("opener.after-major-relay.response-hearts", engine.LastConfiguredRuleId);
        Assert.Equal(AuctionAction.Bid, call.Action);
        Assert.Equal(new Bid(3, Denomination.Hearts), call.Bid);
    }

    [Fact]
    public void StrictProfile_AfterMinorOneNoTrumpFiveFiveShowsSlamAmbition()
    {
        var engine = StrictSefEngine();
        var responder = Cards("SA SK SQ S2 S3 HA HK H2 H3 H4 D2 D3 D4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Hearts), engine.DecideBid(Seat.South, responder, auction));
        Assert.Equal(RuleForcing.GameForcing, engine.LastForcing);
    }

    [Fact]
    public void StrictProfile_AfterTwoOverOneUsesFourthSuitGameForce()
    {
        var engine = StrictSefEngine();
        var responder = Cards("SA SK S8 S7 S6 CA CK C8 C7 H2 H3 D2 D3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(3, Denomination.Diamonds), engine.DecideBid(Seat.South, responder, auction));
        Assert.Equal(RuleForcing.GameForcing, engine.LastForcing);
    }

    [Fact]
    public void StrictProfile_AfterOneHeartOneSpadeOneNoTrumpShowsFiveFiveWithTwoHearts()
    {
        var engine = StrictSefEngine();
        var responder = Cards("SA S8 S7 S4 S2 HK HQ H8 H7 DK D3 C4 C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Hearts)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(2, Denomination.Hearts), engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrictProfile_AfterMinorOneNoTrumpShowsDiamondFiveFive()
    {
        var engine = StrictSefEngine();
        var responder = Cards("S8 S7 S6 S5 S2 D8 D7 D6 D5 D2 H4 H2 C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Spades)),
            (Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(2, Denomination.Diamonds), engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrictProfile_AfterOneLevelNewMajorSupportsOpenerSecondMajor()
    {
        var engine = StrictSefEngine();
        var responder = Cards("SA SK S8 S7 HK HQ H4 D2 D3 D4 C2 C3 C4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.Clubs)),
            (Seat.South, new Bid(1, Denomination.Hearts)),
            (Seat.North, new Bid(1, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Spades), engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrictProfile_PassedResponderNewSuitIsNonForcing()
    {
        var engine = StrictSefEngine();
        var auction = new Auction();
        auction.AddCall(Seat.North, AuctionAction.Pass);
        auction.AddCall(Seat.East, AuctionAction.Pass);
        auction.AddCall(Seat.South, new Bid(1, Denomination.Clubs));
        auction.AddCall(Seat.West, AuctionAction.Pass);
        var responder = Cards("SA S9 S8 S7 S6 HA H8 H6 H2 D9 D8 D7 C2");

        var call = engine.DecideCall(Seat.North, responder, auction);

        Assert.Equal(new Bid(1, Denomination.Spades), call.Bid);
        Assert.Equal(AuctionForcing.None, call.Forcing);
        Assert.Equal("response.minor.passed-new-suit", engine.LastConfiguredRuleId);
    }

    private static SefBiddingEngine StrictSefEngine() => new(profile: BiddingProfile.Load(
        Path.Combine(AppContext.BaseDirectory, "sef-bidding-profile.json")));

    private static List<Card> Cards(params string[] cards) => cards
        .SelectMany(card => card.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Select(Parse).ToList();

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
