namespace Bridge.AI.Bidding;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record BiddingProfile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public int SchemaVersion { get; init; } = 1;
    public string Id { get; init; } = "sef-standard";
    public NoTrumpOpeningSettings OneNoTrump { get; init; } = new();
    public NoTrumpOpeningSettings TwoNoTrump { get; init; } = new() { Minimum = 20, Maximum = 21, AllowFiveCardMajor = false };
    public NoTrumpConventionSettings Conventions { get; init; } = new();
    public OneNoTrumpResponseSettings Responses { get; init; } = new();
    public IReadOnlyList<BiddingRule> Rules { get; init; } = [];
    public bool UseStandardFallback { get; init; } = true;
    public BiddingEvaluationSettings Evaluation { get; init; } = new();
    public BiddingBackupSettings Backup { get; init; } = new();
    public IReadOnlyDictionary<string, bool> CustomConventions { get; init; } = new Dictionary<string, bool>();

    public bool IsConventionEnabled(string id) => CustomConventions.TryGetValue(id, out var enabled)
        ? enabled
        : id switch
        {
            "stayman" => Conventions.Stayman,
            "majorTransfers" => Conventions.MajorTransfers,
            "minorTransfers" => Conventions.MinorTransfers,
            "weakTwo" or "preempts" or "gambling" or "strongTwoClubs" or "gameForcingTwoDiamonds" or
            "fittedTwoNoTrump" or "fittedThreeNoTrump" or "drury" or "truscott" or "splinter" or
            "meetingBids" or "sputnik" or "cuebid" or "cappelletti" or "landy" or "rubensohl" or
            "chasseCroise" or "goldenMisery" or "staymanTransfers" or "convention2012" or
            "majorTwoSuiters" or "minorTwoSuiters" or "weakTwoRelay" or "gamblingRelays" or
            "aceResponses" or "blackwood" or "roudi" or "fourthSuitForcing" or "thirdSuitForcing" => true,
            _ => throw new ArgumentException($"Convention inconnue : {id}.")
        };

    public static BiddingProfile Load(string path) => FromJson(File.ReadAllText(path));

    public static BiddingProfile LoadForApplication()
    {
        var selectedPath = Environment.GetEnvironmentVariable("BRIDGE_BIDDING_PROFILE");
        if (!string.IsNullOrWhiteSpace(selectedPath)) return Load(selectedPath);
        var declarativePath = Path.Combine(AppContext.BaseDirectory, "sef-bidding-profile.json");
        var defaultPath = File.Exists(declarativePath) ? declarativePath : Path.Combine(AppContext.BaseDirectory, "bidding-profile.json");
        return File.Exists(defaultPath) ? Load(defaultPath) : new BiddingProfile();
    }

    public static BiddingProfile FromJson(string json)
    {
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Le profil d'encheres doit etre un objet JSON.", nameof(json));
            if (document.RootElement.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array)
                foreach (var rule in rules.EnumerateArray())
                    if (rule.ValueKind != JsonValueKind.Object ||
                        !rule.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object ||
                        !response.TryGetProperty("forcing", out _))
                        throw new ArgumentException("Chaque reponse de regle doit preciser forcing (NonForcing, OneRound, SelfForcing ou GameForcing).", nameof(json));
        }
        var profile = JsonSerializer.Deserialize<BiddingProfile>(json, JsonOptions)
            ?? throw new ArgumentException("Le profil d'encheres est vide.", nameof(json));
        profile.Validate();
        return profile;
    }

    public string ToJson()
    {
        Validate();
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public void Validate()
    {
        if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(Id))
            throw new ArgumentException("Version ou identifiant de profil invalide.");
        if (OneNoTrump is null || TwoNoTrump is null || Conventions is null || Responses is null || Evaluation is null || Backup is null)
            throw new ArgumentException("Les sections du profil ne peuvent pas etre nulles.");
        OneNoTrump.Validate();
        TwoNoTrump.Validate();
        if (OneNoTrump.Minimum < 10 || TwoNoTrump.Minimum < 18 ||
            OneNoTrump.Maximum >= TwoNoTrump.Minimum || TwoNoTrump.Maximum >= 22)
            throw new ArgumentException("Les ouvertures a SA doivent etre disjointes et inferieures a 2 trefles fort.");
        if (Conventions.StaymanMinimumHL is < 0 or > 37 || Conventions.TwoNoTrumpStaymanMinimumHL is < 0 or > 37)
            throw new ArgumentException("Seuil Stayman invalide.");
        Responses.Validate();
        Evaluation.Validate();
        Backup.Validate();
        if (Rules is null || Rules.Any(rule => rule is null) ||
            Rules.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count() != Rules.Count)
            throw new ArgumentException("Les regles doivent avoir des identifiants uniques.");
        if (CustomConventions is null || CustomConventions.Keys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Catalogue de conventions invalide.");
        foreach (var rule in Rules)
        {
            rule.Validate();
            if (rule.Convention is { } convention) IsConventionEnabled(convention);
            foreach (var required in rule.RequiresConventions) IsConventionEnabled(required);
            foreach (var disabled in rule.DisabledConventions) IsConventionEnabled(disabled);
        }
    }
}

