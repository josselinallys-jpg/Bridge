using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Interventions et réveils SEF.</summary>
public sealed class SefCompetitiveTests
{
    private readonly SefBiddingEngine _engine = new();

    [Fact]
    public void Intervention_OneNoTrumpWithSixteenPointsAndGuard_BidsOneNoTrump()
    {
        var hand = Cards(
            "SA", "S6", "S4",
            "HK", "HQ", "H4",
            "DA", "D8", "D3",
            "CK", "C4", "C3", "C2");
        var auction = AuctionAfterOpponent(new Bid(1, Denomination.Hearts));

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void Intervention_SixCardRobustSuitAtLevelTwo_BidsTwoSpades()
    {
        var hand = Cards(
            "SK", "SQ", "SJ", "S9", "S7", "S4",
            "H8", "H3", "D6", "D2", "C8", "C5", "C3");
        var auction = AuctionAfterOpponent(new Bid(1, Denomination.Diamonds));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void Intervention_WeakFiveCardSuit_Passes()
    {
        var hand = Cards(
            "S9", "S7", "S4", "S2",
            "HK", "H6", "H3",
            "D8", "D5", "D2",
            "C9", "C6", "C3");
        var auction = AuctionAfterOpponent(new Bid(1, Denomination.Diamonds));

        Assert.Null(_engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void Reopening_AfterTwoPasses_WithFiveCardSuit_BidsOneSpade()
    {
        var hand = Cards(
            "SK", "SQ", "S8", "S5", "S3",
            "HK", "H4", "H2",
            "D9", "D6", "D3",
            "C8", "C2");
        var auction = AuctionAfterTwoPasses(new Bid(1, Denomination.Diamonds));

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, auction));
    }

    [Fact]
    public void Reopening_AfterTwoPasses_WithTenPoints_CanDouble()
    {
        var hand = Cards(
            "SA", "SK", "S4",
            "HK", "HQ", "H4",
            "D8", "D5", "D2",
            "C9", "C6", "C3", "C2");
        var auction = AuctionAfterTwoPasses(new Bid(1, Denomination.Hearts));

        Assert.Equal(AuctionAction.Double,
            _engine.DecideCall(Seat.North, hand, auction).Action);
    }

    [Fact]
    public void TakeoutDouble_PartnerWithFourCardMajor_AnswersOneHeart()
    {
        var hand = Cards(
            "HK", "HQ", "H7", "H4",
            "S7", "S6", "S3",
            "D8", "D5", "D2",
            "C9", "C6", "C3");
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.Diamonds));
        auction.AddCall(Seat.South, AuctionAction.Double);
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(1, Denomination.Hearts),
            _engine.DecideBid(Seat.North, hand, auction));
    }

    [Fact]
    public void TakeoutDouble_OpponentRedoublesWithWeakHand_Passes()
    {
        var hand = Cards(
            "H8", "H6", "H3",
            "S7", "S6", "S3",
            "D8", "D6", "D3",
            "C9", "C6", "C4", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.Diamonds));
        auction.AddCall(Seat.South, AuctionAction.Double);
        auction.AddCall(Seat.West, AuctionAction.Redouble);

        Assert.Equal(AuctionAction.Pass,
            _engine.DecideCall(Seat.North, hand, auction).Action);
    }

    [Fact]
    public void TakeoutDouble_OpponentBidsAgainWithoutFit_Passes()
    {
        var hand = Cards(
            "S8", "S6", "S3",
            "H8", "H6", "H3",
            "D8", "D6", "D3",
            "C8", "C6", "C4", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Diamonds));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, null);
        auction.AddCall(Seat.West, new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.North, AuctionAction.Double);
        auction.AddCall(Seat.East, new Bid(2, Denomination.Spades));

        Assert.Equal(AuctionAction.Pass,
            _engine.DecideCall(Seat.South, hand, auction).Action);
    }

    [Fact]
    public void MajorOpeningIntervention_ThreeCardFitEightToTenHld_RaisesSimply()
    {
        var hand = Cards(
            "SK", "S7", "S3",
            "HQ", "H6", "H3",
            "D8", "D6", "D3",
            "C8", "C6", "C4", "C2");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Spades), new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MajorOpeningOneNoTrumpIntervention_NineHonorPoints_DoublesForPenalty()
    {
        var hand = Cards(
            "SK", "S7", "S3",
            "HA", "H6", "H3",
            "DQ", "D6", "D3",
            "C8", "C6", "C4", "C2");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Spades), new Bid(1, Denomination.NoTrump));

        Assert.Equal(AuctionAction.Double,
            _engine.DecideCall(Seat.West, hand, auction).Action);
    }

    [Fact]
    public void MajorOpeningDouble_FourCardFitElevenToTwelveHld_UsesTruscottTwoNoTrump()
    {
        var hand = Cards(
            "SK", "S7", "S3", "S2",
            "HA", "H6", "H3",
            "DQ", "D6", "D3",
            "C8", "C6", "C4");
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.South, AuctionAction.Double);
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MajorOpeningDouble_ThreeCardFitElevenToTwelveHld_Redoubles()
    {
        var hand = Cards(
            "SK", "S7", "S3",
            "HA", "HK",
            "DJ", "D6", "D3",
            "C8", "C6", "C4", "C3", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.South, AuctionAction.Double);
        auction.AddCall(Seat.West, null);

        Assert.Equal(AuctionAction.Redouble,
            _engine.DecideCall(Seat.West, hand, auction).Action);
    }

    [Fact]
    public void MajorOpeningCueBid_ThreeCardFitEightToTenHld_RaisesSimply()
    {
        var hand = Cards(
            "SK", "S7", "S3",
            "HQ", "H6", "H3",
            "DQ", "D6", "D3",
            "CK", "C6", "C4", "C2");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Spades), new Bid(2, Denomination.Spades));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MajorOpeningIntervention_FourCardFitSingletonInInterventionSuit_UsesSplinter()
    {
        var hand = Cards(
            "SK", "SQ", "S8", "S6",
            "H2",
            "DA", "D6", "D3",
            "CK", "C8", "C6", "C4", "C2");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Spades), new Bid(2, Denomination.Hearts));

        Assert.Equal(new Bid(4, Denomination.Hearts),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MajorOpeningIntervention_TwoOverOneSixCardsEightToTenH_BidsNonForcingColor()
    {
        var hand = Cards(
            "S8", "S6",
            "HA", "H8", "H6", "H3", "H2", "H4",
            "DQ", "D6",
            "CK", "C6", "C4");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Spades), new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MajorOpeningOneNoTrumpIntervention_SixCardColor_BidsNonForcingColor()
    {
        var hand = Cards(
            "S8", "S6",
            "HA", "H8", "H6", "H3", "H2", "H4",
            "D8", "D6",
            "C8", "C6", "C4");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Spades), new Bid(1, Denomination.NoTrump));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MajorOpeningTwoSuitedIntervention_FourCardFitElevenToTwelveHld_UsesTwoNoTrump()
    {
        var hand = Cards(
            "SK", "S7", "S3", "S2",
            "HA", "H6", "H3",
            "DQ", "D6", "D3",
            "C8", "C6", "C4");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Spades), new Bid(2, Denomination.Spades));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void InterventionSequence_AfterOpponentGameBid_DoesNotLaunchBlackwoodWithoutFit()
    {
        var hand = Cards(
            "DA", "DK", "D8", "D6",
            "H8", "H6", "H3",
            "S8", "S6", "S3",
            "C8", "C6", "C4");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, new Bid(2, Denomination.Diamonds));
        auction.AddCall(Seat.South, new Bid(3, Denomination.Clubs));
        auction.AddCall(Seat.West, new Bid(3, Denomination.Diamonds));
        auction.AddCall(Seat.North, new Bid(4, Denomination.Spades));

        var call = _engine.DecideCall(Seat.East, hand, auction);

        Assert.NotEqual(new Bid(4, Denomination.NoTrump), call.Bid);
    }

    [Fact]
    public void Landy_PartnerWithFiveSpades_AnswersTwoSpades()
    {
        var hand = Cards(
            "SK", "SQ", "S8", "S5", "S3",
            "H7", "H4", "H2",
            "D9", "D6", "D3",
            "C8", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.South, new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, auction));
    }

    [Fact]
    public void InterventionResponse_ThreeCardSupport_RaisesPartnerSuit()
    {
        var hand = Cards(
            "SK", "S7", "S3",
            "HQ", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C6", "C4", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.Diamonds));
        auction.AddCall(Seat.South, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.North, hand, auction));
    }

    [Fact]
    public void InterventionResponse_ThreeCardSupportDoesNotRaiseSixLevelContract()
    {
        var hand = Cards(
            "S9", "S6", "S3",
            "HQ", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C6", "C4", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Clubs));
        auction.AddCall(Seat.East, new Bid(6, Denomination.Hearts));

        Assert.Null(_engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void MinorOpeningResponse_DoesNotRaiseSixLevelInterventionToSeven()
    {
        var hand = Cards(
            "S9", "S6", "S3",
            "HQ", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C6", "C4", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Clubs));
        auction.AddCall(Seat.East, new Bid(6, Denomination.Hearts));

        Assert.Null(_engine.DecideBid(Seat.South, hand, auction));
    }

    [Fact]
    public void MinorOpeningIntervention_OneOverOne_RequiresFourCardsAndSevenHl()
    {
        var hand = Cards(
            "SK", "S8", "S4", "S2",
            "HA", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C6", "C4");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Clubs), new Bid(1, Denomination.Hearts));

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningIntervention_TwoOverOne_RequiresFiveCardsAndElevenHl()
    {
        var hand = Cards(
            "SA", "SK", "S8", "S5", "S2",
            "HA", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C4");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Clubs), new Bid(2, Denomination.Hearts));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningIntervention_FiveCardSupport_UsesTwoOfMinorWithSixToEightH()
    {
        var hand = Cards(
            "SK", "S6", "S3",
            "HQ", "HJ",
            "D9", "D5", "D2",
            "C8", "C6", "C4", "C3", "C2");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Clubs), new Bid(1, Denomination.Hearts));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningIntervention_DoubleAfterOneDiamond_DeniesFourCardMajor()
    {
        var hand = Cards(
            "SA", "S7", "S3",
            "HQ", "H6", "H3",
            "DJ", "D5", "D2",
            "CJ", "C6", "C4", "C2");
        var auction = PartnerMinorWithIntervention(new Bid(1, Denomination.Clubs), new Bid(1, Denomination.Diamonds));

        Assert.Equal(AuctionAction.Double,
            _engine.DecideCall(Seat.West, hand, auction).Action);
    }

    [Fact]
    public void Intervention_WeakNorthAfterWestOneClub_Passes()
    {
        var hand = Cards(
            "SK",
            "HA", "H8",
            "DK", "D8", "D5",
            "CQ", "C10", "C8", "C7", "C5", "C4", "C2");
        var auction = new Auction();
        auction.AddCall(Seat.West, new Bid(1, Denomination.Clubs));

        Assert.Equal(AuctionAction.Pass,
            _engine.DecideCall(Seat.North, hand, auction).Action);
    }

    // --- Réponses après un contre d'appel adverse sur l'ouverture mineure ---

    [Fact]
    public void MinorOpeningDouble_OneOverOne_RequiresFourCardsAndSevenHl()
    {
        var hand = Cards(
            "SK", "S8", "S4", "S2",
            "HA", "H6", "H3",
            "D9", "D5", "D2",
            "C8", "C6", "C4");
        var auction = PartnerMinorWithDouble(new Bid(1, Denomination.Clubs));

        Assert.Equal(new Bid(1, Denomination.Spades),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningDouble_TwoOverOne_RequiresSixCardsAndSevenToTenH()
    {
        var hand = Cards(
            "CK", "CQ", "C8", "C5", "C3", "C2",
            "HA", "H6", "H3",
            "D9", "D5",
            "S8", "S6");
        var auction = PartnerMinorWithDouble(new Bid(1, Denomination.Diamonds));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningDouble_SevenCardWeakSuit_JumpsToBarrage()
    {
        var hand = Cards(
            "HK", "HQ", "H8", "H6", "H4", "H3", "H2",
            "S8", "S4",
            "D9", "D5",
            "C6", "C4");
        var auction = PartnerMinorWithDouble(new Bid(1, Denomination.Clubs));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningDouble_BalancedEightToTenHl_BidsOneNoTrump()
    {
        var hand = Cards(
            "SK", "S8", "S3",
            "HQ", "H6", "H3",
            "DA", "D9", "D5",
            "C8", "C6", "C4", "C2");
        var auction = PartnerMinorWithDouble(new Bid(1, Denomination.Diamonds));

        Assert.Equal(new Bid(1, Denomination.NoTrump),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningDouble_TenHlWithoutNaturalBid_Redoubles()
    {
        var hand = Cards(
            "SA", "SQ", "S6",
            "HK", "H6", "H3",
            "DQ", "D8", "D5",
            "C9", "C6", "C4", "C2");
        var auction = PartnerMinorWithDouble(new Bid(1, Denomination.Clubs));

        Assert.Equal(AuctionAction.Redouble,
            _engine.DecideCall(Seat.West, hand, auction).Action);
    }

    [Fact]
    public void MinorOpeningDouble_FiveCardSupportNineHl_BidsTruscottTwoNoTrump()
    {
        var hand = Cards(
            "CA", "CK", "C8", "C6", "C3",
            "SQ", "S6", "S3",
            "H8", "H6", "H3",
            "D8", "D6");
        var auction = PartnerMinorWithDouble(new Bid(1, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningDouble_FiveCardSupportSevenToEightH_JumpsToBarrageRaise()
    {
        var hand = Cards(
            "CK", "C8", "C6", "C3", "C2",
            "SQ", "S6", "S3",
            "H8", "H6", "H3",
            "DK", "D6");
        var auction = PartnerMinorWithDouble(new Bid(1, Denomination.Clubs));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    // --- Réponses après une intervention par 1 Sans-Atout sur l'ouverture mineure ---

    [Fact]
    public void MinorOpeningOneNoTrumpIntervention_NineHl_DoublesForPenalty()
    {
        var hand = Cards(
            "SK", "S8", "S3",
            "HQ", "H6", "H3",
            "DA", "D6", "D3",
            "C8", "C6", "C4", "C2");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(1, Denomination.NoTrump));

        Assert.Equal(AuctionAction.Double,
            _engine.DecideCall(Seat.West, hand, auction).Action);
    }

    [Fact]
    public void MinorOpeningOneNoTrumpIntervention_FourFourMajorsUnderNine_BidsTwoClubsCappelletti()
    {
        var hand = Cards(
            "SK", "SQ", "S8", "S5",
            "HQ", "H8", "H6", "H3", "H2",
            "D8", "D6",
            "C8", "C6");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(1, Denomination.NoTrump));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningOneNoTrumpIntervention_FiveCardSupportSevenToEightH_JumpsToBarrage()
    {
        var hand = Cards(
            "CA", "C8", "C6", "C3", "C2",
            "SK", "S6",
            "S3",
            "H8", "H6", "H3",
            "D8", "D6");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(1, Denomination.NoTrump));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningOneNoTrumpIntervention_FiveCardSupportSixH_RaisesSimplyToTwo()
    {
        var hand = Cards(
            "CK", "C8", "C6", "C3", "C2",
            "S8", "S6", "S3",
            "H8", "H6", "H3",
            "DK", "D6");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(1, Denomination.NoTrump));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningOneNoTrumpIntervention_SevenCardWeakSuit_JumpsToBarrage()
    {
        var hand = Cards(
            "HK", "HQ", "H8", "H6", "H4", "H3", "H2",
            "S8", "S4",
            "D9", "D5",
            "C6", "C4");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Diamonds), new Bid(1, Denomination.NoTrump));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningOneNoTrumpIntervention_SixCardSuit_BidsItNonForcing()
    {
        var hand = Cards(
            "CK", "CQ", "C8", "C6", "C3", "C2",
            "S8", "S6", "S3",
            "H6",
            "D8", "D6", "D3");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Diamonds), new Bid(1, Denomination.NoTrump));

        Assert.Equal(new Bid(2, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningOneNoTrumpIntervention_FiveCardSupportThirteenHld_UsesTwoNoTrumpCueBid()
    {
        var hand = Cards(
            "CJ", "C8", "C6", "C4", "C2",
            "HA", "HK", "H3",
            "DK", "DQ", "D3",
            "S8", "S6");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(1, Denomination.NoTrump));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    // --- Réponses après une intervention bicolore (cuebid) sur l'ouverture mineure ---

    [Fact]
    public void MinorOpeningTwoSuitedIntervention_ThreeCardSupportEightToTenHld_RaisesSimply()
    {
        var hand = Cards(
            "CK", "C8", "C3",
            "S8", "S6", "S3",
            "H8", "H6", "H3",
            "DA", "DK", "D6", "D3");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningTwoSuitedIntervention_SixDiamondsEightToTenH_BidsLastSuitNonForcing()
    {
        var hand = Cards(
            "DA", "DK", "DQ", "D6", "D3", "D2",
            "S8", "S6", "S3",
            "H8", "H3",
            "C8", "C6");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.Diamonds),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningTwoSuitedIntervention_BalancedEightHl_BidsNoTrumpNaturally()
    {
        var hand = Cards(
            "SA", "SQ", "S6", "S3",
            "HK", "H6", "H3",
            "D8", "D6", "D3", "D2",
            "C8", "C6");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningTwoSuitedIntervention_SupportWithThirteenHld_CuebidsCheapestSuitForGameForce()
    {
        var hand = Cards(
            "CA", "CK", "CQ", "CJ", "C3",
            "SA", "S6", "S3", "S2",
            "H8", "H6", "H3",
            "D6");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningTwoSuitedIntervention_FiveCardOtherMinorThirteenHld_CuebidsExpensiveSuit()
    {
        var hand = Cards(
            "DA", "DK", "DQ", "DJ", "D3", "D2",
            "SA",
            "H8", "H6", "H4", "H3",
            "C8", "C6");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.Spades),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void MinorOpeningTwoSuitedIntervention_TenHlNoNaturalBid_DoublesForTakeout()
    {
        var hand = Cards(
            "SK", "SQ", "S8", "S6", "S3",
            "HA", "H8", "H6",
            "D8", "D6", "D3", "D2",
            "C8");
        var auction = PartnerMinorWithIntervention(
            new Bid(1, Denomination.Clubs), new Bid(2, Denomination.Clubs));

        Assert.Equal(AuctionAction.Double,
            _engine.DecideCall(Seat.West, hand, auction).Action);
    }

    [Fact]
    public void OneNoTrumpIntervention_DoubleWithTenPoints_RedoublesPunitively()
    {
        var hand = Cards(
            "SA", "SK", "S6", "S3",
            "HA", "H6", "H3",
            "D8", "D6", "D3",
            "C8", "C6", "C4");
        var auction = PartnerOneNoTrumpIntervention(AuctionAction.Double);

        Assert.Equal(AuctionAction.Redouble,
            _engine.DecideCall(Seat.West, hand, auction).Action);
    }

    [Fact]
    public void OneNoTrumpIntervention_TwoNoTrumpWithFiveHearts_UsesRubensohlTransfer()
    {
        var hand = Cards(
            "HA", "HQ", "HJ", "H8", "H7",
            "SK", "S6", "D8", "D6", "D3", "C8", "C6", "C4");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.NoTrump));

        Assert.Equal(new Bid(3, Denomination.Diamonds),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void OneNoTrumpIntervention_TwoClubsWithBothMajors_TransfersToLongerMajor()
    {
        var hand = Cards(
            "SA", "S8", "S6", "S3", "S2",
            "HK", "H8", "H6", "H3",
            "D8", "D6", "C8", "C6");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(3, Denomination.Hearts),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void OneNoTrumpIntervention_LandyWithGameValuesAndNoHeartStop_CuebidsHearts()
    {
        var hand = Cards(
            "SA", "SK", "SQ", "SJ",
            "H9", "H8", "H7",
            "DK", "D6", "D4", "D2",
            "C8", "C6");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.Hearts),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void OneNoTrumpIntervention_LandyWithSixClubs_UsesRubensohlTransfer()
    {
        var hand = Cards(
            "C9", "C8", "C7", "C6", "C4", "C2",
            "D8", "D6", "D4",
            "H8", "H6", "S8", "S6");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.Clubs));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void OneNoTrumpOpener_RectifiesRubensohlTwoNoTrumpToThreeClubs()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S2",
            "HK", "HQ", "H4",
            "DK", "D8", "D3",
            "CQ", "C9", "C5");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.West, new Bid(2, Denomination.NoTrump));
        auction.AddCall(Seat.North, null);

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.East, opener, auction));
    }

    [Fact]
    public void OneNoTrumpIntervention_TwoLevelSuitWithSixCards_UsesRubensohlTransfer()
    {
        var hand = Cards(
            "DA", "DK", "D8", "D6", "D4", "D3",
            "SA", "S6", "H8", "H6", "C8", "C6", "C4");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.Hearts));

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void OneNoTrumpIntervention_TwoHeartsWithSixClubs_UsesRubensohlTransfer()
    {
        var hand = Cards(
            "C9", "C8", "C7", "C6", "C4", "C2",
            "DA", "D6", "D4",
            "H8", "H6", "S8", "S6");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.Hearts));

        Assert.Equal(new Bid(2, Denomination.NoTrump),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void OneNoTrumpIntervention_TwoDiamondsWithFiveHearts_TransfersToHearts()
    {
        var hand = Cards(
            "HA", "HK", "HQ", "H9", "H7",
            "SA", "S6", "D8", "D6", "C8", "C6", "C4", "C2");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.Diamonds));

        Assert.Equal(new Bid(3, Denomination.Diamonds),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    [Fact]
    public void OneNoTrumpOpener_RectifiesNaturalInterventionTransfer()
    {
        var opener = Cards(
            "SA", "SK", "S7", "S2",
            "HK", "HQ", "H4",
            "DK", "D8", "D3",
            "CQ", "C9", "C5");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.Hearts));
        auction.AddCall(Seat.West, new Bid(2, Denomination.NoTrump));
        auction.AddCall(Seat.North, null);

        Assert.Equal(new Bid(3, Denomination.Clubs),
            _engine.DecideBid(Seat.East, opener, auction));
    }

    [Fact]
    public void OneNoTrumpIntervention_ThreeSpadesAsksForHeartStop()
    {
        var responder = Cards(
            "SA", "SK", "SQ", "SJ",
            "H9", "H8", "H7",
            "DA", "DK", "D4", "D2",
            "C8", "C6");
        var opener = Cards(
            "SA", "SK", "S7", "S2",
            "HK", "H8", "H4",
            "DA", "D8", "D3", "D2",
            "C8", "C6");
        var auction = PartnerOneNoTrumpIntervention(new Bid(2, Denomination.Hearts));

        Assert.Equal(new Bid(3, Denomination.Spades),
            _engine.DecideBid(Seat.West, responder, auction));
        auction.AddCall(Seat.West, new Bid(3, Denomination.Spades));
        auction.AddCall(Seat.North, null);

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.East, opener, auction));
    }

    [Fact]
    public void OneNoTrumpIntervention_ThreeSpadesWithSpadeGuard_BidsThreeNoTrump()
    {
        var hand = Cards(
            "SA", "S6", "S3", "S2",
            "HK", "H6", "H3",
            "DQ", "D6", "D3",
            "C8", "C6", "C4");
        var auction = PartnerOneNoTrumpIntervention(new Bid(3, Denomination.Spades));

        Assert.Equal(new Bid(3, Denomination.NoTrump),
            _engine.DecideBid(Seat.West, hand, auction));
    }

    private static Auction AuctionAfterOpponent(Bid opening)
    {
        var auction = new Auction();
        auction.AddCall(Seat.East, opening);
        return auction;
    }

    private static Auction PartnerMinorWithIntervention(Bid opening, Bid intervention)
    {
        var auction = new Auction();
        auction.AddCall(Seat.East, opening);
        auction.AddCall(Seat.South, intervention);
        return auction;
    }

    private static Auction PartnerOneNoTrumpIntervention(Bid intervention)
    {
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.South, intervention);
        auction.AddCall(Seat.North, null);
        return auction;
    }

    private static Auction PartnerOneNoTrumpIntervention(AuctionAction intervention)
    {
        var auction = new Auction();
        auction.AddCall(Seat.East, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.South, intervention);
        auction.AddCall(Seat.North, null);
        return auction;
    }

    private static Auction PartnerMinorWithDouble(Bid opening)
    {
        var auction = new Auction();
        auction.AddCall(Seat.East, opening);
        auction.AddCall(Seat.South, AuctionAction.Double);
        return auction;
    }

    private static Auction AuctionAfterTwoPasses(Bid opening)
    {
        var auction = new Auction();
        auction.AddCall(Seat.East, opening);
        auction.AddCall(Seat.South, null);
        auction.AddCall(Seat.West, null);
        return auction;
    }

    private static List<Card> Cards(params string[] cards) => cards.Select(Parse).ToList();

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
