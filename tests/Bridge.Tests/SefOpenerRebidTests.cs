using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

/// <summary>Chapitre SEF : les redemandes de l'ouvreur.</summary>
public sealed class SefOpenerRebidTests
{
    private readonly SefBiddingEngine _engine = new();

    [Theory]
    [InlineData("candidateEconomic", Denomination.Spades, 2, Denomination.Clubs, Denomination.Diamonds)]
    [InlineData("candidateReverse", Denomination.Diamonds, 1, Denomination.Spades, Denomination.Hearts)]
    [InlineData("responseFitPoints", Denomination.Clubs, 1, Denomination.Hearts, Denomination.Diamonds)]
    [InlineData("agreedFitPoints", Denomination.Spades, 2, Denomination.Spades, Denomination.Diamonds)]
    public void ConfiguredRebid_NewFactsAreEvaluated(string fact, Denomination opening, int responseLevel,
        Denomination response, Denomination candidate)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new()
            {
                Id = "fact-probe", Stage = BiddingStage.OpenerRebid,
                CandidateSuits = [(RuleDenomination)candidate],
                Conditions = [new() { Fact = fact, Minimum = 1 }],
                Response = new() { Level = 7, Forcing = RuleForcing.NonForcing }
            }]
        });
        var auction = AuctionWithBids((Seat.North, new Bid(1, opening)),
            (Seat.South, new Bid(responseLevel, response)));

        Assert.Equal(new Bid(7, Denomination.NoTrump), engine.DecideBid(Seat.North,
            Cards("SA", "SQ", "SJ", "S7", "S5", "HK", "H8", "H4", "DA", "D6", "D3", "C4", "C2"), auction));
    }

    [Theory]
    [InlineData("1S 2S", "SA SK SQ S7 S5 HK H8 H4 DQ D6 D3 CJ C2", 3, Denomination.Diamonds, RuleForcing.OneRound)]
    [InlineData("1S 2S", "SQ SJ S7 S6 S5 HA HK H4 DA DK D3 C4 C2", 2, Denomination.NoTrump, RuleForcing.OneRound)]
    [InlineData("1C 2C", "CA CK CJ C4 DK D8 D5 HA HQ H7 S4 S2 S3", 2, Denomination.Diamonds, RuleForcing.OneRound)]
    [InlineData("1D 2C", "DA DK D7 D4 HQ H5 H2 SK S8 S3 C6 C5 C2", 2, Denomination.NoTrump, RuleForcing.OneRound)]
    [InlineData("1D 2C", "DA DK DQ D4 HA HQ H2 SK S8 S3 CJ C5 C2", 2, Denomination.NoTrump, RuleForcing.OneRound)]
    [InlineData("1C 1D", "CA CK C7 C4 HQ H5 H2 SK S8 S3 D6 D5 D2", 1, Denomination.NoTrump, RuleForcing.NonForcing)]
    [InlineData("1C 1D", "CA CK C7 C4 HQ H5 SA SK S3 S2 D6 D5 D2", 1, Denomination.Spades, RuleForcing.NonForcing)]
    [InlineData("1S 2C", "SA SQ SJ S7 S5 HK H8 H4 DA DQ D3 C4 C2", 2, Denomination.NoTrump, RuleForcing.OneRound)]
    [InlineData("1S 2C", "SA SK SQ S7 S5 HK H8 H4 DA DQ D3 C4 C2", 3, Denomination.NoTrump, RuleForcing.NonForcing)]
    [InlineData("1S 2H", "SA SQ SJ S7 S5 HK H3 H8 H4 DA D6 C4 C2", 4, Denomination.Hearts, RuleForcing.NonForcing)]
    [InlineData("1S 2H", "SA SQ SJ S7 S5 HK H8 H4 DA D6 D3 C4 C2", 2, Denomination.Spades, RuleForcing.OneRound)]
    [InlineData("1D 1S", "DA DK DQ DJ D4 HA HK H8 H2 C6 C5 S3 S2", 2, Denomination.Hearts, RuleForcing.OneRound)]
    [InlineData("1D 1NT", "DA DK DQ DJ D4 HA HK H8 H2 C6 C5 S3 S2", 2, Denomination.Hearts, RuleForcing.GameForcing)]
    [InlineData("1D 2C", "DA DK DQ D7 D4 HA HK H8 H2 C6 C5 S3 S2", 2, Denomination.Hearts, RuleForcing.GameForcing)]
    [InlineData("1S 1NT", "SA SK S7 S6 S5 S2 HQ H8 H2 DK D4 C8 C2", 2, Denomination.Spades, RuleForcing.NonForcing)]
    [InlineData("1S 2C", "SA SK SQ S7 S6 S2 HK H8 H2 DQ D4 CJ C2", 3, Denomination.Spades, RuleForcing.OneRound)]
    [InlineData("1S 2C", "SA SQ SJ S7 S5 HK H8 H4 DQ D6 D3 C4 C2", 2, Denomination.Spades, RuleForcing.OneRound)]
    public void ChapterSeven_ReferenceRebids(string sequence, string cards, int level, Denomination denomination, RuleForcing forcing)
    {
        var engine = DeclarativeEngine();
        var bids = sequence.Split(' ').Select((token, index) => (index % 2 == 0 ? Seat.North : Seat.South, ParseBid(token))).ToArray();
        var auction = AuctionWithBids(bids);

        Assert.Equal(new Bid(level, denomination), engine.DecideBid(Seat.North, TextCards(cards), auction));
        Assert.Equal(forcing, engine.LastForcing);
        Assert.StartsWith("opener.", engine.LastConfiguredRuleId);
    }

    [Fact]
    public void ChapterSeven_DeferredHeartSupportUsesLegalThreeHearts()
    {
        var engine = DeclarativeEngine();
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.Hearts)), (Seat.North, new Bid(2, Denomination.Spades)),
            (Seat.South, new Bid(2, Denomination.NoTrump)));

        Assert.Equal(new Bid(3, Denomination.Hearts), engine.DecideBid(Seat.North,
            Cards("SA", "SQ", "SJ", "S7", "S5", "HK", "H8", "H4", "DQ", "D6", "D3", "C4", "C2"), auction));
    }

    [Theory]
    [InlineData("1C 1H", "CA CK C8 C6 C2 HA HK H8 H4 DK DQ D3 S2", 3, Denomination.Diamonds, "opener.fit-major.super-forcing")]
    [InlineData("1D 1S", "DA DK D8 D6 D2 SA SK S8 S4 CK CQ C3 H2", 3, Denomination.Clubs, "opener.fit-major.super-forcing")]
    [InlineData("1C 1H", "CA CK C8 C6 C2 HA HK H8 H4 SK SQ S3 D2", 2, Denomination.Spades, "opener.fit-major.false-jump")]
    [InlineData("1C 1H", "CA CK CJ C6 HA HK H8 H4 DK D8 D3 SQ S2", 3, Denomination.Diamonds, "opener.fit-major.false-jump.default")]
    [InlineData("1C 1D", "CA CK CQ C8 C6 DA DK D8 D2 SQ S8 S3 H2", 3, Denomination.Hearts, "opener.clubs-diamonds.splinter")]
    [InlineData("1C 1H", "CA CK C8 C6 CJ C2 HA H8 H6 H4 DK D3 S2", 4, Denomination.Clubs, "opener.fit-major.jump-opening-minor")]
    public void ChapterSeven_ArtificialFitPriorities(string sequence, string cards, int level, Denomination denomination, string ruleId)
    {
        var bids = sequence.Split(' ').Select((token, index) => (index % 2 == 0 ? Seat.North : Seat.South, ParseBid(token))).ToArray();
        var engine = DeclarativeEngine();

        Assert.Equal(new Bid(level, denomination), engine.DecideBid(Seat.North, TextCards(cards), AuctionWithBids(bids)));
        Assert.Equal(ruleId, engine.LastConfiguredRuleId);
        Assert.Equal(RuleForcing.GameForcing, engine.LastForcing);
    }

    [Fact]
    public void ChapterSeven_FalseJumpConfirmsFitOnNextTurn()
    {
        var engine = DeclarativeEngine();
        var auction = AuctionWithBids((Seat.North, ParseBid("1C")), (Seat.South, ParseBid("1H")),
            (Seat.North, ParseBid("2S")), (Seat.South, ParseBid("3C")));

        Assert.Equal(ParseBid("4H"), engine.DecideBid(Seat.North,
            TextCards("CA CK C8 C6 C2 HA HK H8 H4 SK S8 S3 D2"), auction));
    }

    [Theory]
    [InlineData("1C 1D 1H P", "CA CK C8 C6 HQ H8 H2 S7 S4 S2 DK D6 D2", 1, Denomination.NoTrump, "opener.forced.one-nt")]
    [InlineData("1S P 2C 2H", "SA SK S7 S6 S5 S2 HQ H8 D8 D4 C8 C5 C2", 2, Denomination.Spades, "opener.free.repeat-two")]
    [InlineData("1S P 2C 2H", "SA SK S7 S6 S5 HQ H8 H2 D8 D4 C8 C5 C2", 0, Denomination.NoTrump, "opener.natural.uncovered.pass")]
    [InlineData("1S P 2C 2H", "SA SK S7 S6 S5 DQ D8 D7 D5 D2 HA CK C2", 3, Denomination.Diamonds, "opener.free.strong-new-color")]
    public void ChapterSeven_CompetitiveContexts(string sequence, string cards, int level, Denomination denomination, string ruleId)
    {
        var auction = AllCalls(sequence);
        var engine = DeclarativeEngine();
        var call = engine.DecideCall(Seat.North, TextCards(cards), auction);

        if (level == 0) Assert.Equal(AuctionAction.Pass, call.Action);
        else Assert.Equal(new Bid(level, denomination), call.Bid);
        Assert.Equal(ruleId, engine.LastConfiguredRuleId);
    }

    [Fact]
    public void ChapterSeven_SputnikIsAnOpenerRebidAndAdjustsSupportThresholds()
    {
        var engine = DeclarativeEngine();
        var auction = AllCalls("1C 1H X P");

        Assert.Equal(ParseBid("3S"), engine.DecideBid(Seat.North,
            TextCards("CA CK C8 C6 H7 H2 DQ D8 D3 SK SQ S4 S2"), auction));
        Assert.Equal("opener.sputnik.fit-jump", engine.LastConfiguredRuleId);
    }

    [Fact]
    public void ChapterSeven_PassedFittedNoTrumpKeepsItsShortnessRelay()
    {
        var auction = new Auction();
        auction.AddCall(Seat.South, null);
        auction.AddCall(Seat.West, null);
        auction.AddCall(Seat.North, ParseBid("1S"));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, ParseBid("2NT"));
        auction.AddCall(Seat.West, null);
        var engine = DeclarativeEngine();

        Assert.Equal(ParseBid("3C"), engine.DecideBid(Seat.North,
            TextCards("SA SK S7 S4 S2 HQ H8 H2 DA D8 D3 C4 C2"), auction));
        Assert.Equal("fitted-nt.passed.shortness-ask", engine.LastConfiguredRuleId);
    }

    [Theory]
    [InlineData("1D 1H 2C P", "DA DK DQ D7 D4 HA HK H8 H2 C6 C5 S3 S2", "2H", RuleForcing.GameForcing)]
    [InlineData("1S X 2C P", "SA SK S7 S6 S2 HA HK H8 H4 DJ D6 C8 C2", "2H", RuleForcing.OneRound)]
    [InlineData("1S X 2C 2D", "SA SK S7 S6 S2 HA HK H8 H4 DJ D6 C8 C2", "2H", RuleForcing.OneRound)]
    public void ChapterSeven_CompetitiveForcingMatchesResponse(string sequence, string cards, string expected, RuleForcing forcing)
    {
        var engine = DeclarativeEngine();
        var auction = AllCalls(sequence);

        Assert.Equal(ParseBid(expected), engine.DecideBid(Seat.North, TextCards(cards), auction));
        Assert.Equal(forcing, engine.LastForcing);
    }

    private static Auction AllCalls(string sequence)
    {
        var auction = new Auction();
        var tokens = sequence.Split(' ');
        for (var index = 0; index < tokens.Length; index++)
        {
            var seat = (Seat)(index % 4);
            if (tokens[index] == "P") auction.AddCall(seat, null);
            else if (tokens[index] == "X") auction.AddCall(seat, AuctionAction.Double);
            else auction.AddCall(seat, ParseBid(tokens[index]));
        }
        return auction;
    }

    private static SefBiddingEngine DeclarativeEngine() => new(profile: BiddingProfile.Load(
        Path.Combine(AppContext.BaseDirectory, "sef-bidding-profile.json")));

    private static List<Card> TextCards(string text) => Cards(text.Split(' '));

    private static Bid ParseBid(string token) => new(token[0] - '0', token[1..] switch
    {
        "C" => Denomination.Clubs, "D" => Denomination.Diamonds, "H" => Denomination.Hearts,
        "S" => Denomination.Spades, _ => Denomination.NoTrump
    });

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
    public void MinorResponse_FiveCardSupportWithFiveH_UsesOneNoTrump()
    {
        var responder = Cards(
            "CQ", "C9", "C7", "C5", "C2",
            "HK", "H8", "H4",
            "S8", "S7", "S3",
            "D6", "D2");
        var auction = AuctionWithBids((Seat.North, new Bid(1, Denomination.Clubs)));

        Assert.Equal(new Bid(1, Denomination.NoTrump),
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