public sealed record NoTrumpOpeningSettings
{
    public int Minimum { get; init; } = 15;
    public int Maximum { get; init; } = 17;
    public bool AllowFiveCardMajor { get; init; } = true;

    internal void Validate()
    {
        if (Minimum < 0 || Maximum > 40 || Minimum > Maximum)
            throw new ArgumentException("Zone de points d'ouverture a SA invalide.");
    }
}

public sealed record NoTrumpConventionSettings
{
    public bool Stayman { get; init; } = true;
    public int StaymanMinimumHL { get; init; } = 8;
    public int TwoNoTrumpStaymanMinimumHL { get; init; } = 4;
    public bool MajorTransfers { get; init; } = true;
    public bool MinorTransfers { get; init; } = true;
}

public sealed record OneNoTrumpResponseSettings
{
    public int InvitationMinimumHL { get; init; } = 8;
    public int GameMinimumHL { get; init; } = 10;
    public int SmallSlamInvitationMinimumHL { get; init; } = 16;
    public int SmallSlamMinimumHL { get; init; } = 18;
    public int GrandSlamInvitationMinimumHL { get; init; } = 20;
    public int GrandSlamMinimumHL { get; init; } = 22;
    public int MaximumHL { get; init; } = 25;

    internal void Validate()
    {
        var thresholds = new[] { InvitationMinimumHL, GameMinimumHL, SmallSlamInvitationMinimumHL,
            SmallSlamMinimumHL, GrandSlamInvitationMinimumHL, GrandSlamMinimumHL, MaximumHL + 1 };
        if (InvitationMinimumHL < 0 || MaximumHL > 37 ||
            thresholds.Zip(thresholds.Skip(1), (left, right) => left < right).Any(increasing => !increasing))
            throw new ArgumentException("Les zones de reponse a 1SA doivent etre strictement croissantes.");
    }
}

public sealed record BiddingBackupSettings
{
    public int MinimumAgreedFit { get; init; } = 8;
    public int GameHighCardPoints { get; init; } = 25;
    public int GameFitPoints { get; init; } = 25;
    public int SlamHighCardPoints { get; init; } = 33;
    public int SlamFitPoints { get; init; } = 33;
    public int MinimumNaturalSuitLength { get; init; } = 5;
    public int PartnerOneLevelMinimumH { get; init; } = 12;
    public int PartnerOneLevelMinimumHL { get; init; } = 13;
    public int PartnerOneNoTrumpMinimumH { get; init; } = 15;
    public int PartnerTwoNoTrumpMinimumHL { get; init; } = 20;
    public int PartnerStrongTwoClubsMinimumHL { get; init; } = 21;
    public int PartnerGameForceMinimumHL { get; init; } = 24;
    public bool RequireAllStopperForNoTrump { get; init; } = true;

    internal void Validate()
    {
        var pointThresholds = new[] { GameHighCardPoints, GameFitPoints, SlamHighCardPoints, SlamFitPoints,
            PartnerOneLevelMinimumH, PartnerOneLevelMinimumHL, PartnerOneNoTrumpMinimumH,
            PartnerTwoNoTrumpMinimumHL, PartnerStrongTwoClubsMinimumHL, PartnerGameForceMinimumHL };
        if (MinimumAgreedFit is < 6 or > 13 || MinimumNaturalSuitLength is < 4 or > 13 ||
            pointThresholds.Any(points => points is < 0 or > 40) ||
            GameHighCardPoints > SlamHighCardPoints || GameFitPoints > SlamFitPoints)
            throw new ArgumentException("Seuils du secours d'encheres invalides.");
    }
}