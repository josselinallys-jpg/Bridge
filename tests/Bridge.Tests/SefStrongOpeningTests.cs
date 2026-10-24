using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Chapitre SEF : les ouvertures fortes (2♣, 2♦, 2SA).</summary>
public sealed class SefStrongOpeningTests
{
    private readonly SefBiddingEngine _engine = new();

    // --- Ouvertures ---

    [Theory]
    [InlineData(Denomination.Clubs, "strong-two-clubs-opening")]
    [InlineData(Denomination.Diamonds, "strong-two-diamonds-opening")]
    public void Opening_StrongBid_HasSpecificTooltip(Denomination denomination, string ruleId)
    {
        var hand = Cards(
            "SA", "SK", "SQ", "SJ", "S5",
            "HA", "HK", "H4",
            "DK", "D7", "D3",
            "C4", "C2");
        var call = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(2, denomination));

        var explanation = _engine.ExplainCall(Seat.North, hand, Array.Empty<AuctionCall>(), call);

        Assert.Equal(ruleId, explanation.RuleId);
    }

    [Fact]
    public void Opening_GamblingThreeNoTrump_HasSpecificTooltip()
    {
        var hand = Cards(
            "CA", "CK", "CQ", "C8", "C6", "C5", "C2",
            "D7", "D4", "D2",
            "H8", "H3", "S9");
        var call = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(3, Denomination.NoTrump));

        var explanation = _engine.ExplainCall(Seat.North, hand, Array.Empty<AuctionCall>(), call);

        Assert.Equal("strong-gambling-three-no-trump", explanation.RuleId);
    }

    [Fact]
    public void Intervention_LandyHasSpecificTooltip()
    {
        var hand = Cards(
            "SA", "S8", "S6", "S3", "S2",
            "HK", "H8", "H6", "H3",
            "D8", "D6", "C8", "C6");
        var opening = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.NoTrump));
        var intervention = new AuctionCall(Seat.East, AuctionAction.Bid, new Bid(2, Denomination.Clubs));

        var explanation = _engine.ExplainCall(Seat.East, hand, [opening], intervention);

        Assert.Equal("landy-two-suited", explanation.RuleId);
    }

    [Fact]
    public void Tooltip_GamblingOpeningDisplaysTheLongMinor()
    {
        var hand = Cards(
            "CA", "CK", "CQ", "C8", "C6", "C5", "C2",
            "D7", "D4", "D2",
            "H8", "H3", "S9");
        var call = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(3, Denomination.NoTrump));

        var explanation = _engine.ExplainCall(Seat.North, hand, Array.Empty<AuctionCall>(), call);

        Assert.Equal("strong-gambling-three-no-trump", explanation.RuleId);
        Assert.Equal("Trèfles", explanation.Values["couleur"]);
        Assert.Equal("7", explanation.Values["cartes"]);
    }

    [Fact]
    public void Tooltip_MajorRaiseHldIncludesNineTrumpBonus()
    {
        var hand = Cards(
            "SA", "S9", "S8", "S2",
            "HK", "H4", "H2",
            "D8", "D6", "D4", "D2",
            "C8", "C6");
        var opening = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.Spades));
        var raise = new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(3, Denomination.Spades));

        var explanation = _engine.ExplainCall(Seat.South, hand, [opening], raise);

        Assert.Equal("major-support-3", explanation.RuleId);
        Assert.Equal("10", explanation.Values["HLD"]);
    }

    [Fact]
    public void StrongTwoNoTrump_FiveFiveResponseHasSpecificTooltip()
    {
        var hand = Cards(
            "HA", "HK", "H9", "H7", "H6",
            "SA", "SK", "S9", "S7", "S6",
            "D7", "D6", "C8");
        var opening = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(2, Denomination.NoTrump));
        var response = new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(4, Denomination.Diamonds));

        var explanation = _engine.ExplainCall(Seat.South, hand, [opening], response);

        Assert.Equal("strong-two-nt-five-five", explanation.RuleId);
    }

    [Fact]
    public void Tooltip_OneNoTrumpFourDiamondsFiveFiveShowsTheRightConvention()
    {
        var hand = Cards(
            "HA", "H8", "H6", "H4", "H2",
            "SK", "S8", "S6", "S4", "S2",
            "D8", "D6", "C8");
        var opening = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.NoTrump));
        var response = new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(4, Denomination.Diamonds));

        var explanation = _engine.ExplainCall(Seat.South, hand, [opening], response);

        Assert.Equal("one-nt-major-two-suited", explanation.RuleId);
        Assert.Contains("H", explanation.Values["zone"]);
    }

    [Fact]
    public void Tooltip_BlackwoodFiveHeartsMeansTwoOrFiveWithoutTrumpQueen()
    {
        var hand = Cards(
            "SA", "SK", "S4",
            "HK", "H8", "H4",
            "DA", "D8", "D4",
            "CA", "C8", "C4", "C2");
        var calls = new AuctionCall[]
        {
            new(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.Spades)),
            new(Seat.South, AuctionAction.Bid, new Bid(2, Denomination.Spades)),
            new(Seat.North, AuctionAction.Bid, new Bid(4, Denomination.NoTrump))
        };
        var response = new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(5, Denomination.Hearts));

        var explanation = _engine.ExplainCall(Seat.South, hand, calls, response);

        Assert.Equal("blackwood-2-5-no-queen", explanation.RuleId);
    }

    [Fact]
    public void Tooltip_BlackwoodDirectlyOverPartnerPreemptIsRecognized()
    {
        var hand = Cards(
            "SA", "SK", "S8", "S4",
            "HA", "HK", "H8",
            "D8", "D6", "D4",
            "C8", "C6", "C4");
        var preempt = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(3, Denomination.Spades));
        var ask = new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(4, Denomination.NoTrump));

        var explanation = _engine.ExplainCall(Seat.South, hand, [preempt], ask);

        Assert.Equal("blackwood-ask", explanation.RuleId);
    }

    [Theory]
    [InlineData(Denomination.Hearts, "blackwood-2-5-no-queen")]
    [InlineData(Denomination.Spades, "blackwood-2-5-queen")]
    public void Tooltip_BlackwoodResponseNamesTrumpQueen(Denomination responseSuit, string ruleId)
    {
        var hand = Cards(
            "SA", "SK", "SQ", "S4",
            "HK", "H8", "H4",
            "DA", "D8", "D4",
            "CA", "C8", "C4");
        var opening = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.Spades));
        var agreement = new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(2, Denomination.Spades));
        var ask = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(4, Denomination.NoTrump));
        var response = new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(5, responseSuit));

        var explanation = _engine.ExplainCall(Seat.South, hand, [opening, agreement, ask], response);

        Assert.Equal(ruleId, explanation.RuleId);
    }

    [Fact]
    public void Opening_StrongFiveCardSuitTwentyOneHl_OpensTwoClubs()
    {
        var hand = Cards(
            "SA", "SK", "SQ", "SJ", "S5",
            "HA", "HK", "H4",
            "DK", "D7", "D3",
            "C4", "C2");

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void TwoNoTrump_SixCardMajor_ConcludesAtFourMajor()
    {
        var responder = Cards(
            "H7", "H6", "H5", "H4", "H3", "H2",
            "S9", "S7", "S5",
            "D8", "D6",
            "C8", "C6");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void TwoNoTrump_BalancedFourHonorLengthPoints_BidsThreeNoTrump()
    {
        var responder = Cards(
            "SA", "S7", "S5",
            "HK", "H8", "H4",
            "D8", "D6", "D4",
            "CQ", "C5", "C3", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void Opening_TwentyHonorLengthWithFiveCardMinor_OpensTwoNoTrump()
    {
        var hand = Cards(
            "CA", "CK", "CJ", "C8", "C2",
            "DQ", "D7", "D3",
            "HA", "HK", "H2",
            "SQ", "S4");

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_NineteenHonorLengthWithFiveCardMinor_DoesNotOpenTwoNoTrump()
    {
        var hand = Cards(
            "CA", "CK", "CJ", "C8", "C2",
            "D8", "D7", "D3",
            "HA", "HK", "H2",
            "SK", "S4");

        Assert.Equal(new Bid(1, Denomination.Clubs),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_TwentyFourHonorLengthOpensTwoDiamonds()
    {
        var hand = Cards(
            "SA", "SK", "SQ", "S6", "S4",
            "HA", "HK", "HQ",
            "DK", "DQ", "D3",
            "CJ", "C4");

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_NinePlayingTricksAndTwoDefensiveTricks_OpensTwoDiamonds()
    {
        var hand = Cards(
            "SA", "SK", "SQ", "SJ", "S9", "S7", "S2",
            "DA", "D5",
            "HA", "H4",
            "C8", "C6");

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void Opening_LongMajorWithoutTwoDefensiveTricks_DoesNotForceTwoDiamonds()
    {
        var hand = Cards(
            "SA", "SK", "SQ", "SJ", "S9", "S7", "S2",
            "DA", "D5",
            "H8", "H4",
            "C8", "C6");

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Fact]
    public void StrictProfile_StrongClubsPrefersDescribedFiveCardMajorToTwoNoTrump()
    {
        var engine = StrictSefEngine();
        var hand = Cards("HA HK HQ H5 H2 SA SK S2 DA D2 D3 CJ C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.Hearts), engine.DecideBid(Seat.North, hand, auction));
        Assert.Equal("strong-clubs.rebid.major", engine.LastConfiguredRuleId);
    }

    [Fact]
    public void StrictProfile_StrongClubsUsesTwoNoTrumpWithoutFiveCardMajor()
    {
        var engine = StrictSefEngine();
        var hand = Cards("SA SK SJ S2 HA HK H2 DA D2 CQ CJ C2 C3");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.NoTrump), engine.DecideBid(Seat.North, hand, auction));
        Assert.Equal("strong-clubs.rebid.nt", engine.LastConfiguredRuleId);
    }

    [Fact]
    public void StrictProfile_StrongClubsSevenCardMajorRequiresPlayingAndDefensiveTricks()
    {
        var engine = StrictSefEngine();
        var hand = Cards("HA HK H10 H9 H8 H7 H2 SK S2 DK D2 CQ C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.Hearts), engine.DecideBid(Seat.North, hand, auction));
        Assert.Equal("strong-clubs.rebid.seven-major", engine.LastConfiguredRuleId);
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
    public void GameForce_TwoAcesOfSameRank_BidsThreeSpades()
    {
        var responder = Cards(
            "SA", "S5", "S3",
            "HA", "H8", "H4",
            "D8", "D6", "D4", "D3",
            "C6", "C5", "C3");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Theory]
    [InlineData("SA", "CA", 3, Denomination.Hearts)]
    [InlineData("SA", "HA", 3, Denomination.Spades)]
    [InlineData("SA", "DA", 3, Denomination.NoTrump)]
    public void GameForce_TwoAces_DescribesAceGroup(
        string firstAce, string secondAce, int expectedLevel, Denomination expectedSuit)
    {
        var responder = Cards(
            firstAce, secondAce,
            "S7", "S5", "S3",
            "H8", "H4", "H2",
            "D8", "D4", "D2",
            "C8", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(expectedLevel, expectedSuit),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void GameForce_EightPointsWithoutAceAndWithoutShortness_BidsTwoNoTrump()
    {
        var responder = Cards(
            "SK", "SQ", "S5",
            "HK", "H8", "H4",
            "D8", "D6", "D4",
            "C6", "C5", "C3", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void GameForce_EightPointsWithoutAceAndWithShortness_BidsTwoHearts()
    {
        var responder = Cards(
            "SK", "SQ", "S5", "S3",
            "HK", "H8", "H6", "H4",
            "D8", "D6", "D4", "D3",
            "C5");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Développements après 2♦ ---

    [Fact]
    public void GameForce_WeakFit_UsesTwoNoTrumpDefault()
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

        Assert.Equal(new Bid(2, Denomination.NoTrump),
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

    [Fact]
    public void GameForce_ThreeNoTrumpResponse_IsConclusive()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S2",
            "HA", "HK", "H4",
            "DA", "D8", "D3",
            "CQ", "C9", "C5");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Diamonds)),
            (Seat.South, new Bid(3, Denomination.NoTrump)));

        Assert.Null(_engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void GameForce_OpenerWithSixCardMajor_RebidsAtThree()
    {
        var opener = Cards(
            "HA", "HK", "HQ", "HJ", "H9", "H2",
            "SA", "SK", "S4",
            "DQ", "D7", "D3",
            "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Diamonds)),
            (Seat.South, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void GameForce_OpenerWithFiveCardMajor_RebidsAtTwo()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "SJ", "S2",
            "HA", "HK", "HQ", "H9",
            "DK", "D8", "D3",
            "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Diamonds)),
            (Seat.South, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void GameForce_FourCardFitWithSingleton_UsesSplinter()
    {
        var responder = Cards(
            "SQ", "S9", "S7", "S5",
            "H2",
            "DQ", "D6", "D4",
            "C8", "C6", "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Diamonds)),
            (Seat.South, new Bid(2, Denomination.Hearts)),
            (Seat.North, new Bid(2, Denomination.Spades)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    // --- Développements après 2♣ ---

    [Fact]
    public void StrongTwoClubs_FitAndValues_ShowsSlamInterestAtThree()
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

        Assert.Equal(new Bid(3, Denomination.Spades),
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

    [Fact]
    public void StrongTwoClubsRelay_FiveFiveMajorThreeLosers_RebidsThreeNoTrump()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "SJ", "S2",
            "HA", "HK", "HQ", "HJ", "H3",
            "DA", "DK", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void StrongTwoClubsRelay_SixHeartsFiveSpadesThreeLosers_RebidsFourClubs()
    {
        var opener = Cards(
            "HA", "HK", "HQ", "HJ", "H9", "H2",
            "SA", "SK", "SQ", "SJ", "S2",
            "DA", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(4, Denomination.Clubs),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void StrongTwoClubsRelay_SevenCardMajor_RebidsAtThree()
    {
        var opener = Cards(
            "SA", "SK", "SQ", "SJ", "S10", "S9", "S2",
            "HA", "HK", "H4",
            "DQ", "D3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void StrongTwoClubsMajorRebid_FiveHonorPoints_UsesForcingTwoNoTrump()
    {
        var responder = Cards(
            "SQ", "S7", "S5",
            "HK", "H4",
            "D7", "D6", "D3", "D2",
            "C8", "C6", "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubsMajorRebid_FiveCardPositiveSuit_BidsAtThree()
    {
        var responder = Cards(
            "SQ", "S7", "S4",
            "HK", "H4",
            "CA", "CK", "C8", "C6", "C2",
            "D8", "D6", "D2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubsMajorRebid_ThreeCardFit_ShowsSlamInterestAtThree()
    {
        var responder = Cards(
            "HK", "H8", "H4",
            "SA", "S7", "S4",
            "DQ", "D6", "D3", "D2",
            "C8", "C6", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubsMajorRebid_AtLeastFivePoints_UsesForcingTwoNoTrump()
    {
        var responder = Cards(
            "SQ", "S7", "S5",
            "HK", "H4",
            "D7", "D6", "D3", "D2",
            "C8", "C6", "C4", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(2, Denomination.Hearts)));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubsMinorRebid_FitAtFour_ShowsSlamInterestAtFour()
    {
        var responder = Cards(
            "C8", "C7", "C6", "C5",
            "SA", "SK", "S4",
            "HA", "HK", "H4",
            "D8", "D6", "D2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(3, Denomination.Clubs)));

        Assert.Equal(new Bid(4, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubsThreeLevelMajorRebid_FitAndControl_BidsControl()
    {
        var responder = Cards(
            "HK", "H8", "H4",
            "CA", "C7", "C4",
            "SA", "S8", "S4",
            "DQ", "D6", "D3", "D2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(3, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubsThreeLevelMajorRebid_FitWithoutControl_ConcludesGame()
    {
        var responder = Cards(
            "H9", "H8", "H7", "H6",
            "S9", "S8", "S7",
            "D9", "D8", "D7",
            "C9", "C8", "C7");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.Clubs)),
            (Seat.South, new Bid(2, Denomination.Diamonds)),
            (Seat.North, new Bid(3, Denomination.Hearts)));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubs_AfterTwoLevelIntervention_BidsNaturalFiveCardSuit()
    {
        var responder = Cards(
            "HA", "HK", "H8", "H6", "H4",
            "S8", "S6", "S4",
            "D8", "D6", "D4",
            "C8", "C6");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.East, new Bid(2, Denomination.Diamonds));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void StrongTwoClubs_AfterDouble_RedoublesWithFiveHonorPoints()
    {
        var responder = Cards(
            "SK", "SQ", "S7", "S5",
            "HK", "H8", "H4",
            "D8", "D6", "D4",
            "C8", "C6", "C4");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.East, AuctionAction.Double);

        Assert.Equal(AuctionAction.Redouble,
            _engine.DecideCall(Seat.South, responder, auction).Action);
    }

    [Fact]
    public void StrongTwoClubs_AfterRelayAndInterventionWithoutStop_PassesForcing()
    {
        var opener = Cards(
            "S8", "S6", "S4",
            "HA", "HK", "HQ",
            "DA", "DK", "DQ",
            "CA", "CK", "CQ", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Diamonds));
        auction.AddCall(Seat.West, new Bid(2, Denomination.Spades));

        Assert.Null(_engine.DecideBid(Seat.North, opener, auction));
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
    public void TwoNoTrump_FiveFiveMajors_UsesFourDiamondsAndOpenerChooses()
    {
        var responder = Cards(
            "HA", "HK", "H9", "H7", "H6",
            "SA", "SK", "S9", "S7", "S6",
            "D7", "D6", "C8");
        var opener = Cards(
            "SA", "SK", "S5",
            "HA", "HK", "HQ", "H4",
            "DA", "DQ", "D3",
            "CK", "C5", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.Diamonds),
            _engine.DecideBid(Seat.South, responder, auction));
        auction.AddCall(Seat.South, new Bid(4, Denomination.Diamonds));
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void TwoNoTrump_FourClubsTexasDiamonds_IsRectifiedToFourDiamonds()
    {
        var responder = Cards(
            "DA", "DK", "DQ", "DJ", "D10", "D8",
            "SA", "S6", "H8", "H6", "C8", "C6", "C4");
        var opener = Cards(
            "SA", "SK", "S5",
            "HA", "HK", "H4",
            "DA", "DQ", "D4",
            "CK", "C5", "C3", "C2");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
        auction.AddCall(Seat.South, new Bid(4, Denomination.Clubs));
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(4, Denomination.Diamonds),
            _engine.DecideBid(Seat.North, opener, auction));
    }

    [Fact]
    public void TwoNoTrump_ElevenPoints_BidsQuantitativeFourNoTrump()
    {
        var responder = Cards(
            "SK", "S7", "S5",
            "HQ", "HJ", "H4",
            "DK", "DJ", "D4",
            "CQ", "C5", "C3", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void TwoNoTrump_FourHonorLengthWithFourCardMajor_UsesStayman()
    {
        var responder = Cards(
            "S8", "S6", "S4", "S2",
            "HK", "HQ", "H4", "H2",
            "D8", "D6", "D4",
            "C8", "C6");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void TwoNoTrump_TwelveBalancedHonorLengthPoints_InvitesWithFourNoTrump()
    {
        var responder = Cards(
            "SK", "S7", "S5",
            "HQ", "HJ", "H4",
            "DK", "DJ", "D4",
            "CQ", "C5", "C3", "C2");
        var auction = AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(4, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, responder, auction));
    }

    [Fact]
    public void TwoNoTrump_TexasWithoutFit_OpenerReturnsToNoTrumpAndResponderPasses()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S2",
            "HK", "H4",
            "DK", "D8", "D3",
            "CQ", "C9", "C5", "C2");
        var responder = Cards(
            "HA", "HQ", "H9", "H7", "H6",
            "S8", "S6", "S4",
            "D7", "D6", "D4",
            "C8", "C6");
        var auction = AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.NoTrump)),
            (Seat.South, new Bid(3, Denomination.Diamonds)),
            (Seat.North, new Bid(3, Denomination.NoTrump)));

        Assert.Null(_engine.DecideBid(Seat.South, responder, auction));
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

    [Fact]
    public void Profile_TwoNoTrump_UsesConfiguredHonorLengthRange()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            TwoNoTrump = new() { Minimum = 18, Maximum = 19, AllowFiveCardMajor = false }
        });
        var hand = Cards("CA", "CK", "CJ", "C8", "C2", "D8", "D7", "D3", "HA", "HK", "H2", "SK", "S4");

        Assert.Equal(new Bid(2, Denomination.NoTrump), engine.DecideBid(Seat.North, hand, new Auction()));
    }

    [Theory]
    [InlineData(true, 4, Denomination.Clubs)]
    [InlineData(true, 5, Denomination.NoTrump)]
    [InlineData(false, 4, Denomination.NoTrump)]
    public void Profile_TwoNoTrumpStayman_UsesActivationAndThreshold(bool enabled, int threshold, Denomination expected)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Conventions = new() { Stayman = enabled, TwoNoTrumpStaymanMinimumHL = threshold }
        });
        var hand = Cards("SA", "S7", "S4", "S2", "H8", "H5", "H2", "D8", "D6", "D3", "C6", "C5", "C2");

        Assert.Equal(new Bid(3, expected), engine.DecideBid(Seat.South, hand,
            AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)))));
    }

    [Theory]
    [InlineData(true, 3, Denomination.Diamonds)]
    [InlineData(false, 4, Denomination.Hearts)]
    public void Profile_TwoNoTrumpMajorTransfers_HaveNaturalFallback(bool enabled, int level, Denomination expected)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Conventions = new() { MajorTransfers = enabled }
        });
        var hand = Cards("S7", "S4", "S2", "HQ", "H8", "H7", "H5", "H2", "D8", "D3", "C6", "C5", "C2");

        Assert.Equal(new Bid(level, expected), engine.DecideBid(Seat.South, hand,
            AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)))));
    }

    [Theory]
    [InlineData(true, 3, Denomination.Spades)]
    [InlineData(false, 5, Denomination.Clubs)]
    public void Profile_TwoNoTrumpMinorTransfers_HaveNaturalFallback(bool enabled, int level, Denomination expected)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Conventions = new() { MinorTransfers = enabled }
        });
        var hand = Cards("SA", "SK", "S2", "HA", "HK", "H2", "D2", "C9", "C8", "C7", "C6", "C5", "C2");

        Assert.Equal(new Bid(level, expected), engine.DecideBid(Seat.South, hand,
            AuctionWithBids((Seat.North, new Bid(2, Denomination.NoTrump)))));
    }

    [Theory]
    [InlineData(3, Denomination.Clubs)]
    [InlineData(3, Denomination.Diamonds)]
    [InlineData(3, Denomination.Hearts)]
    [InlineData(3, Denomination.Spades)]
    [InlineData(4, Denomination.Clubs)]
    public void Profile_TwoNoTrumpDisabledConventions_OpenerDoesNotRectify(int level, Denomination response)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Conventions = new() { Stayman = false, MajorTransfers = false, MinorTransfers = false }
        });
        var hand = Cards("SA", "SK", "S7", "S4", "HQ", "H6", "H3", "DA", "D8", "D3", "CQ", "C9", "C2");

        Assert.Null(engine.DecideBid(Seat.North, hand, AuctionWithBids(
            (Seat.North, new Bid(2, Denomination.NoTrump)), (Seat.South, new Bid(level, response)))));
    }

    [Theory]
    [InlineData(Seat.North)]
    [InlineData(Seat.South)]
    public void StrongNoTrumpStayman_FivePointsConcludesThreeNoTrump(Seat responder)
    {
        var engine = new SefBiddingEngine(profile: BiddingProfile.Load(
            Path.Combine(AppContext.BaseDirectory, "sef-bidding-profile.json")));
        var opener = responder.Partner();
        var hand = Cards("HK", "HQ", "H8", "H2", "S9", "S7", "S3", "D9", "D6", "D2", "C9", "C5", "C2");
        var auction = AuctionWithBids(
            (opener, new Bid(2, Denomination.Clubs)),
            (responder, new Bid(2, Denomination.Diamonds)),
            (opener, new Bid(2, Denomination.NoTrump)),
            (responder, new Bid(3, Denomination.Clubs)),
            (opener, new Bid(3, Denomination.Diamonds)));

        Assert.Equal(5, new HandEvaluation(hand).HighCardPoints);
        var call = engine.DecideCall(responder, hand, auction);

        Assert.Equal(new Bid(3, Denomination.NoTrump), call.Bid);
        Assert.Equal("strong-nt.stayman.conclude-no-major", engine.LastConfiguredRuleId);
        var explanation = engine.ExplainCall(responder, hand, auction.Calls, call);
        Assert.Equal(engine.LastConfiguredRuleId, explanation.RuleId);
        Assert.Contains("3SA", explanation.Values["descriptionRegle"]);
    }

    [Fact]
    public void StrongNoTrumpStayman_FullSequenceWithNorthFivePointsEndsAtThreeNoTrump()
    {
        var engine = new SefBiddingEngine(profile: BiddingProfile.Load(
            Path.Combine(AppContext.BaseDirectory, "sef-bidding-profile.json")));
        var openerHand = Cards("CA", "CK", "CQ", "CJ", "DA", "DK", "D2", "HA", "H8", "H2", "SJ", "S8", "S2");
        var northHand = Cards("HK", "HQ", "H7", "H3", "S9", "S7", "S3", "D9", "D6", "D3", "C9", "C5", "C2");
        var expected = new[]
        {
            (Seat.South, new Bid(2, Denomination.Clubs)),
            (Seat.North, new Bid(2, Denomination.Diamonds)),
            (Seat.South, new Bid(2, Denomination.NoTrump)),
            (Seat.North, new Bid(3, Denomination.Clubs)),
            (Seat.South, new Bid(3, Denomination.Diamonds)),
            (Seat.North, new Bid(3, Denomination.NoTrump))
        };
        var auction = new Auction();

        Assert.Equal(22, new HandEvaluation(openerHand).HighCardPoints);
        Assert.Equal(5, new HandEvaluation(northHand).HighCardPoints);
        foreach (var (seat, bid) in expected)
        {
            while (auction.Calls.Count % 4 != (int)seat)
                auction.AddCall((Seat)(auction.Calls.Count % 4), null);
            var call = engine.DecideCall(seat, seat == Seat.North ? northHand : openerHand, auction);
            Assert.Equal(bid, call.Bid);
            auction.AddCall(call);
        }
    }

    private static List<Card> Cards(params string[] cards) => cards
        .SelectMany(card => card.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Select(Parse).ToList();

    private static SefBiddingEngine StrictSefEngine() => new(profile: BiddingProfile.Load(
        Path.Combine(AppContext.BaseDirectory, "sef-bidding-profile.json")));

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
