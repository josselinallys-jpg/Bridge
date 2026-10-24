using System.Globalization;
using System.IO;

namespace Bridge.Desktop;

internal static class AuctionTooltipRules
{
    private static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        ["unknown"] = "Enchère non prévue",
        ["pass"] = "Passe : aucun seuil ou aucune enchère forcing adaptée.",
        ["pass-forcing"] = "Passe exclu par l'accord : la séquence est forcing de manche.",
        ["double"] = "Contre : demande de choix au partenaire avec soutien des couleurs non nommées.",
        ["redouble"] = "Surcontre : volonté de jouer la séquence contrée.",
        ["opening-major"] = "Ouverture {couleur} : {cartes} cartes, 12H ou 13HL minimum ({H}H, {HL}HL).",
        ["opening-minor"] = "Ouverture de mineure : meilleure mineure, 12H ou 11H avec 13HL ({H}H, {HL}HL).",
        ["opening-1nt"] = "Ouverture 1SA : distribution SA, {minimum1SA}-{maximum1SA}H, majeure cinquième {majeureCinquieme1SA} ({H}H, {HL}HL).",
        ["opening-2nt"] = "Ouverture 2SA : distribution SA, {minimum2SA}-{maximum2SA}HL, majeure cinquième {majeureCinquieme2SA} ({H}H, {HL}HL).",
        ["strong-two-clubs-opening"] = "Ouverture de 2♣ fort indéterminé, auto-forcing; le répondant relaie obligatoirement à 2♦ ({H}H, {HL}HL).",
        ["strong-two-diamonds-opening"] = "Ouverture de 2♦ forcing de manche : 24HL ou une levée de la manche avec deux levées défensives ({H}H, {HL}HL).",
        ["strong-gambling-three-no-trump"] = "Ouverture 3SA gambling : mineure maîtresse de sept cartes avec ARD et au plus une Dame annexe ({couleur}, {cartes} cartes).",
        ["weak-two"] = "Ouverture de 2 faible à {couleur} : six belles cartes, 5-10H; dénie une majeure quatrième annexe et deux gros honneurs latéraux ({H}H, {HL}HL).",
        ["preempt"] = "Ouverture de barrage à {palier}{couleur} : {cartes} cartes, 4-10H au palier 3 ou 5-10H au palier 4; belle couleur sans majeure annexe quatrième ({H}H, {HL}HL).",
        ["intervention-major"] = "Intervention à la couleur : {couleur}, au moins cinq cartes et 9-18HL ({cartes} cartes, {H}H, {HL}HL).",
        ["intervention-minor"] = "Intervention à la couleur : {couleur}, au moins cinq cartes et 9-18HL ({cartes} cartes, {H}H, {HL}HL).",
        ["intervention-nt"] = "Intervention à {palier}SA : jeu régulier et arrêt dans la couleur adverse; à 1SA, 16-18H ({H}H, {HL}HL).",
        ["intervention-two-level"] = "Intervention au palier 2 à {couleur} : six cartes robustes, ou cinq cartes solides avec une valeur d'ouverture ({cartes} cartes, {H}H, {HL}HL).",
        ["intervention-jump"] = "Intervention à saut à {couleur} : au moins six cartes, barrage faible jusqu'à 10H ({cartes} cartes, {H}H, {HL}HL).",
        ["one-nt-stayman"] = "Stayman : au moins {minimumStayman}HL et une majeure quatrième; demande la majeure de l'ouvreur ({H}H, {HL}HL).",
        ["one-nt-texas-hearts"] = "Texas cœurs : transfert obligatoire, au moins cinq cœurs ({cartes} cartes, {H}H, {HL}HL).",
        ["one-nt-texas-spades"] = "Texas piques : transfert obligatoire, au moins cinq piques ({cartes} cartes, {H}H, {HL}HL).",
        ["one-nt-texas-minor"] = "Texas mineur vers {couleur} : au moins cinq cartes, généralement six ou une courte ({cartes} cartes, {H}H, {HL}HL).",
        ["one-nt-quantitative"] = "Réponse quantitative à 1SA : zone de force pour jouer {palier}SA ({H}H, {HL}HL).",
        ["one-nt-slam-suit"] = "Enchère à saut : unicolore sixième et espoir de chelem à {couleur} ({H}H, {HL}HL).",
        ["one-nt-major-two-suited"] = "Bicolore majeur 5-5 : proposition de manche, l'ouvreur choisit la majeure ({zone}).",
        ["stayman-no-major"] = "Réponse au Stayman : aucune majeure quatrième.",
        ["stayman-four-hearts"] = "Réponse au Stayman : quatre cœurs, sans quatre piques.",
        ["stayman-four-spades"] = "Réponse au Stayman : quatre piques, sans quatre cœurs.",
        ["stayman-both-majors"] = "Réponse au Stayman : quatre cœurs et quatre piques.",
        ["one-nt-texas-rectification"] = "Rectification obligatoire du Texas vers {couleur}.",
        ["one-nt-major-game-choice"] = "Choix de la manche majeure : fit et bicolore majeur décrits à l'ouvreur.",
        ["landy-two-suited"] = "Landy sur 1SA : bicolore majeur, au moins quatre cartes dans chaque majeure.",
        ["stayman-two-nt-transfer"] = "Transfert propositionnel après 1SA-2♣-2SA : proposition de manche avec fit à {couleur}.",
        ["stayman-two-nt-transfer-game"] = "Transfert forcing de manche après 1SA-2♣-2SA : fit à {couleur}.",
        ["stayman-two-nt-transfer-rectification"] = "Rectification du transfert propositionnel vers {couleur}; elle promet un fit.",
        ["stayman-two-nt-transfer-game-rectification"] = "Rectification du transfert forcing de manche vers {couleur}; elle promet un fit.",
        ["stayman-slam-fit"] = "Fit majeur et ambition de chelem : convention 2012 ({couleur}).",
        ["texas-major-continuation"] = "Continuation après Texas majeur : six cartes ou soutien de la majeure ({couleur}).",
        ["texas-minor-continuation"] = "Continuation après Texas mineur : courte ou ambition de chelem décrite ({couleur}).",
        ["blackwood-ask"] = "Blackwood 5 clés : demande des As et du Roi d'atout.",
        ["blackwood-0-3"] = "Réponse Blackwood : 0 ou 3 clés.",
        ["blackwood-1-4"] = "Réponse Blackwood : 1 ou 4 clés.",
        ["blackwood-2-5-no-queen"] = "Réponse Blackwood : 2 ou 5 clés sans la Dame d'atout.",
        ["blackwood-2-5-queen"] = "Réponse Blackwood : 2 ou 5 clés avec la Dame d'atout.",
        ["blackwood-void-2-4"] = "Réponse Blackwood : chicane et 2 ou 4 clés.",
        ["blackwood-void-1-3"] = "Réponse Blackwood : chicane et 1 ou 3 clés ({couleur}).",
        ["slam"] = "Enchère de chelem : contrôle ou conclusion selon la séquence.",
        ["major-support-2"] = "Soutien de la majeure : {cartes} atouts, 6-10 HLD ({HLD} HLD estimés).",
        ["major-support-3"] = "Soutien de la majeure : {cartes} atouts, 11-12 HLD ({HLD} HLD estimés).",
        ["major-support-4"] = "Conclusion à 4M : barrage avec cinq atouts et 8H, ou soutien fort de manche avec 13-15 HLD selon la main ({H}H, {HLD} HLD estimés).",
        ["major-change-1"] = "Changement de couleur au palier 1 : {cartes} cartes, 6HL, forcing un tour ({H}H, {HL}HL).",
        ["major-change-2"] = "Changement de couleur au palier 2 : {cartes} cartes, 11H ou 12HL, auto-forcing ({H}H, {HL}HL).",
        ["nt"] = "Jeu régulier à Sans-Atout au palier {palier} ({H}H, {HL}HL).",
        ["rebid-repeat"] = "Répétition de la couleur d'ouverture : longueur et zone de force décrites ({H}H, {HL}HL).",
        ["rebid-nt"] = "Redemande à Sans-Atout : jeu régulier et zone de force décrite ({H}H, {HL}HL).",
        ["rebid-suit"] = "Redemande de l'ouvreur : nouvelle couleur naturelle, force décrite ({H}H, {HL}HL).",
        ["minor-change"] = "Annonce naturelle de mineure : longueur et force décrites au palier {palier} ({H}H, {HL}HL).",
        ["natural"] = "Enchère naturelle : décrit la longueur, la force ou le fit ({H}H, {HL}HL).",
        ["strong-two-clubs-relay"] = "Relais obligatoire à 2♦ sur l'ouverture forte de 2♣.",
        ["strong-two-clubs-description"] = "Redemande de 2♣ après le relais : décrit la longue couleur, la main semi-régulière ou le bicolore majeur ({HL}HL).",
        ["strong-two-clubs-development"] = "Développement après la description de l'ouvreur de 2♣ : soutien, couleur positive ou enchère d'attente ({H}H, {HL}HL).",
        ["strong-two-diamonds-no-ace"] = "Réponse à 2♦ forcing : aucun As, 0-7H ou 8+H avec une courte ({H}H).",
        ["strong-two-diamonds-no-ace-regular"] = "Réponse à 2♦ forcing : aucun As, 8H ou plus et au moins deux cartes par couleur ({H}H).",
        ["strong-two-diamonds-major-ace"] = "Réponse à 2♦ forcing : As de cœur ou de pique.",
        ["strong-two-diamonds-minor-ace"] = "Réponse à 2♦ forcing : As de la mineure annoncée ({couleur}).",
        ["strong-two-diamonds-ace-group"] = "Réponse à 2♦ forcing : groupe des deux As décrit par l'enchère ({couleur}).",
        ["strong-two-diamonds-rebid"] = "Redemande naturelle de l'ouvreur après 2♦ forcing ({H}H, {HL}HL).",
        ["strong-two-diamonds-development"] = "Développement forcing après 2♦ : la séquence reste forcing jusqu'à la manche ({H}H, {HL}HL).",
        ["strong-two-nt-stayman"] = "Stayman sur l'ouverture forte de 2SA : majeure quatrième, dès {minimumStayman2SA}HL.",
        ["strong-two-nt-major-texas"] = "Texas majeur sur 2SA : au moins cinq cartes dans la majeure transférée ({cartes} cartes).",
        ["strong-two-nt-club-texas"] = "Texas trèfle sur 2SA : six cartes ou plus avec espoir de chelem.",
        ["strong-two-nt-diamond-texas"] = "Texas carreau sur 2SA : six cartes ou plus avec espoir de chelem.",
        ["strong-two-nt-five-five"] = "Bicolore majeur 5-5 : l'ouvreur choisit la majeure.",
        ["strong-two-nt-major-game"] = "Conclusion à la manche majeure avec six cartes ou plus ({couleur}).",
        ["strong-two-nt-natural-game"] = "Conclusion naturelle à 3SA avec une main régulière sans fit majeur.",
        ["strong-two-nt-quantitative"] = "Proposition quantitative de petit ou grand chelem à Sans-Atout ({palier}SA, {HL}HL).",
        ["strong-two-nt-stayman-answer"] = "Réponse de l'ouvreur au Stayman après 2SA : décrit ses majeures quatrièmes.",
        ["strong-two-nt-texas-fit"] = "Rectification du Texas majeur : l'ouvreur promet un fit.",
        ["strong-two-nt-texas-no-fit"] = "Sans fit majeur, l'ouvreur revient à 3SA.",
        ["strong-two-nt-club-texas-rectification"] = "Rectification du Texas trèfle vers 4♣.",
        ["strong-two-nt-diamond-texas-rectification"] = "Rectification du Texas carreau vers 4♦.",
        ["strong-two-nt-five-five-choice"] = "Choix de la majeure après l'annonce du bicolore majeur 5-5.",
        ["strong-opening-interference"] = "Défense sur ouverture forte : enchère naturelle à partir de cinq cartes et 5H; contre ou surcontre avec au moins 5H sans enchère naturelle."
    };

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Loaded = new(Load);

    public static string Get(string key, IReadOnlyDictionary<string, string>? values = null)
    {
        if (!Loaded.Value.TryGetValue(key, out var text))
            return values is not null && values.TryGetValue("descriptionRegle", out var description)
                ? string.Join("\n", new[] { description, values.GetValueOrDefault("conditionsRegle"), values.GetValueOrDefault("forcing") }
                    .Where(part => !string.IsNullOrWhiteSpace(part)))
                : "Enchère non prévue";
        if (values is null) return text;
        foreach (var pair in values)
            text = text.Replace("{" + pair.Key + "}", pair.Value, StringComparison.OrdinalIgnoreCase);
        return text;
    }

    private static IReadOnlyDictionary<string, string> Load()
    {
        var rules = new Dictionary<string, string>(Defaults);
        var path = Path.Combine(AppContext.BaseDirectory, "Rules", "auction-tooltip-rules.csv");
        if (!File.Exists(path)) return rules;
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var separator = line.IndexOf(';');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            var text = line[(separator + 1)..].Trim();
            if (key.Length > 0 && text.Length > 0) rules[key] = text;
        }
        return rules;
    }
}
