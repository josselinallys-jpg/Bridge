namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

internal static class BiddingRuleText
{
    public static string Description(BiddingRule rule, AuctionCall call)
    {
        if (!string.IsNullOrWhiteSpace(rule.Description)) return rule.Description;
        if (call.Action == AuctionAction.Pass)
            return rule.Response.Forcing == RuleForcing.NonForcing
                ? "Passe : arret de la sequence, sans nouvelle proposition."
                : "Passe forcing : le partenaire doit encore se prononcer.";
        if (call.Action == AuctionAction.Double && rule.Convention is null)
            return rule.Id.Contains("penalty", StringComparison.Ordinal) || rule.SefSection.StartsWith("5.", StringComparison.Ordinal)
                ? "Contre punitif : proposition de jouer le contrat adverse contre."
                : "Contre d'appel : demande au partenaire de decrire sa main ou de choisir une couleur.";
        if (call.Action == AuctionAction.Redouble && rule.Convention is null)
            return "Surcontre : reponse positive au contre adverse selon les conditions de la sequence.";
        var meaning = rule.Convention switch
        {
            "stayman" => "Stayman : recherche d'un fit majeur",
            "majorTransfers" => "Texas majeur et ses developpements",
            "minorTransfers" => "Texas mineur et ses developpements",
            "staymanTransfers" => "Transfert apres Stayman",
            "chasseCroise" => "Chasse-croise : recherche du fit majeur",
            "goldenMisery" => "Majeure cinquieme faible apres Stayman",
            "convention2012" => "Fit majeur et ambition de chelem apres Stayman",
            "fittedTwoNoTrump" => "2SA fitte et ses developpements",
            "fittedThreeNoTrump" => "3SA fitte et recherche de chelem",
            "drury" => "Drury : soutien majeur apres passe",
            "truscott" => "Truscott : soutien apres contre adverse",
            "splinter" => "Splinter : fit et courte dans la couleur annoncee",
            "openerSuperForcing" => "Soutien fort differe par un bicolore forcing",
            "openerFitJump" => "Mineure sixieme et fit majeur quatrieme",
            "openerTrialBids" => "Enchere d'essai de manche apres soutien majeur",
            "openerTrialDouble" => "Contre d'essai de manche, non punitif",
            "weakTwoRelay" => "Relais 2SA sur un deux faible",
            "weakTwo" => "Deux faible en majeure",
            "preempts" => "Enchere de barrage",
            "gambling" => "3SA gambling : mineure maitresse",
            "gamblingRelays" => "Relais apres 3SA gambling",
            "blackwood" => "Blackwood cinq cles",
            "roudi" => "Roudi : recherche du fit majeur",
            "thirdSuitForcing" => "Troisieme couleur forcing",
            "fourthSuitForcing" => "Quatrieme couleur forcing",
            "cuebid" => "Cuebid : enchere artificielle dans une couleur adverse",
            "sputnik" => "Contre Sputnik d'appel",
            "rubensohl" => "Rubensohl apres intervention sur 1SA",
            "landy" => "Landy : bicolore majeur sur 1SA",
            "cappelletti" => "Bicolore majeur apres intervention a 1SA",
            "meetingBids" => "Enchere de rencontre : fit et couleur cinquieme",
            "majorTwoSuiters" => "Bicolore majeur : recherche de la meilleure manche",
            "minorTwoSuiters" => "Bicolore mineur apres Texas",
            "strongTwoClubs" => "2 trefles fort et ses developpements",
            "gameForcingTwoDiamonds" => "2 carreaux forcing de manche et ses developpements",
            "aceResponses" => "Reponse en As a 2 carreaux forcing de manche",
            _ => rule.Stage switch
            {
                BiddingStage.Opening => "Ouverture selon la force et la distribution",
                BiddingStage.Response => "Reponse a l'ouverture du partenaire",
                BiddingStage.OpenerRebid => "Redemande de l'ouvreur",
                BiddingStage.ResponderRebid => "Deuxieme enchere du repondant",
                BiddingStage.Intervention => "Intervention apres ouverture adverse",
                _ => "Continuation de la sequence du camp"
            }
        };
        var action = call.Action switch
        {
            AuctionAction.Pass => "Passe", AuctionAction.Double => "Contre",
            AuctionAction.Redouble => "Surcontre", _ => call.Bid?.ToString() ?? "Enchere"
        };
        return $"{action} : {meaning}.";
    }

