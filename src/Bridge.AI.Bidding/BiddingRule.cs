namespace Bridge.AI.Bidding;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<BiddingStage>))]
public enum BiddingStage
{
    Any,
    Opening,
    Response,
    OpenerRebid,
    ResponderRebid,
    Continuation,
    Intervention
}

[JsonConverter(typeof(JsonStringEnumConverter<RuleCallAction>))]
public enum RuleCallAction { Bid, Pass, Double, Redouble }

[JsonConverter(typeof(JsonStringEnumConverter<RuleDenomination>))]
public enum RuleDenomination
{
    Clubs, Diamonds, Hearts, Spades, NoTrump,
    Opening, Partner, Opponent, Longest, LongestMajor, LongestMinor, OtherMajor, Shortest,
    Candidate, FirstResponse, OpenerRebid, FourthSuit, OtherMinor, Trump, FittedMajor, CheapCueBid, ExpensiveCueBid, OtherUnbid,
    TransferredMinor, ShortMajor
}

[JsonConverter(typeof(JsonStringEnumConverter<RuleBidLevel>))]
public enum RuleBidLevel { Fixed, Cheapest, Jump, Game, SmallSlam, GrandSlam, DoubleJump, Support, SupportJump }

[JsonConverter(typeof(JsonStringEnumConverter<RuleForcing>))]
public enum RuleForcing { NonForcing, OneRound, SelfForcing, GameForcing }

[JsonConverter(typeof(JsonStringEnumConverter<RuleSuitOrder>))]
public enum RuleSuitOrder { LongestThenHighest, LongestThenLowest, Lowest, Highest }

