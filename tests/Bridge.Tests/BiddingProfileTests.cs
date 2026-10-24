using System.Text.Json;
using Bridge.AI.Bidding;
using Bridge.Core.Models;

namespace Bridge.Tests;

public sealed class BiddingProfileTests
{
    [Fact]
    public void DistributedSefProfile_IsValidAndHasNoHiddenFallback()
    {
        var profile = BiddingProfile.Load(Path.Combine(AppContext.BaseDirectory, "sef-bidding-profile.json"));

        Assert.False(profile.UseStandardFallback);
        Assert.NotEmpty(profile.Rules);
        Assert.All(new[] { "2", "3", "4", "5", "6" }, section =>
            Assert.Contains(profile.Rules, rule => rule.SefSection.Split('.')[0] == section));
        Assert.All(profile.Rules, rule => Assert.NotNull(rule.Response.Forcing));
        Assert.Equal(RuleForcing.OneRound, profile.Rules.Single(rule => rule.Id == "opening.strong-clubs.regular").Response.Forcing);
        Assert.Equal(RuleForcing.OneRound, profile.Rules.Single(rule => rule.Id == "strong-clubs.relay").Response.Forcing);
        Assert.Equal(RuleForcing.GameForcing, profile.Rules.Single(rule => rule.Id == "one-nt.stayman.chasse-croise").Response.Forcing);
        Assert.Equal(RuleForcing.NonForcing, profile.Rules.Single(rule => rule.Id == "one-nt.quantitative.game").Response.Forcing);
    }

    [Fact]
    public void DistributedProfile_MatchesEngineDefaults()
    {
        Assert.Equal(new BiddingProfile().ToJson(), BiddingProfile.Load(Path.Combine(AppContext.BaseDirectory, "bidding-profile.json")).ToJson());
    }