    public static string Forcing(RuleForcing forcing) => forcing switch
    {
        RuleForcing.OneRound => "Forcing un tour",
        RuleForcing.SelfForcing => "Auto-forcing",
        RuleForcing.GameForcing => "Forcing de manche",
        _ => "Non forcing"
    };

    public static string Conditions(BiddingRule rule)
    {
        var conditions = rule.Conditions.Select(Condition).ToList();
        if (rule.AnyOf.Count > 0)
            conditions.Add("(" + string.Join(" ou ", rule.AnyOf.Select(group => string.Join(" et ", group.Select(Condition)))) + ")");
        return string.Join(" ; ", conditions);
    }

    private static string Condition(HandCondition condition)
    {
        var name = FactName(condition.Fact);
        var minimum = condition.MinimumFact is { } lower ? FactName(lower) : condition.Minimum?.ToString();
        var maximum = condition.MaximumFact is { } upper ? FactName(upper) : condition.Maximum?.ToString();
        if (minimum == maximum && minimum is not null) return $"{name} : {minimum}";
        if (minimum is not null && maximum is not null) return $"{name} : de {minimum} a {maximum}";
        return minimum is not null ? $"{name} : au moins {minimum}" : $"{name} : au plus {maximum}";
    }

    private static string FactName(string fact)
    {
        if (fact.StartsWith("shape.", StringComparison.Ordinal)) return "Distribution " + string.Join("-", fact[6..].ToCharArray());
        var parts = fact.Split('.');
        if (parts.Length == 2)
        {
            var color = parts[1] switch
            {
                "clubs" => "a trefle", "diamonds" => "a carreau", "hearts" => "a coeur", "spades" => "a pique",
                "opening" => "dans la couleur d'ouverture", "partner" => "dans la couleur du partenaire",
                "opponent" => "dans la couleur adverse", "candidate" => "dans la couleur annoncee",
                "firstResponse" => "dans la couleur du repondant", "trump" => "a l'atout",
                "otherMajor" => "dans l'autre majeure", "otherMinor" => "dans l'autre mineure",
                "longestMajor" => "dans la majeure la plus longue", "longestMinor" => "dans la mineure la plus longue",
                "longest" => "dans la couleur la plus longue", "shortest" => "dans la couleur la plus courte",
                "otherUnbid" => "dans la couleur residuelle", "transferredMinor" => "dans la mineure transferee",
                "shortMajor" => "dans la majeure courte du partenaire", "fourthSuit" => "dans la quatrieme couleur",
                "openerRebid" => "dans la couleur de redemande", _ => "dans la couleur de la sequence"
            };
            var metric = parts[0] switch
            {
                "length" => "Cartes", "honors" => "Honneurs", "bigHonors" => "As ou Rois",
                "sideBigHonors" => "As ou Rois exterieurs", "sideQueens" => "Dames exterieures",
                "topHonors" => "As-Roi-Dame", "stopper" => "Arret", "control" => "Controle",
                "force" => "Force", "ace" => "As", "king" => "Roi", "queen" => "Dame", _ => "Valeur"
            };
            return $"{metric} {color}";
        }
        return fact switch
        {
            "h" or "adjustedH" => "Points H", "hl" or "adjustedHL" => "Points HL", "hld" => "Points HLD",
            "fitPoints" or "responseFitPoints" or "agreedFitPoints" or "adjustedResponseFitPoints" => "Points de soutien HLD",
            "aces" => "Nombre d'As", "keys" => "Cles", "trumpQueen" => "Dame d'atout",
            "regular" => "Main reguliere", "semiRegular" => "Main semi-reguliere", "balanced" => "Main equilibree",
            "singletons" => "Singletons", "voids" => "Chicanes", "doubletons" => "Doubletons",
            "majorFours" => "Majeures quatriemes", "majorFit" => "Fit majeur", "singleSuited" => "Main unicolore",
            "twoSuited" => "Main bicolore", "losers" => "Perdantes", "playingTricks" => "Levees de jeu",
            "playingTricksHalf" => "Levees de jeu (demi-plis inclus)",
            "defensiveTricks" => "Levees defensives", "concentratedHonorsPercent" => "Pourcentage d'honneurs dans les deux couleurs",
            "passedHand" => "Main ayant passe", "passedOpening" => "Ouverture en troisieme ou quatrieme position",
            "freeRebid" => "Redemande libre", "forcedRebid" => "Redemande forcee", "partnerSputnik" => "Contre Sputnik du partenaire",
            "vulnerable" => "Camp vulnerable", "opponentsVulnerable" => "Adversaires vulnerables",
            "favorableVulnerability" => "Vulnerabilite favorable", "seatPosition" => "Position a la table",
            "firstResponseIsOpening" or "lastSideIsOpening" => "Soutien de la couleur d'ouverture",
            "candidateIsOpening" => "Couleur annoncee identique a l'ouverture", "candidateIsResponse" or "candidateIsPartner" => "Soutien du repondant",
            "candidateIsOpponent" => "Couleur annoncee identique a la couleur adverse", "candidateIsMajor" => "Couleur annoncee majeure",
            "firstResponseLevel" => "Palier de reponse", "openingLevel" => "Palier d'ouverture",
            "lastOpponentLevel" => "Palier adverse", "partnerSupport" => "Atouts dans la couleur du repondant",
            "agreedTrump" => "Atout agree", "gameReached" => "Manche atteinte", "opponentsRaised" => "Soutien adverse",
            "trialSpace" => "Place pour une enchere d'essai", "shortSuitStoppers" => "Arrets dans les doubletons",
            "shortHonor" => "Gros honneur sec", "candidateReverse" => "Bicolore cher", "candidateEconomic" => "Bicolore economique",
            "oneLevelMajorAvailable" => "Majeure quatrieme annoncable au palier 1", "repeatNeedsSix" => "Repetition promettant six cartes",
            "partnerAskedBlackwood" => "Demande de cles du partenaire", "naturalResponse" => "Reponse naturelle",
            "opponentDoubled" => "Contre adverse", "opponentRedoubled" => "Surcontre adverse",
            "firstThreeDistinct" => "Nombre de couleurs nommees", "sideBids" => "Encheres du camp",
            "ownBids" => "Encheres du joueur", "partnerBids" => "Encheres du partenaire", "opponentBids" => "Encheres adverses",
            "shortSuitsHaveHonor" => "Honneur dans chaque doubleton", "sideMajorFours" => "Majeures quatriemes annexes",
            "sideFourCardSuits" => "Couleurs quatriemes annexes", "ownKings" => "Nombre de Rois", "ownQueens" => "Nombre de Dames",
            "sameColorAces" => "Deux As de meme couleur", "sameRankAces" => "Deux As de meme rang",
            "openingIsMajor" => "Ouverture majeure", "openingIsMinor" => "Ouverture mineure",
            "partnerIsMajor" => "Majeure annoncee par le partenaire", "openerRebidIsMajor" => "Redemande majeure",
            "openerRebidLevel" => "Palier de redemande", "hasSideControl" => "Controle lateral",
            "opponentsShowTwoSuits" => "Intervention bicolore", "candidateCheapestLevel" => "Palier economique de la couleur",
            "repeatCheapestLevel" => "Palier de repetition", "partnerResponseIsSuit" => "Reponse a la couleur",
            "openerFakeJump" => "Soutien differe par un faux bicolore", "fourthSeatActed" => "Intervention du quatrieme joueur",
            "freeActionRequired" => "Force ou distribution imposant une action", "openerControlFit" => "Fit quatrieme du repondant",
            "minorTexas" => "Sequence de Texas mineur", "minorTexasShortness" => "Courte majeure annoncee",
            "minorTexasAgreed" => "Fit dans la mineure transferee", "minorTexasFitPoints" => "Points HLD dans la mineure transferee",
            "candidateIsTransferredMinor" => "Couleur annoncee identique a l'atout mineur",
            "candidateIsShortMajor" => "Couleur annoncee identique a la courte majeure",
            "candidateIsShownControl" => "Controle deja annonce", "minorTexasControlsCovered" => "Controles lateraux assures",
            "minorTexasNewControl" => "Nouveau controle du partenaire",
            "minorTexasBlackwoodAnswered" => "Reponse au Blackwood dans la mineure transferee",
            "minorTexasCombinedKeys" => "Cles estimees du camp dans l'atout mineur",
            _ => "Condition de la sequence"
        };
    }
}