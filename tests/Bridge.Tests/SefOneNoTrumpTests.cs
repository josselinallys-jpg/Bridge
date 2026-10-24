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

    [Fact]
    public void Opening_FiveFourTwoTwoWithFiveCardMinor_OpensOneNoTrump()
    {
        var hand = Cards(
            "CA", "CK", "CJ", "C8", "C5",
            "DQ", "DJ", "D6", "D3",
            "HA", "H2", "S7", "S4");

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_SixThreeTwoTwoWithHonorsInShortSuits_OpensOneNoTrump()
    {
        var hand = Cards(
            "CA", "CK", "CQ", "C8", "C6", "C4",
            "D7", "D4", "D2",
            "HA", "H3", "SQ", "S2");

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_SeventeenPointsWithFiveFourTwoTwoMinor_DoesNotOpenOneNoTrump()
    {
        var hand = Cards(
            "CA", "CK", "CQ", "C8", "C5",
            "DA", "DJ", "D6", "D3",
            "HK", "H2", "S7", "S4");

        Assert.Equal(new Bid(1, Denomination.Clubs),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_SeventeenPointsWithSixThreeTwoTwoMinor_DoesNotOpenOneNoTrump()
    {
        var hand = Cards(
            "CA", "CK", "CQ", "C8", "C6", "C4",
            "DA", "D7", "D4",
            "HK", "H3", "SJ", "S2");

        Assert.Equal(new Bid(1, Denomination.Clubs),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_FiveCardMajorWithOneDoubleton_OpensOneNoTrump()
    {
        var hand = Cards(
            "SA", "SK", "S7", "S4", "S2",
            "HQ", "H6", "H3",
            "DA", "D8", "D3",
            "CQ", "C9");

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Theory]
    [InlineData(18, 6)]
    [InlineData(20, 5)]
    [InlineData(22, 7)]
    public void Response_HighQuantitativeZones_UsesSefNoTrumpLevel(int expectedPoints, int expectedLevel)
    {
        var hand = expectedPoints switch
        {
            18 => Cards("SA", "SK", "SQ", "HK", "HQ", "DA", "D2", "D3", "D4", "C2", "C3", "C4", "C5"),
            20 => Cards("SA", "SK", "SQ", "HA", "HK", "DA", "D2", "D3", "D4", "C2", "C3", "C4", "C5"),
            _ => Cards("SA", "SK", "SQ", "HA", "HK", "DA", "DQ", "D2", "D3", "C2", "C3", "C4", "C5")
        };
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(expectedLevel, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void Response_SixCardSpadeAfterTexas_RepeatsSpadesAtGame()
    {
        var responder = Cards(
            "SA", "SK", "SQ", "SJ", "S8", "S7",
            "HK", "HQ", "DJ", "D2", "C3", "C4", "C5");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Hearts)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_SixCardHeartWithShortSpade_UsesMajorSplinter()
    {
        var responder = Cards(
            "HA", "HK", "HQ", "HJ", "H8", "H7",
            "SK", "DQ", "D3", "D4", "C4", "C5", "C6");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_FiveCardMinorWithShortness_UsesMinorTexas()
    {
        var responder = Cards(
            "CK", "CQ", "CJ", "C8", "C7",
            "HA", "S3", "S4", "S5", "DA", "D2", "D3", "D4");

        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_MinorTexasWithShortness_DescribesShortSuitAfterRectification()
    {
        var responder = Cards(
            "CK", "CQ", "CJ", "C8", "C7",
            "HA", "S3", "S4", "S5", "DA", "D2", "D3", "D4");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Spades)),
            (Seat.North, new Bid(3, Denomination.Clubs)));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_SixCardDiamondWithSlamHope_JumpsNaturally()
    {
        var responder = Cards(
            "DA", "DK", "DQ", "DJ", "D8", "D7",
            "SA", "SK", "H3", "H4", "C3", "C4", "C5");

        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Diamonds),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Stayman_TwoNoTrumpAnswer_TransfersFiveHearts()
    {
        var responder = Cards(
            "HA", "HQ", "HJ", "H8", "H7",
            "S3", "S4", "D3", "D4", "C3", "C4", "C5", "C6");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Stayman_TwoNoTrumpAnswer_OpenerRectifiesHeartTransfer()
    {
        var opener = Cards(
            "HA", "HK", "H4", "S3", "S4", "D3", "D4", "D5", "C3", "C4", "C5", "C6", "C7");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)));

        var rectification = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(3, Denomination.Hearts));
        var explanation = _engine.ExplainCall(Seat.North, opener, auction.Calls, rectification);

        Assert.Equal("stayman-two-nt-transfer-rectification", explanation.RuleId);
        Assert.Equal(new Bid(3, Denomination.Hearts), _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void Stayman_ThreeClubsTransferWithoutFit_ReturnsToThreeNoTrump()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S2",
            "HK", "H4",
            "DK", "D8", "D3",
            "CQ", "C9", "C5", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)));

        var call = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(3, Denomination.NoTrump));
        var explanation = _engine.ExplainCall(Seat.North, opener, auction.Calls, call);

        Assert.Equal("stayman-two-nt-transfer-no-fit", explanation.RuleId);
        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Theory]
    [InlineData(3, "stayman-two-nt-transfer")]
    [InlineData(4, "stayman-two-nt-transfer-game")]
    public void Tooltip_StaymanTransferDistinguishesInvitationFromGameForce(int level, string ruleId)
    {
        var responder = Cards(
            "HA", "HQ", "HJ", "H8", "H7",
            "S3", "S4", "D3", "D4", "C3", "C4", "C5", "C6");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.NoTrump)));
        var call = new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(level, Denomination.Clubs));

        var explanation = _engine.ExplainCall(Seat.South, responder, auction.Calls, call);

        Assert.Equal(ruleId, explanation.RuleId);
    }

    [Fact]
    public void Tooltip_GameForcingStaymanTransferRectification_IsDistinct()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S2",
            "HA", "HK", "H4",
            "DK", "D8", "D3",
            "CQ", "C9", "C5");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(4, Denomination.Clubs)));
        var rectification = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(4, Denomination.Hearts));

        var explanation = _engine.ExplainCall(Seat.North, opener, auction.Calls, rectification);

        Assert.Equal("stayman-two-nt-transfer-game-rectification", explanation.RuleId);
    }

    [Fact]
    public void Stayman_MajorAnswerWithShortness_UsesSplinter()
    {
        var responder = Cards(
            "HA", "HK", "HQ", "HJ", "SK", "SQ", "S5", "S6", "D3", "D4", "D5", "D6", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Stayman_MajorAnswerWithOtherMajor_UsesConvention2012()
    {
        var responder = Cards(
            "HA", "HK", "HQ", "HJ", "SK", "SQ", "SJ", "D3", "D4", "D5", "C2", "C6", "C7");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Stayman_ContraWithBothMajors_AnnouncesBothMajors()
    {
        var opener = Cards(
            "HA", "HK", "H8", "H3", "SK", "SQ", "S7", "S2",
            "DA", "D3", "D4", "C3", "C4");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.West, AuctionAction.Double);

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void Texas_ContraWithFourCardFit_RectifiesForOwnPlay()
    {
        var opener = Cards(
            "HA", "HK", "HQ", "H4", "S3", "S4", "D3", "D4", "D5", "C3", "C4", "C5", "C6");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Diamonds));
        auction.AddCall(Seat.West, AuctionAction.Double);

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
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
    public void Response_SevenHonorLengthPointsWithoutLongSuit_Passes()
    {
        var responder = Cards(
            "SK", "S4", "S3",
            "HQ", "H4", "H3",
            "D4", "D3", "D2",
            "CQ", "C3", "C2", "C5");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Null(_engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_TwelveHonorLengthPoints_BidsThreeNoTrump()
    {
        var responder = Cards(
            "SA", "SK", "S4",
            "HQ", "H4", "H3",
            "DA", "D4", "D3",
            "C4", "C3", "C2", "C5");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Response_EightHonorLengthPoints_BidsTwoNoTrump()
    {
        var responder = Cards(
            "SQ", "S5", "S2",
            "HQ", "H4", "H3",
            "DA", "D4", "D3", "D2",
            "C6", "C5", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
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

        Assert.Equal(new Bid(6, Denomination.NoTrump),
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

    [Theory]
    [InlineData(12, 14, 1)]
    [InlineData(15, 17, 0)]
    public void Profile_Opening_UsesConfiguredRange(int minimum, int maximum, int expectedLevel)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            OneNoTrump = new() { Minimum = minimum, Maximum = maximum }
        });
        var hand = Cards("SA", "S7", "S4", "HK", "H5", "H2", "DK", "D8", "D3", "CQ", "C6", "C5", "C2");

        var bid = engine.DecideBid(Seat.North, hand, new Auction());

        if (expectedLevel == 1) Assert.Equal(new Bid(1, Denomination.NoTrump), bid);
        else Assert.NotEqual(new Bid(1, Denomination.NoTrump), bid);
    }

    [Fact]
    public void Profile_FiveCardMajorCanBeExcluded()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            OneNoTrump = new() { AllowFiveCardMajor = false }
        });
        var hand = Cards("SA", "SK", "S7", "S4", "S2", "HQ", "H6", "H3", "DA", "D8", "D3", "CQ", "C9");

        Assert.Equal(new Bid(1, Denomination.Spades), engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Theory]
    [InlineData(true, 8, 2, Denomination.Clubs)]
    [InlineData(true, 11, 2, Denomination.NoTrump)]
    [InlineData(false, 8, 2, Denomination.NoTrump)]
    public void Profile_Stayman_UsesActivationAndThreshold(bool enabled, int minimum, int level, Denomination denomination)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Conventions = new() { Stayman = enabled, StaymanMinimumHL = minimum }
        });
        var hand = Cards("SK", "S7", "S4", "S2", "HQ", "H5", "H2", "DK", "D8", "D3", "C6", "C5", "C2");

        Assert.Equal(new Bid(level, denomination), engine.DecideBid(Seat.South, hand,
            AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)))));
    }

    [Theory]
    [InlineData(true, Denomination.Diamonds)]
    [InlineData(false, Denomination.Hearts)]
    public void Profile_MajorTransferDisabled_UsesNaturalResponse(bool enabled, Denomination denomination)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Conventions = new() { MajorTransfers = enabled }
        });
        var hand = Cards("S7", "S4", "S2", "HQ", "H8", "H7", "H5", "H2", "D8", "D3", "C6", "C5", "C2");

        Assert.Equal(new Bid(2, denomination), engine.DecideBid(Seat.South, hand,
            AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)))));
    }

    [Theory]
    [InlineData(true, 2, Denomination.Spades)]
    [InlineData(false, 3, Denomination.Clubs)]
    public void Profile_MinorTransferDisabled_UsesNaturalResponse(bool enabled, int level, Denomination denomination)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Conventions = new() { MinorTransfers = enabled }
        });
        var hand = Cards("S7", "S4", "S2", "H8", "H2", "D8", "D3", "CQ", "C8", "C7", "C6", "C5", "C2");

        Assert.Equal(new Bid(level, denomination), engine.DecideBid(Seat.South, hand,
            AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)))));
    }

    [Theory]
    [InlineData(Denomination.Clubs)]
    [InlineData(Denomination.Diamonds)]
    [InlineData(Denomination.Hearts)]
    [InlineData(Denomination.Spades)]
    public void Profile_DisabledConventions_OpenerDoesNotRectify(Denomination response)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Conventions = new() { Stayman = false, MajorTransfers = false, MinorTransfers = false }
        });
        var hand = Cards("SA", "SK", "S7", "S4", "HQ", "H6", "H3", "DA", "D8", "D3", "CQ", "C9", "C2");

        Assert.Null(engine.DecideBid(Seat.North, hand, AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)), (Seat.South, new Bid(2, response)))));
    }

    [Fact]
    public void Profile_Invitation_OpenerAcceptsAtConfiguredMaximum()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            OneNoTrump = new() { Minimum = 12, Maximum = 14 }
        });
        var hand = Cards("SA", "S7", "S4", "HK", "H5", "H2", "DK", "D8", "D3", "CA", "C6", "C5", "C2");

        Assert.Equal(new Bid(3, Denomination.NoTrump), engine.DecideBid(Seat.North, hand, AuctionWithBids(
            (Seat.North, new Bid(1, Denomination.NoTrump)), (Seat.South, new Bid(2, Denomination.NoTrump)))));
    }

    [Fact]
    public void Profile_QuantitativeResponses_UseConfiguredThresholds()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Responses = new() { InvitationMinimumHL = 6, GameMinimumHL = 8 }
        });
        var hand = Cards("SK", "S7", "S4", "HQ", "H5", "H2", "DK", "D8", "D3", "C8", "C6", "C5", "C2");

        Assert.Equal(new Bid(3, Denomination.NoTrump), engine.DecideBid(Seat.South, hand,
            AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)))));
    }

    [Fact]
    public void Profile_Explanation_ExposesConfiguredValues()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Id = "sa-faible",
            OneNoTrump = new() { Minimum = 12, Maximum = 14 },
            Conventions = new() { StaymanMinimumHL = 11 }
        });
        var hand = Cards("SA", "S7", "S4", "HK", "H5", "H2", "DK", "D8", "D3", "CQ", "C6", "C5", "C2");
        var explanation = engine.ExplainCall(Seat.North, hand, [],
            new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.NoTrump)));

        Assert.Equal("opening-1nt", explanation.RuleId);
        Assert.Equal("sa-faible", explanation.Values["profil"]);
        Assert.Equal("12", explanation.Values["minimum1SA"]);
        Assert.Equal("14", explanation.Values["maximum1SA"]);
        Assert.Equal("11", explanation.Values["minimumStayman"]);
    }

    [Fact]
    public void Profile_NaturalResponse_IsNotExplainedAsTransfer()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Conventions = new() { MajorTransfers = false }
        });
        var hand = Cards("S7", "S4", "S2", "HQ", "H8", "H7", "H5", "H2", "D8", "D3", "C6", "C5", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)));
        var call = engine.DecideCall(Seat.South, hand, auction);

        Assert.Equal("natural", engine.ExplainCall(Seat.South, hand, auction.Calls, call).RuleId);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    [InlineData(4, 5)]
    [InlineData(5, 7)]
    public void Profile_AllQuantitativeZones_AreConfigurable(int offset, int level)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Responses = new()
            {
                InvitationMinimumHL = 8 - offset,
                GameMinimumHL = 9 - offset,
                SmallSlamInvitationMinimumHL = 10 - offset,
                SmallSlamMinimumHL = 11 - offset,
                GrandSlamInvitationMinimumHL = 12 - offset,
                GrandSlamMinimumHL = 13 - offset
            }
        });
        var hand = Cards("SK", "S7", "S4", "HQ", "H5", "H2", "DK", "D8", "D3", "C8", "C6", "C5", "C2");

        Assert.Equal(new Bid(level, Denomination.NoTrump), engine.DecideBid(Seat.South, hand,
            AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)))));
    }

    [Fact]
    public void MinorTexas_ShortHeart_OpenerShowsClubControlAndDiamondFit()
    {
        var engine = DeclarativeTexasEngine();
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)), (Seat.North, new Bid(3, Denomination.Diamonds)),
            (Seat.South, new Bid(3, Denomination.Hearts)));
        var hand = Cards("SA", "SK", "S2", "H8", "H5", "H2", "DK", "DQ", "D2", "CA", "C8", "C5", "C2");

        var call = engine.DecideCall(Seat.North, hand, auction);
        var explanation = engine.ExplainCall(Seat.North, hand, auction.Calls, call);

        Assert.Equal(new Bid(4, Denomination.Clubs), call.Bid);
        Assert.Equal("one-nt.minor-short.opener-control", explanation.RuleId);
        Assert.Contains("Controle", explanation.Values["descriptionRegle"]);
        Assert.Contains("mineure", explanation.Values["descriptionRegle"]);
        Assert.Equal(RuleForcing.GameForcing, engine.LastForcing);
    }

    [Fact]
    public void MinorTexas_AfterClubControl_ResponderConcludesFiveDiamondsInsteadOfPassing()
    {
        var engine = DeclarativeTexasEngine();
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)), (Seat.North, new Bid(3, Denomination.Diamonds)),
            (Seat.South, new Bid(3, Denomination.Hearts)), (Seat.North, new Bid(4, Denomination.Clubs)));
        var hand = Cards("SQ", "S8", "S2", "H2", "DA", "DQ", "DJ", "D8", "D6", "D2", "CQ", "C8", "C2");

        Assert.Equal(new Bid(5, Denomination.Diamonds), engine.DecideBid(Seat.South, hand, auction));
        Assert.Equal("one-nt.minor-short.conclude", engine.LastConfiguredRuleId);
    }

    [Fact]
    public void MinorTexas_AfterClubControl_PositiveResponderShowsSpadeControl()
    {
        var engine = DeclarativeTexasEngine();
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)), (Seat.North, new Bid(3, Denomination.Diamonds)),
            (Seat.South, new Bid(3, Denomination.Hearts)), (Seat.North, new Bid(4, Denomination.Clubs)));
        var hand = Cards("SA", "S8", "S2", "H2", "DA", "DK", "DJ", "D8", "D6", "CQ", "C8", "C5", "C2");

        Assert.Equal(new Bid(4, Denomination.Spades), engine.DecideBid(Seat.South, hand, auction));
        Assert.Equal("one-nt.minor-short.next-control", engine.LastConfiguredRuleId);
    }

    [Theory]
    [InlineData(false, 5, Denomination.Hearts)]
    [InlineData(true, 5, Denomination.Clubs)]
    public void MinorTexas_BlackwoodCountsKeysInTransferredMinor(bool trumpKing, int level, Denomination denomination)
    {
        var engine = DeclarativeTexasEngine();
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)), (Seat.North, new Bid(3, Denomination.Diamonds)),
            (Seat.South, new Bid(3, Denomination.Hearts)), (Seat.North, new Bid(4, Denomination.Clubs)),
            (Seat.South, new Bid(4, Denomination.Spades)), (Seat.North, new Bid(4, Denomination.NoTrump)));
        var hand = Cards("SA", "S8", "S2", "H2", "DA", trumpKing ? "DK" : "D9", "DJ", "D8", "D6", "CQ", "C8", "C5", "C2");

        Assert.Equal(new Bid(level, denomination), engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void MinorTexas_NoFitWithStopper_ReturnsToThreeNoTrump()
    {
        var engine = DeclarativeTexasEngine();
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)), (Seat.North, new Bid(3, Denomination.Diamonds)),
            (Seat.South, new Bid(3, Denomination.Hearts)));

        Assert.Equal(new Bid(3, Denomination.NoTrump), engine.DecideBid(Seat.North,
            Cards("SA", "SK", "S2", "HA", "H8", "H5", "H2", "DQ", "D2", "CK", "C8", "C5", "C2"), auction));
    }

    [Fact]
    public void MinorTexas_ClubFit_FourClubsIsSupportNotDiamondTransfer()
    {
        var engine = DeclarativeTexasEngine();
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(2, Denomination.Spades)), (Seat.North, new Bid(3, Denomination.Clubs)),
            (Seat.South, new Bid(3, Denomination.Hearts)));
        var hand = Cards("SJ", "S8", "S2", "HA", "H8", "H2", "DQ", "D8", "D2", "CA", "CK", "CQ", "C2");
        var call = engine.DecideCall(Seat.North, hand, auction);

        Assert.Equal(new Bid(4, Denomination.Clubs), call.Bid);
        Assert.Equal("one-nt.minor-short.opener-support", engine.ExplainCall(Seat.North, hand, auction.Calls, call).RuleId);
    }

    [Fact]
    public void MinorTexas_BlackwoodConclusion_RemainsInDiamonds()
    {
        var engine = DeclarativeTexasEngine();
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)), (Seat.North, new Bid(3, Denomination.Diamonds)),
            (Seat.South, new Bid(3, Denomination.Hearts)), (Seat.North, new Bid(4, Denomination.Clubs)),
            (Seat.South, new Bid(4, Denomination.Spades)), (Seat.North, new Bid(4, Denomination.NoTrump)),
            (Seat.South, new Bid(5, Denomination.Hearts)));

        Assert.Equal(new Bid(6, Denomination.Diamonds), engine.DecideBid(Seat.North,
            Cards("SA", "SK", "S2", "H8", "H5", "H2", "DK", "DQ", "D2", "CA", "C8", "C5", "C2"), auction));
    }

    [Fact]
    public void MinorTexas_PassTooltipDoesNotHideGameForcingAgreement()
    {
        var engine = DeclarativeTexasEngine();
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Clubs)), (Seat.North, new Bid(3, Denomination.Diamonds)),
            (Seat.South, new Bid(3, Denomination.Hearts)), (Seat.North, new Bid(4, Denomination.Clubs)));
        var explanation = engine.ExplainCall(Seat.South,
            Cards("SQ", "S8", "S2", "H2", "DA", "DQ", "DJ", "D8", "D6", "D2", "CQ", "C8", "C2"),
            auction.Calls, new AuctionCall(Seat.South, AuctionAction.Pass));

        Assert.Equal("pass-forcing", explanation.RuleId);
        Assert.Contains("forcing de manche", Bridge.Desktop.AuctionTooltipRules.Get(explanation.RuleId, explanation.Values));
    }

    private static SefBiddingEngine DeclarativeTexasEngine() => new(profile: BiddingProfile.Load(
        Path.Combine(AppContext.BaseDirectory, "sef-bidding-profile.json")));

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