    [Fact]
    public void Json_RoundTrip_PreservesProfile()
    {
        var profile = new BiddingProfile
        {
            Id = "personnalise",
            OneNoTrump = new() { Minimum = 12, Maximum = 14, AllowFiveCardMajor = false },
            Conventions = new() { MajorTransfers = false, StaymanMinimumHL = 10 }
        };

        Assert.Equal(profile.ToJson(), BiddingProfile.FromJson(profile.ToJson()).ToJson());
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"oneNoTrump\":{\"minimum\":18,\"maximum\":15}}")]
    [InlineData("{\"twoNoTrump\":{\"minimum\":17}}")]
    [InlineData("{\"responses\":{\"gameMinimumHL\":7}}")]
    [InlineData("{\"conventions\":null}")]
    [InlineData("null")]
    public void Json_InvalidProfile_IsRejected(string json)
    {
        Assert.Throws<ArgumentException>(() => BiddingProfile.FromJson(json));
    }

    [Fact]
    public void Json_UnknownOption_IsRejected()
    {
        Assert.Throws<JsonException>(() => BiddingProfile.FromJson("{\"conventions\":{\"staymann\":false}}"));
    }

    [Fact]
    public void ProgrammaticRule_WithoutForcing_IsRejected()
    {
        var profile = new BiddingProfile
        {
            Rules = [new() { Id = "missing-forcing", Response = new() { Level = 2 } }]
        };

        Assert.Throws<ArgumentException>(profile.Validate);
    }

    [Fact]
    public void BackupSettings_RejectImpossibleFitAndGameThresholds()
    {
        Assert.Throws<ArgumentException>(() => new BiddingProfile
        {
            Backup = new() { MinimumAgreedFit = 5 }
        }.Validate());
        Assert.Throws<ArgumentException>(() => new BiddingProfile
        {
            Backup = new() { GameFitPoints = 34, SlamFitPoints = 33 }
        }.Validate());
    }

    [Fact]
    public void Rule_HldDoesNotIncludeDistributionWithoutFit()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new()
            {
                Id = "hld-requires-fit",
                Stage = BiddingStage.Response,
                SideSequence = ["1C"],
                Conditions = [new() { Fact = "hld", Minimum = 10 }],
                Response = new() { Level = 2, Denomination = RuleDenomination.Diamonds,
                    Forcing = RuleForcing.NonForcing }
            }]
        });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Clubs));
        auction.AddCall(Seat.East, AuctionAction.Pass);

        engine.DecideCall(Seat.South, Cards("HA H8 H7 H4 H2 DK D8 D6 D4 S8 S6 S4 C2"), auction);

        Assert.Null(engine.LastConfiguredRuleId);
    }

    [Fact]
    public void Rule_RepeatedSuitWithFewerThanEightCardsDoesNotCountDistribution()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new()
            {
                Id = "underfit-hld",
                Stage = BiddingStage.OpenerRebid,
                SideSequence = ["1S", "2S"],
                Conditions = [new() { Fact = "hld", Minimum = 10 }],
                Response = new() { Level = 4, Denomination = RuleDenomination.Opening,
                    Forcing = RuleForcing.NonForcing }
            }]
        });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, AuctionAction.Pass);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Spades));
        auction.AddCall(Seat.West, AuctionAction.Pass);

        engine.DecideCall(Seat.North, Cards("S2 H2 H3 H4 DA D2 D3 D4 C2 C3 C4 C5 C6"), auction);

        Assert.Null(engine.LastConfiguredRuleId);
    }

    [Fact]
    public void Rule_PlayingTricksHalf_AddsOneHalfForThreeAces()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new()
            {
                Id = "half-trick-aces",
                Stage = BiddingStage.OpenerRebid,
                SideSequence = ["2C", "2D"],
                Conditions = [new() { Fact = "playingTricksHalf", Minimum = 17, Maximum = 17 }],
                Response = new() { Level = 3, Denomination = RuleDenomination.Hearts,
                    Forcing = RuleForcing.OneRound }
            }]
        });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.East, AuctionAction.Pass);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Diamonds));
        auction.AddCall(Seat.West, AuctionAction.Pass);

        Assert.Equal(new Bid(3, Denomination.Hearts), engine.DecideBid(Seat.North,
            Cards("SA SK S2 S3 HA HK H2 DA DK D2 CQ CJ C2"), auction));
        Assert.Equal("half-trick-aces", engine.LastConfiguredRuleId);
    }

    [Fact]
    public void Rule_PlayingTricksHalf_AddsOneHalfForThreeTens()
    {
        AssertHalfTrickRuleMatches("SK S10 S9 S8 HA H10 H8 DK D10 D8 CA C9 C8");
    }

    [Fact]
    public void Rule_PlayingTricksHalf_AddsOneHalfForTwoTensAndThreeNines()
    {
        AssertHalfTrickRuleMatches("SK S10 S9 S8 HA H10 H9 DK D9 D8 CA C9 C8");
    }

    [Fact]
    public void Backup_FitBelowGameKeepsLowestOwnedPartScoreAndLogsAssessment()
    {
        var profile = BackupProfile();
        var engine = new SefBiddingEngine(profile: profile);
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Spades));
        auction.AddCall(Seat.West, null);
        var hand = Cards("S2 S3 S4 S5 S6 H2 H3 H4 H5 D2 D3 D4 C2");

        Assert.Equal(new AuctionCall(Seat.North, AuctionAction.Pass), engine.DecideCall(Seat.North, hand, auction));
        Assert.Contains("Secours unique", engine.LastDecisionRule);
        Assert.Contains("fit agréé 8 cartes à Piques", engine.LastDecisionRule);
        Assert.Contains("force insuffisante", engine.LastDecisionRule);
    }

    [Fact]
    public void Backup_FitAndCombinedStrengthReachMajorGame()
    {
        var engine = new SefBiddingEngine(profile: BackupProfile());
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Spades));
        auction.AddCall(Seat.West, null);
        var hand = Cards("SA SK SQ SJ S10 HA H2 D2 D3 D4 C2 C3 C4");

        Assert.Equal(new Bid(4, Denomination.Spades), engine.DecideBid(Seat.North, hand, auction));
        Assert.Contains("force combinée suffisante pour la manche", engine.LastDecisionRule);
    }

    [Fact]
    public void Backup_AgreedFitAndSlamStrengthStartsBlackwood()
    {
        var engine = new SefBiddingEngine(profile: BackupProfile());
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Spades));
        auction.AddCall(Seat.West, null);
        var hand = Cards("SA SK SQ SJ S10 HA HK HQ H2 DA D2 C2 C3");

        Assert.Equal(new Bid(4, Denomination.NoTrump), engine.DecideBid(Seat.North, hand, auction));
        Assert.Equal(RuleForcing.OneRound, engine.LastForcing);
        Assert.Contains("exploration Blackwood", engine.LastDecisionRule);
    }

    [Fact]
    public void Backup_NoFitNeedsSafeNoTrumpForGame()
    {
        var engine = new SefBiddingEngine(profile: BackupProfile());
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);
        var hand = Cards("SA S7 S2 HK H8 H2 DA D8 D2 CQ C8 C5 C2");

        Assert.Equal(new Bid(3, Denomination.NoTrump), engine.DecideBid(Seat.South, hand, auction));
        Assert.Contains("force combinée suffisante pour la manche", engine.LastDecisionRule);
    }

    [Fact]
    public void Backup_NoFitWithoutAllStopsKeepsCurrentPartScore()
    {
        var engine = new SefBiddingEngine(profile: BackupProfile());
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);
        var hand = Cards("S2 S3 S4 H2 H3 H4 D2 D3 D4 C2 C3 C4 C5");

        Assert.Equal(new AuctionCall(Seat.South, AuctionAction.Pass), engine.DecideCall(Seat.South, hand, auction));
        Assert.Contains("conservation du partiel", engine.LastDecisionRule);
    }

    [Theory]
    [InlineData("{\"rules\":[{\"id\":\"test\",\"conditions\":[{\"fact\":\"unknown\",\"minimum\":5}],\"response\":{\"forcing\":\"NonForcing\"}}]}")]
    [InlineData("{\"rules\":[{\"id\":\"test\",\"sideSequence\":[\"8NT\"],\"response\":{\"forcing\":\"NonForcing\"}}]}")]
    [InlineData("{\"rules\":[{\"id\":\"test\",\"conditions\":[{\"fact\":\"h\",\"minimum\":15,\"maximum\":12}],\"response\":{\"forcing\":\"NonForcing\"}}]}")]
    [InlineData("{\"rules\":[{\"id\":\"missing-forcing\",\"response\":{\"level\":2,\"denomination\":\"Clubs\"}}]}")]
    [InlineData("{\"rules\":[{\"id\":\"same\",\"response\":{\"forcing\":\"NonForcing\"}},{\"id\":\"same\",\"response\":{\"forcing\":\"NonForcing\"}}]}")]
    public void Json_InvalidRule_IsRejected(string json)
    {
        Assert.Throws<ArgumentException>(() => BiddingProfile.FromJson(json));
    }

    [Fact]
    public void Rule_ChangesOpeningThresholdAndResponse()
    {
        var hand = Deal.Create(new Random(42))[Seat.North];
        var points = new HandEvaluation(hand).HighCardPoints;
        var engine = new SefBiddingEngine(profile: BiddingProfile.FromJson($$$"""
            {"rules":[{"id":"custom-opening","stage":"Opening","conditions":[{"fact":"h","minimum":{{{points}}},"maximum":{{{points}}}}],
            "response":{"level":1,"denomination":"Diamonds","forcing":"NonForcing"}}]}
            """));

        Assert.Equal(new Bid(1, Denomination.Diamonds), engine.DecideBid(Seat.North, hand, new Auction()));
        Assert.Equal("custom-opening", engine.LastConfiguredRuleId);
    }

    [Fact]
    public void Rule_ChangesResponseToMajorAndForcingMeaning()
    {
        var engine = new SefBiddingEngine(profile: BiddingProfile.FromJson("""
            {"rules":[{"id":"major-support","stage":"Response","sideSequence":["1M"],"opponentsHaveActed":false,
            "response":{"denomination":"Opening","levelMode":"Jump","forcing":"OneRound"}}]}
            """));
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, null);

        Assert.Equal(new Bid(3, Denomination.Spades), engine.DecideBid(Seat.South, Deal.Create(new Random(42))[Seat.South], auction));
        Assert.Equal(RuleForcing.OneRound, engine.LastForcing);
    }

    [Fact]
    public void Rule_SkipsIllegalResponseAndUsesNextPriority()
    {
        var engine = new SefBiddingEngine(profile: BiddingProfile.FromJson("""
            {"useStandardFallback":false,"rules":[
              {"id":"illegal","priority":20,"response":{"level":1,"denomination":"Clubs","forcing":"NonForcing"}},
              {"id":"legal","priority":10,"response":{"level":2,"denomination":"Hearts","forcing":"NonForcing"}},
              {"id":"lower","response":{"level":3,"denomination":"Hearts","forcing":"NonForcing"}}]}
            """));
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));

        Assert.Equal(new Bid(2, Denomination.Hearts), engine.DecideBid(Seat.East, Deal.Create(new Random(42))[Seat.East], auction));
        Assert.Equal("legal", engine.LastConfiguredRuleId);
    }

    [Theory]
    [InlineData(true, RuleCallAction.Double, AuctionAction.Double)]
    [InlineData(false, RuleCallAction.Double, AuctionAction.Pass)]
    public void Rule_ConventionControlsAllItsRules(bool enabled, RuleCallAction action, AuctionAction expected)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            CustomConventions = new Dictionary<string, bool> { ["defense"] = enabled },
            Rules = [new() { Id = "defense-double", Convention = "defense", Response = new() { Action = action, Forcing = RuleForcing.NonForcing } }]
        });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));

        Assert.Equal(expected, engine.DecideCall(Seat.East, Deal.Create(new Random(42))[Seat.East], auction).Action);
    }

    [Fact]
    public void Rule_RedoubleIsLegalAndExplained()
    {
        var engine = new SefBiddingEngine(profile: BiddingProfile.FromJson("""
            {"rules":[{"id":"redouble","description":"Surcontre positif","opponentSequence":["X"],
            "response":{"action":"Redouble","forcing":"NonForcing"}}]}
            """));
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, AuctionAction.Double);
        var hand = Deal.Create(new Random(42))[Seat.South];

        var call = engine.DecideCall(Seat.South, hand, auction);

        Assert.Equal(AuctionAction.Redouble, call.Action);
        Assert.Equal("redouble", engine.ExplainCall(Seat.South, hand, auction.Calls, call).RuleId);
    }

    [Theory]
    [InlineData(AuctionForcing.OneRound, Seat.South)]
    [InlineData(AuctionForcing.SelfForcing, Seat.North)]
    [InlineData(AuctionForcing.GameForcing, Seat.South)]
    public void Auction_RejectsPassWhileForcingIsOutstanding(AuctionForcing forcing, Seat forcedSeat)
    {
        var auction = new Auction();
        auction.AddCall(new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(2, Denomination.Clubs), forcing));

        Assert.Equal(forcing, auction.ForcingFor(forcedSeat));
        Assert.Throws<ArgumentException>(() => auction.AddCall(forcedSeat, null));
    }

    [Theory]
    [InlineData(AuctionAction.Pass)]
    [InlineData(AuctionAction.Double)]
    [InlineData(AuctionAction.Redouble)]
    public void Auction_RejectsNonBidCallsWhileOneRoundForcing(AuctionAction action)
    {
        var auction = new Auction();
        auction.AddCall(new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.Spades), AuctionForcing.OneRound));
        if (action == AuctionAction.Double)
            auction.AddCall(new AuctionCall(Seat.East, AuctionAction.Bid, new Bid(2, Denomination.Clubs)));
        else if (action == AuctionAction.Redouble)
            auction.AddCall(Seat.East, AuctionAction.Double);
        else
            auction.AddCall(Seat.East, null);

        Assert.Throws<ArgumentException>(() => auction.AddCall(Seat.South, action));
    }

    [Fact]
    public void Auction_GameForceIsSatisfiedOncePartnershipReachesGame()
    {
        var auction = new Auction();
        auction.AddCall(new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(2, Denomination.Diamonds), AuctionForcing.GameForcing));
        auction.AddCall(Seat.East, null);
        auction.AddCall(new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(3, Denomination.NoTrump)));

        Assert.Equal(AuctionForcing.None, auction.ForcingFor(Seat.West));
        auction.AddCall(Seat.West, null);
        auction.AddCall(Seat.North, null);
    }

    [Fact]
    public void Auction_GameForceAllowsIntermediateBidsButRejectsPass()
    {
        var auction = new Auction();
        auction.AddCall(new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(2, Denomination.Diamonds), AuctionForcing.GameForcing));

        auction.AddCall(Seat.South, new Bid(2, Denomination.Hearts));
        Assert.Throws<ArgumentException>(() => auction.AddCall(Seat.North, null));
    }

    [Theory]
    [InlineData(RuleForcing.OneRound, AuctionForcing.OneRound, Seat.South, Seat.North)]
    [InlineData(RuleForcing.SelfForcing, AuctionForcing.SelfForcing, Seat.North, Seat.South)]
    [InlineData(RuleForcing.GameForcing, AuctionForcing.GameForcing, Seat.South, Seat.North)]
    public void ConfiguredForcingIsStoredAndEngineNeverPasses(RuleForcing ruleForcing,
        AuctionForcing storedForcing, Seat firstBidder, Seat nextSeat)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new()
            {
                Id = "forcing-opening", Stage = BiddingStage.Opening,
                Response = new() { Level = 2, Denomination = RuleDenomination.Clubs, Forcing = ruleForcing }
            }]
        });
        var hand = Deal.Create(new Random(42))[firstBidder];
        var auction = new Auction();
        var forcingCall = engine.DecideCall(firstBidder, hand, auction);

        Assert.Equal(storedForcing, forcingCall.Forcing);
        auction.AddCall(forcingCall);
        if (ruleForcing == RuleForcing.SelfForcing)
            auction.AddCall(nextSeat, null);

        var obligatedSeat = ruleForcing == RuleForcing.SelfForcing ? firstBidder : nextSeat;
        var continuation = engine.DecideCall(obligatedSeat, Deal.Create(new Random(43))[obligatedSeat], auction);

        Assert.Equal(AuctionAction.Bid, continuation.Action);
        Assert.Equal(ruleForcing, engine.LastForcing);
        if (ruleForcing != RuleForcing.NonForcing)
            Assert.Contains("Continuation de sécurité", engine.LastDecisionRule);
        if (ruleForcing == RuleForcing.GameForcing)
            Assert.True(continuation.Bid is { } bid && (bid.Denomination == Denomination.NoTrump
                ? bid.Level >= 3
                : bid.Level >= (bid.Denomination is Denomination.Hearts or Denomination.Spades ? 4 : 5)));
    }

    [Fact]
    public void HumanCallCanBeAnnotatedAndPassIsRejectedOnNextTurn()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new() { Id = "human-forcing", Stage = BiddingStage.Opening,
                Response = new() { Level = 2, Denomination = RuleDenomination.Clubs, Forcing = RuleForcing.OneRound } }]
        });
        var auction = new Auction();
        var call = engine.AnnotateCall(Seat.North, Deal.Create(new Random(42))[Seat.North], auction.Calls,
            new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(2, Denomination.Clubs)));

        Assert.Equal(AuctionForcing.OneRound, call.Forcing);
        auction.AddCall(call);
        Assert.Throws<ArgumentException>(() => auction.AddCall(Seat.South, null));
    }

    [Fact]
    public void EngineAllowsIntermediateConfiguredResponseDuringGameForce()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new() { Id = "game-force-answer", Stage = BiddingStage.Response,
                Response = new() { Level = 2, Denomination = RuleDenomination.Hearts, Forcing = RuleForcing.NonForcing } }]
        });
        var auction = new Auction();
        auction.AddCall(new AuctionCall(Seat.North, AuctionAction.Bid,
            new Bid(2, Denomination.Diamonds), AuctionForcing.GameForcing));

        var call = engine.DecideCall(Seat.South, Deal.Create(new Random(47))[Seat.South], auction);

        Assert.Equal(AuctionAction.Bid, call.Action);
        Assert.Equal(new Bid(2, Denomination.Hearts), call.Bid);
    }

    [Fact]
    public void ForcingPassTransfersOneRoundObligationToPartner()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new() { Id = "forcing-pass", Stage = BiddingStage.Intervention,
                Response = new() { Action = RuleCallAction.Pass, Forcing = RuleForcing.OneRound } }]
        });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));

        var forcingPass = engine.DecideCall(Seat.East, Deal.Create(new Random(44))[Seat.East], auction);
        Assert.Equal(AuctionForcing.OneRound, forcingPass.Forcing);
        auction.AddCall(forcingPass);
        Assert.Throws<ArgumentException>(() => auction.AddCall(Seat.West, null));
    }

    [Theory]
    [InlineData("4", Denomination.Clubs)]
    [InlineData("5", Denomination.NoTrump)]
    public void SefProfile_StaymanThresholdIsEditable(string threshold, Denomination expected)
    {
        var profile = SefProfile();
        var rules = profile.Rules.Select(rule => rule.Id == "two-nt.stayman.ask"
            ? rule with { Conditions = [new() { Fact = "hl", Minimum = int.Parse(threshold) }, new() { Fact = "majorFours", Minimum = 1 }] }
            : rule).ToArray();
        var engine = new SefBiddingEngine(profile: profile with { Rules = rules });
        var hand = Cards("SA S7 S4 S2 H8 H5 H2 D8 D6 D3 C6 C5 C2");
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(2, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);

        Assert.Equal(new Bid(3, expected), engine.DecideBid(Seat.South, hand, auction));
    }

    [Theory]
    [InlineData(true, Denomination.Diamonds)]
    [InlineData(false, Denomination.Hearts)]
    public void SefProfile_DisabledTexasUsesConfiguredNaturalAlternative(bool enabled, Denomination expected)
    {
        var profile = SefProfile();
        var conventions = new Dictionary<string, bool>(profile.CustomConventions) { ["majorTransfers"] = enabled };
        var engine = new SefBiddingEngine(profile: profile with { CustomConventions = conventions });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);

        Assert.Equal(new Bid(2, expected), engine.DecideBid(Seat.South,
            Cards("S7 S4 S2 HQ H8 H7 H5 H2 D8 D3 C6 C5 C2"), auction));
    }

    [Fact]
    public void SefProfile_DisabledStaymanDoesNotRecognizeItsResponse()
    {
        var profile = SefProfile();
        var engine = new SefBiddingEngine(profile: profile with
        {
            CustomConventions = new Dictionary<string, bool>(profile.CustomConventions) { ["stayman"] = false }
        });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, new Bid(2, Denomination.Clubs));
        auction.AddCall(Seat.West, null);

        engine.DecideCall(Seat.North, Cards("SA SK S7 S4 HQ H6 H3 DA D8 D3 CQ C9 C2"), auction);

        Assert.Null(engine.LastConfiguredRuleId);
        Assert.StartsWith("Secours unique", engine.LastDecisionRule);
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(0, 0)]
    public void SefProfile_EvaluationWeightsAreUsed(int aceValue, int expectedLevel)
    {
        var profile = SefProfile();
        var engine = new SefBiddingEngine(profile: profile with { Evaluation = profile.Evaluation with { Ace = aceValue } });
        var bid = engine.DecideBid(Seat.North, Cards("SA S7 S4 HA H5 H2 DA D8 D3 CQ C6 C5 C2"), new Auction());

        Assert.Equal(expectedLevel, bid?.Level ?? 0);
    }

    [Fact]
    public void SefProfile_ResponseCanBeChangedWithoutChangingCode()
    {
        var profile = SefProfile();
        var rules = profile.Rules.Select(rule => rule.Id == "one-nt.quantitative.game"
            ? rule with { Response = rule.Response with { Level = 4 } } : rule).ToArray();
        var engine = new SefBiddingEngine(profile: profile with { Rules = rules });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);

        Assert.Equal(new Bid(4, Denomination.NoTrump), engine.DecideBid(Seat.South,
            Cards("SA S7 S4 HK H5 H2 DK D8 D3 CQ C6 C5 C2"), auction));
        Assert.Equal("one-nt.quantitative.game", engine.LastConfiguredRuleId);
    }

    [Theory]
    [InlineData(BiddingStage.Opening)]
    [InlineData(BiddingStage.Intervention)]
    [InlineData(BiddingStage.Response)]
    [InlineData(BiddingStage.OpenerRebid)]
    [InlineData(BiddingStage.ResponderRebid)]
    [InlineData(BiddingStage.Continuation)]
    public void Rule_AllAuctionStagesAreConfigurable(BiddingStage stage)
    {
        var auction = new Auction();
        var nextSeat = Seat.North;
        var calls = stage switch
        {
            BiddingStage.Opening => 0,
            BiddingStage.Intervention => 1,
            BiddingStage.Response => 2,
            BiddingStage.OpenerRebid => 4,
            BiddingStage.ResponderRebid => 6,
            _ => 8
        };
        for (var index = 0; index < calls; index++)
        {
            nextSeat = (Seat)(index % 4);
            auction.AddCall(nextSeat, index % 2 == 0 ? new Bid(1 + index / 8, (Denomination)(index / 2 % 4)) : null);
        }
        nextSeat = (Seat)(calls % 4);
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new() { Id = "stage", Stage = stage, Response = new() { Level = 7, Forcing = RuleForcing.NonForcing } }]
        });

        Assert.Equal(new Bid(7, Denomination.NoTrump), engine.DecideBid(nextSeat, Deal.Create(new Random(42))[nextSeat], auction));
    }

    [Fact]
    public void SefProfile_EveryDeclaredConventionHasRules()
    {
        var profile = SefProfile();
        foreach (var convention in profile.CustomConventions.Keys)
            Assert.Contains(profile.Rules, rule => rule.Convention == convention || rule.RequiresConventions.Contains(convention));
    }

    [Fact]
    public void SefProfile_EveryConventionCanDisableItsRules()
    {
        var profile = SefProfile();
        foreach (var convention in profile.CustomConventions.Keys)
        {
            var engine = new SefBiddingEngine(profile: profile with
            {
                CustomConventions = new Dictionary<string, bool>(profile.CustomConventions) { [convention] = false },
                Rules = [new() { Id = "disabled", Convention = convention, Response = new() { Level = 7, Forcing = RuleForcing.NonForcing } }]
            });

            Assert.Null(engine.DecideBid(Seat.North, Deal.Create(new Random(42))[Seat.North], new Auction()));
            Assert.Null(engine.LastConfiguredRuleId);
        }
    }

    [Theory]
    [InlineData("CA CK C8 C2 DQ D7 D3 HK H5 H2 S8 S5 S3", 1, Denomination.Clubs)]
    [InlineData("SA SK S7 S4 S2 HQ H6 H3 DK D8 D3 C9 C2", 1, Denomination.Spades)]
    [InlineData("SA SK S7 S4 HQ H6 H3 DA D8 D3 CQ C9 C2", 1, Denomination.NoTrump)]
    [InlineData("SK SQ S7 S6 S5 S2 H8 H4 H2 D8 D4 C8 C2", 2, Denomination.Spades)]
    [InlineData("CK CQ CJ C8 C7 C6 C2 H8 H4 H2 D8 D4 S2", 3, Denomination.Clubs)]
    [InlineData("CA CK CQ C8 C7 C6 C2 H8 H4 H2 D8 D4 S2", 3, Denomination.NoTrump)]
    [InlineData("SA SK SQ S2 HA HQ H2 DK DQ D2 C8 C5 C2", 2, Denomination.NoTrump)]
    [InlineData("SA SK SQ S2 HA HK H2 DA DK D2 C8 C5 C2", 2, Denomination.Clubs)]
    [InlineData("SA SK SQ S2 HA HK H2 DA DK D2 CQ C5 C2", 2, Denomination.Diamonds)]
    public void SefProfile_ReferenceOpeningsMatchAllChapters(string cards, int level, Denomination denomination)
    {
        var engine = new SefBiddingEngine(profile: SefProfile());

        Assert.Equal(new Bid(level, denomination), engine.DecideBid(Seat.North, Cards(cards), new Auction()));
    }

    [Fact]
    public void RulesAndConventionsAreSnapshottedAtConstruction()
    {
        var conventions = new Dictionary<string, bool> { ["custom"] = true };
        var rules = new List<BiddingRule>
        {
            new() { Id = "immutable", Convention = "custom", Response = new() { Level = 7, Forcing = RuleForcing.NonForcing } }
        };
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            Rules = rules, CustomConventions = conventions, UseStandardFallback = false
        });
        conventions["custom"] = false;
        rules.Clear();

        Assert.Equal(new Bid(7, Denomination.NoTrump), engine.DecideBid(Seat.North, Deal.Create(new Random(42))[Seat.North], new Auction()));
    }

    [Fact]
    public void SefProfile_AuctionsOnlyProduceLegalCalls()
    {
        var profile = SefProfile();
        for (var seed = 0; seed < 24; seed++)
        {
            var deal = Deal.Create(new Random(seed));
            var engine = new SefBiddingEngine(profile: profile);
            var auction = new Auction();
            for (var turn = 0; turn < 320 && !auction.IsComplete; turn++)
            {
                var seat = (Seat)(turn % 4);
                auction.AddCall(engine.DecideCall(seat, deal[seat], auction));
            }
            Assert.True(auction.IsComplete);
        }
    }

    [Theory]
    [InlineData(RuleCallAction.Bid, false)]
    [InlineData(RuleCallAction.Double, true)]
    public void Rule_LastOpponentActionDoesNotConfuseBidAndDouble(RuleCallAction action, bool shouldMatch)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new() { Id = "context", LastOpponentAction = action, Response = new() { Action = RuleCallAction.Redouble, Forcing = RuleForcing.NonForcing } }]
        });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, AuctionAction.Double);

        Assert.Equal(shouldMatch ? AuctionAction.Redouble : AuctionAction.Pass,
            engine.DecideCall(Seat.South, Cards("SA S7 S4 HK H5 H2 DK D8 D3 CQ C6 C5 C2"), auction).Action);
    }

    [Theory]
    [InlineData(Vulnerability.EastWest, true)]
    [InlineData(Vulnerability.NorthSouth, false)]
    public void Rule_FavorableVulnerabilityIsConfigurable(Vulnerability vulnerability, bool expected)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new()
            {
                Id = "favorable", Conditions = [new() { Fact = "favorableVulnerability", Minimum = 1 }],
                Response = new() { Level = 1, Denomination = RuleDenomination.Hearts, Forcing = RuleForcing.NonForcing }
            }]
        }) { Vulnerability = vulnerability };

        Assert.Equal(expected, engine.DecideBid(Seat.North, Deal.Create(new Random(42))[Seat.North], new Auction()).HasValue);
    }

    [Theory]
    [InlineData("1C P", "SA S8 S2 HK H8 H7 H2 DQ D8 D2 C9 C5 C2", 1, Denomination.Hearts)]
    [InlineData("1S P", "SA S8 S2 HK H8 H2 DQ DJ D8 D2 CQ C5 C2", 2, Denomination.NoTrump)]
    [InlineData("1S P", "SA S8 S7 S2 HK H8 H2 DQ DJ D8 CJ C5 C2", 3, Denomination.NoTrump)]
    [InlineData("1NT P 2C P", "SA SK S7 S4 HK H8 H7 H2 DQ D8 D2 CQ C2", 2, Denomination.NoTrump)]
    [InlineData("1NT P 2H P", "SA SK S2 HK H8 H2 DQ D8 D2 CQ C8 C5 C2", 2, Denomination.Spades)]
    [InlineData("1NT P 2S P", "SA SK S2 HK H8 H2 DQ D8 D2 CQ C8 C5 C2", 3, Denomination.Clubs)]
    [InlineData("2D P", "SK S8 S2 HQ H8 H2 DK D8 D2 CJ C8 C5 C2", 2, Denomination.NoTrump)]
    [InlineData("2D P", "S8 S5 S2 H8 H5 H2 D8 D5 D2 C8 C7 C5 C2", 2, Denomination.Hearts)]
    [InlineData("2D P", "SA S8 S2 H8 H5 H2 D8 D5 D2 C8 C7 C5 C2", 2, Denomination.Spades)]
    [InlineData("2D P 2H P 3C P", "S8 S5 S2 H8 H5 H2 D8 D5 D2 C8 C7 C5 C2", 5, Denomination.Clubs)]
    [InlineData("2D P 2H P 2NT P", "S8 S5 S2 H8 H5 H2 D8 D5 D2 C8 C7 C5 C2", 3, Denomination.NoTrump)]
    [InlineData("1NT 2D", "S8 S7 S5 S4 S2 HQ H5 H2 D8 D3 C6 C5 C2", 3, Denomination.Hearts)]
    [InlineData("1C P 1S P 1NT P 2C P", "SA S7 S2 HA HQ H8 H2 DK D8 D2 CQ C5 C2", 2, Denomination.Hearts)]
    public void SefProfile_ReferenceSequencesUseConfiguredRules(string sequence, string cards, int level, Denomination denomination)
    {
        var auction = new Auction();
        var calls = sequence.Split(' ');
        for (var index = 0; index < calls.Length; index++)
        {
            var token = calls[index];
            if (token == "P") auction.AddCall((Seat)(index % 4), null);
            else
            {
                var suit = token[1..] switch
                {
                    "C" => Denomination.Clubs, "D" => Denomination.Diamonds,
                    "H" => Denomination.Hearts, "S" => Denomination.Spades, _ => Denomination.NoTrump
                };
                auction.AddCall((Seat)(index % 4), new Bid(token[0] - '0', suit));
            }
        }
        var engine = new SefBiddingEngine(profile: SefProfile());

        Assert.Equal(new Bid(level, denomination), engine.DecideBid((Seat)(calls.Length % 4), Cards(cards), auction));
        Assert.NotNull(engine.LastConfiguredRuleId);
    }

    [Fact]
    public void SefProfile_DruryUsesThePassedHandContext()
    {
        var auction = new Auction();
        auction.AddCall(Seat.South, null);
        auction.AddCall(Seat.West, null);
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, null);
        var engine = new SefBiddingEngine(profile: SefProfile());

        Assert.Equal(new Bid(2, Denomination.Clubs), engine.DecideBid(Seat.South,
            Cards("SA S8 S2 HK H8 H2 DQ DJ D8 D2 CQ C5 C2"), auction));
        Assert.Equal("response.major.drury", engine.LastConfiguredRuleId);
    }

    [Fact]
    public void SefProfile_TruscottUsesTheDoubleContext()
    {
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Spades));
        auction.AddCall(Seat.East, AuctionAction.Double);
        var engine = new SefBiddingEngine(profile: SefProfile());

        Assert.Equal(new Bid(2, Denomination.NoTrump), engine.DecideBid(Seat.South,
            Cards("SA S8 S7 S2 HK H8 H2 DQ DJ D8 C9 C5 C2"), auction));
        Assert.Equal("major.doubled.truscott", engine.LastConfiguredRuleId);
    }

    [Fact]
    public void Rule_DoubleJump_IsTwoLevelsAboveCheapestBid()
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new()
            {
                Id = "double-jump", Stage = BiddingStage.OpenerRebid,
                Response = new() { Denomination = RuleDenomination.Clubs, LevelMode = RuleBidLevel.DoubleJump, Forcing = RuleForcing.NonForcing }
            }]
        });
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.Clubs));
        auction.AddCall(Seat.East, null);
        auction.AddCall(Seat.South, new Bid(1, Denomination.Hearts));
        auction.AddCall(Seat.West, null);

        Assert.Equal(new Bid(4, Denomination.Clubs), engine.DecideBid(Seat.North,
            Cards("SA S7 S4 HK H5 H2 DK D8 D3 CQ C6 C5 C2"), auction));
    }

    [Fact]
    public void Explanation_DescribesHoveredCallEvenWhenAnotherRuleWins()
    {
        var engine = new SefBiddingEngine(profile: BiddingProfile.FromJson("""
            {"rules":[
              {"id":"first","priority":20,"response":{"level":1,"denomination":"Clubs","forcing":"NonForcing"}},
              {"id":"other","priority":10,"conditions":[{"fact":"h","minimum":37}],"response":{"level":1,"denomination":"Spades","forcing":"OneRound"}}]}
            """));
        var call = new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.Spades));
        var explanation = engine.ExplainCall(Seat.North, Deal.Create(new Random(42))[Seat.North], [], call);

        Assert.Equal("other", explanation.RuleId);
        Assert.DoesNotContain("other", explanation.Values["descriptionRegle"]);
        Assert.Contains("Points H", explanation.Values["conditionsRegle"]);
        Assert.Contains("37", explanation.Values["conditionsRegle"]);
        Assert.Equal("Forcing un tour", explanation.Values["forcing"]);
    }

    [Theory]
    [InlineData(AuctionAction.Pass)]
    [InlineData(AuctionAction.Double)]
    [InlineData(AuctionAction.Redouble)]
    public void Tooltip_ActionsHaveReadableLabels(AuctionAction action)
    {
        var key = action switch
        {
            AuctionAction.Pass => "pass", AuctionAction.Double => "double", _ => "redouble"
        };

        var text = Bridge.Desktop.AuctionTooltipRules.Get(key);

        Assert.NotEmpty(text);
        Assert.DoesNotContain("non prévue", text);
    }

    [Fact]
    public void Tooltip_DeclarativeRuleDoesNotExposeTechnicalIdentifiers()
    {
        var engine = new SefBiddingEngine(profile: SefProfile());
        var auction = new Auction();
        auction.AddCall(Seat.North, new Bid(1, Denomination.NoTrump));
        auction.AddCall(Seat.East, null);
        var call = new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(2, Denomination.Clubs));
        var explanation = engine.ExplainCall(Seat.South, Cards("SK S7 S4 S2 HQ H5 H2 DK D8 D3 C6 C5 C2"), auction.Calls, call);

        var text = Bridge.Desktop.AuctionTooltipRules.Get(explanation.RuleId, explanation.Values);

        Assert.Contains("Stayman", text);
        Assert.Contains("Points HL", text);
        Assert.DoesNotContain(explanation.RuleId, text);
        Assert.DoesNotContain("OneRound", text);
        Assert.DoesNotContain("majorFours", text);
    }

    private static BiddingProfile SefProfile() => BiddingProfile.Load(Path.Combine(AppContext.BaseDirectory, "sef-bidding-profile.json"));

    private static BiddingProfile BackupProfile() => SefProfile() with { Rules = [] };

    private static void AssertHalfTrickRuleMatches(string cards)
    {
        var engine = new SefBiddingEngine(profile: new BiddingProfile
        {
            UseStandardFallback = false,
            Rules = [new()
            {
                Id = "half-trick-intermediates",
                Stage = BiddingStage.Opening,
                Conditions = [new() { Fact = "playingTricksHalf", Minimum = 11, Maximum = 11 }],
                Response = new() { Level = 1, Denomination = RuleDenomination.Clubs,
                    Forcing = RuleForcing.NonForcing }
            }]
        });

        Assert.Equal(new Bid(1, Denomination.Clubs), engine.DecideBid(Seat.North, Cards(cards), new Auction()));
        Assert.Equal("half-trick-intermediates", engine.LastConfiguredRuleId);
    }

    private static Card[] Cards(string text) => text.Split(' ').Select(value => new Card(value[0] switch
    {
        'C' => Suit.Clubs, 'D' => Suit.Diamonds, 'H' => Suit.Hearts, _ => Suit.Spades
    }, value[1..] switch
    {
        "A" => Rank.Ace, "K" => Rank.King, "Q" => Rank.Queen, "J" => Rank.Jack,
        "T" or "10" => Rank.Ten, _ => (Rank)int.Parse(value[1..])
    })).ToArray();
}