public sealed record BiddingRule
{
    public string Id { get; init; } = "";
    public string SefSection { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public int Priority { get; init; }
    public string? Convention { get; init; }
    public IReadOnlyList<string> RequiresConventions { get; init; } = [];
    public IReadOnlyList<string> DisabledConventions { get; init; } = [];
    public BiddingStage Stage { get; init; } = BiddingStage.Any;
    public IReadOnlyList<string> SideSequence { get; init; } = [];
    public bool AllowAdditionalSideBids { get; init; }
    public IReadOnlyList<string> OpponentSequence { get; init; } = [];
    public IReadOnlyList<string> AuctionSequence { get; init; } = [];
    public bool IgnorePasses { get; init; } = true;
    public string? LastSideCall { get; init; }
    public string? LastOpponentCall { get; init; }
    public RuleCallAction? LastOpponentAction { get; init; }
    public bool? OpponentsHaveActed { get; init; }
    public IReadOnlyList<RuleDenomination> CandidateSuits { get; init; } = [];
    public RuleSuitOrder SuitOrder { get; init; }
    public IReadOnlyList<HandCondition> Conditions { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<HandCondition>> AnyOf { get; init; } = [];
    public RuleResponse Response { get; init; } = new();

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Response is null || SideSequence is null ||
            OpponentSequence is null || AuctionSequence is null || Conditions is null || AnyOf is null ||
            RequiresConventions is null || RequiresConventions.Any(string.IsNullOrWhiteSpace) ||
            DisabledConventions is null || DisabledConventions.Any(string.IsNullOrWhiteSpace) ||
            CandidateSuits is null || CandidateSuits.Any(suit => suit > RuleDenomination.Spades || !Enum.IsDefined(suit)) ||
            !Enum.IsDefined(Stage) || !Enum.IsDefined(SuitOrder) ||
            LastOpponentAction.HasValue && !Enum.IsDefined(LastOpponentAction.Value))
            throw new ArgumentException("Regle d'encheres incomplete.");
        foreach (var pattern in SideSequence.Concat(OpponentSequence).Concat(AuctionSequence)
                 .Concat(new[] { LastSideCall, LastOpponentCall }.OfType<string>()))
            if (!IsCallPattern(pattern)) throw new ArgumentException($"Sequence invalide : {pattern}.");
        foreach (var condition in Conditions.Concat(AnyOf.SelectMany(group =>
                     group ?? throw new ArgumentException("Groupe de conditions nul."))))
        {
            if (condition is null) throw new ArgumentException("Condition nulle.");
            condition.Validate();
        }
        if (AnyOf.Any(group => group.Count == 0))
            throw new ArgumentException("Un groupe alternatif ne peut pas etre vide.");
        if (CandidateSuits.Count == 0 && (Response.Denomination == RuleDenomination.Candidate ||
            Conditions.Concat(AnyOf.SelectMany(group => group)).Any(condition =>
                condition.Fact.Contains("candidate", StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Une regle utilisant une couleur candidate doit declarer ses couleurs.");
        Response.Validate();
    }

    internal static bool IsCallPattern(string? pattern) => pattern is "*" or "P" or "X" or "XX" ||
        pattern is { Length: 2 or 3 } && pattern[0] is >= '1' and <= '7' &&
        pattern[1..] is "C" or "D" or "H" or "S" or "NT" or "M" or "m" or "x";
}

public sealed record HandCondition
{
    public string Fact { get; init; } = "";
    public int? Minimum { get; init; }
    public int? Maximum { get; init; }
    public string? MinimumFact { get; init; }
    public string? MaximumFact { get; init; }

    internal void Validate()
    {
        if (!IsKnownFact(Fact) || Minimum is null && Maximum is null && MinimumFact is null && MaximumFact is null ||
            Minimum > Maximum || MinimumFact is not null && !IsKnownFact(MinimumFact) ||
            MaximumFact is not null && !IsKnownFact(MaximumFact))
            throw new ArgumentException($"Condition invalide : {Fact}.");
    }

    internal static bool IsKnownFact(string? fact)
    {
        if (fact is "h" or "hl" or "hld" or "fitPoints" or "aces" or "keys" or "trumpQueen" or
            "balanced" or "regular" or "singletons" or "voids" or "doubletons" or "majorFours" or
            "losers" or "playingTricks" or "playingTricksHalf" or "defensiveTricks" or "passedHand" or "passedOpening" or
            "ownBids" or "partnerBids" or "opponentBids" or "lastOpponentLevel" or "sideBids" or
            "opponentDoubled" or "opponentRedoubled" or "sameColorAces" or "sameRankAces" or
            "candidateIsOpening" or "candidateIsOpponent" or "candidateIsMajor" or "candidateIsResponse" or
            "openingLevel" or "openingIsMajor" or "openingIsMinor" or "firstResponseLevel" or
            "firstResponseIsOpening" or "lastSideIsOpening" or "agreedTrump" or "firstThreeDistinct" or "shortSuitsHaveHonor" or
            "allUnbidStoppers" or
            "candidateIsPartner" or "partnerFirstBidIsCandidate" or "partnerIsMajor" or "openerRebidIsMajor" or "openerRebidLevel" or
            "hasSideControl" or "partnerAskedBlackwood" or "opponentsShowTwoSuits" or "length.otherMajorThanCandidate") return true;
        if (fact is "sideMajorFours" or "ownKings" or "ownQueens" or "majorFit" or "singleSuited" or "twoSuited" or "sideFourCardSuits" or
            "vulnerable" or "opponentsVulnerable" or "favorableVulnerability" or "seatPosition" or "gameReached") return true;
        if (fact is "responseFitPoints" or "agreedFitPoints" or "adjustedHL" or "adjustedH" or "adjustedResponseFitPoints" or
            "semiRegular" or "oneLevelMajorAvailable" or "candidateCheapestLevel" or "repeatCheapestLevel" or
            "candidateReverse" or "candidateEconomic" or "concentratedHonorsPercent" or "repeatNeedsSix" or
            "forcedRebid" or "freeRebid" or "partnerSputnik" or "opponentsRaised" or "trialSpace" or
            "shortSuitStoppers" or "shortHonor" or "partnerResponseIsSuit" or "openerFakeJump" or "partnerSupport" or
            "naturalResponse" or "fourthSeatActed" or "freeActionRequired" or "openerControlFit") return true;
        if (fact is "minorTexas" or "minorTexasShortness" or "minorTexasAgreed" or "minorTexasFitPoints" or
            "candidateIsTransferredMinor" or "candidateIsShortMajor" or "candidateIsShownControl" or
            "minorTexasControlsCovered" or "minorTexasNewControl" or "minorTexasBlackwoodAnswered" or "minorTexasCombinedKeys") return true;
        if (fact is null) return false;
        var parts = fact.Split('.');
        if (parts.Length == 2 && parts[0] == "shape")
            return parts[1].Length == 4 && parts[1].All(char.IsAsciiDigit) && parts[1].Sum(character => character - '0') == 13;
        return parts.Length == 2 &&
            parts[0] is "length" or "honors" or "bigHonors" or "sideBigHonors" or "sideQueens" or "topHonors" or "stopper" or "ace" or "king" or "queen" or "force" or "control" &&
            parts[1] is "clubs" or "diamonds" or "hearts" or "spades" or "opening" or "partner" or
                "opponent" or "longest" or "longestMajor" or "longestMinor" or "otherMajor" or "shortest" or
                "candidate" or "firstResponse" or "openerRebid" or "fourthSuit" or "otherMinor" or "trump" or "otherUnbid" or
                "transferredMinor" or "shortMajor";
    }
}

public sealed record RuleResponse
{
    public RuleCallAction Action { get; init; } = RuleCallAction.Bid;
    public int Level { get; init; } = 1;
    public RuleDenomination Denomination { get; init; } = RuleDenomination.NoTrump;
    public RuleBidLevel LevelMode { get; init; } = RuleBidLevel.Fixed;
    public RuleForcing? Forcing { get; init; }
    public int MinimumLevel { get; init; } = 1;
    public int MaximumLevel { get; init; } = 7;

    internal void Validate()
    {
        if (!Enum.IsDefined(Action) || !Enum.IsDefined(Denomination) || !Enum.IsDefined(LevelMode) ||
            Forcing is null || !Enum.IsDefined(Forcing.Value) || Level is < 1 or > 7 ||
            MinimumLevel < 1 || MaximumLevel > 7 || MinimumLevel > MaximumLevel)
            throw new ArgumentException("Reponse de regle invalide.");
    }
}

public sealed record BiddingEvaluationSettings
{
    public int Ace { get; init; } = 4;
    public int King { get; init; } = 3;
    public int Queen { get; init; } = 2;
    public int Jack { get; init; } = 1;
    public int LengthStartsAt { get; init; } = 5;
    public int LengthPoint { get; init; } = 1;
    public int Void { get; init; } = 3;
    public int Singleton { get; init; } = 2;
    public int Doubleton { get; init; } = 1;
    public int NinthTrump { get; init; } = 2;
    public int ExtraTrump { get; init; } = 1;

    internal void Validate()
    {
        if (LengthStartsAt is < 1 or > 13 ||
            new[] { Ace, King, Queen, Jack, LengthPoint, Void, Singleton, Doubleton, NinthTrump, ExtraTrump }
                .Any(points => points is < 0 or > 40))
            throw new ArgumentException("Evaluation des mains invalide.");
    }
}