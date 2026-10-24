# Enchères SEF

Le moteur `SefBiddingEngine` applique le socle du Système d'Enseignement Français décrit dans la référence Majeure cinquième.

## Profil configurable

Le fichier `sef-bidding-profile.json`, à la racine du dépôt, est copié dans les sorties
console et WPF. Les applications le chargent par défaut au démarrage. Pour utiliser un autre
fichier sans recompilation, définir son chemin avant de lancer l'application :

```powershell
$env:BRIDGE_BIDDING_PROFILE = 'C:\Bridge\profils\mon-systeme.json'
dotnet run --project src/Bridge.Console
```

Sans variable, le catalogue SEF situé à côté de l'exécutable est utilisé. En son
absence, `bidding-profile.json` sélectionne le mode historique ; sans aucun fichier,
le moteur utilise les paramètres historiques standard. Un fichier invalide est refusé.
Un fichier sélectionné explicitement doit exister. Le profil est immuable durant
la session et commun aux quatre sièges ; redémarrer pour changer de système.
Les partenaires humains doivent appliquer les mêmes accords que les IA.

### Catalogue SEF déclaratif

Le catalogue contient des règles couvrant les familles des huit chapitres de
`SEF.txt`. Les seuils, longueurs, distributions, réponses et conventions sont des
données éditables, pas des constantes à recompiler. `useStandardFallback: false`
interdit le recours aux décisions historiques : une situation non couverte aboutit
à passe avec la trace « Aucune regle configuree applicable ».

| Section de SEF.txt | Configuration |
|---|---|
| 1. Classification et évaluation | `evaluation` : H, L, D et revalorisation des atouts ; faits de régularité, unicolore et bicolore |
| 2. Ouvertures mineures | `opening.minor.*`, `response.minor.*`, `minor.*`, suites naturelles et conventions |
| 3. Ouvertures majeures | `opening.major*`, `response.major.*`, `major.*`, Drury, Truscott, soutiens et splinters |
| 4. Ouverture de 1SA | `one-nt.*`, Stayman, Texas, chassé-croisé, misères dorées, convention 2012 et Rubensohl |
| 5. Barrages | `opening.weak-two`, `opening.preempt-*`, `weak-two.*`, `preempt.*`, `gambling.*`, Blackwood |
| 6. Ouvertures fortes | `strong-clubs.*`, `game-force.*`, `strong-nt.*`, `two-nt.*`, défenses après intervention |
| 7. Redemandes de l'ouvreur | `opener.*` : soutiens, SA, bicolores, répétitions, enchères libres/forcées et Sputnik |
| 8. Deuxième enchère du répondant | `responder.*` : essais après fit, relais, forcing, arrêts et développements naturels |

L'évaluation HLD ajoute les points D seulement quand un fit d'au moins huit atouts
est établi ou décrit par la séquence. Sans fit, les points comptés sont H+L.

Chaque règle possède un `id` unique et une `sefSection` indiquant sa provenance.
La priorité la plus élevée est examinée en premier ; à priorité égale, l'ordre du
fichier est conservé. La première règle applicable produisant un appel légal gagne.
Une réponse illégale est ignorée ; le moteur examine alors la règle suivante.
Les domaines de points de règles concurrentes peuvent se chevaucher volontairement :
la priorité les départage, contrairement aux plages nommées du profil historique.

Exemple : modifier le seuil et la réponse d'un soutien majeur sans toucher au C# :

```json
{
	"id": "response.major.raise-simple",
	"sefSection": "3.soutiens",
	"stage": "Response",
	"priority": 830,
	"sideSequence": ["1M"],
	"opponentsHaveActed": false,
	"conditions": [
		{ "fact": "length.opening", "minimum": 3 },
		{ "fact": "fitPoints", "minimum": 6, "maximum": 10 }
	],
	"response": { "level": 2, "denomination": "Opening" }
}
```

Modifier l'entrée existante, sans dupliquer son `id`. `enabled: false` désactive une
règle. `customConventions` active ou désactive une convention pour toutes les règles
qui la désignent par `convention` ou `requiresConventions`. `disabledConventions`
permet de définir explicitement les réponses naturelles de remplacement. Une nouvelle
convention s'ajoute en déclarant son nom dans `customConventions` et ses séquences
dans `rules`, sans code exécutable dans le JSON.

Principales conventions : `stayman`, `majorTransfers`, `minorTransfers`,
`fittedTwoNoTrump`, `fittedThreeNoTrump`, `drury`, `truscott`, `splinter`,
`meetingBids`, `sputnik`, `cuebid`, `cappelletti`, `landy`, `rubensohl`,
`chasseCroise`, `goldenMisery`, `staymanTransfers`, `convention2012`,
`majorTwoSuiters`, `minorTwoSuiters`, `weakTwoRelay`, `gamblingRelays`,
`aceResponses`, `blackwood`, `roudi`, `thirdSuitForcing`, `fourthSuitForcing`.
Les ouvertures `weakTwo`, `preempts`, `gambling`, `strongTwoClubs` et
`gameForcingTwoDiamonds` disposent aussi d'interrupteurs.

### Conditions et réponses

- Phases : `Opening`, `Response`, `OpenerRebid`, `ResponderRebid`, `Continuation`, `Intervention` ou `Any`.
- `sideSequence` décrit les appels non passés du camp ; `opponentSequence` ceux de l'adversaire. `M` désigne une majeure, `m` une mineure, `x` une couleur, `NT` les sans-atout et `*` un appel quelconque. `X`/`XX` désignent contre/surcontre.
- `auctionSequence` décrit la chronologie complète ; `ignorePasses: false` permet d'y inclure les `P`. Les séquences sont exactes, sauf `allowAdditionalSideBids` qui autorise une suite au préfixe du camp.
- `lastSideCall`, `lastOpponentCall` et `lastOpponentAction` précisent les dernières actions non passées. Les passes préalables se testent avec `passedHand`, `passedOpening` et `seatPosition`.
- Les `conditions` sont cumulatives. `anyOf` ajoute des alternatives, chacune composée de conditions cumulatives.
- Faits numériques : `h`, `hl`, `hld`, `fitPoints`, `aces`, `keys`, `trumpQueen`, `losers`, `playingTricks`, `defensiveTricks`, `ownKings`, `ownQueens`, `singletons`, `voids`, `doubletons`, compteurs d'appels et paliers.
- Faits logiques (0/1) : `regular`, `balanced`, `singleSuited`, `twoSuited`, `majorFit`, `agreedTrump`, `gameReached`, `vulnerable`, `opponentsVulnerable`, `favorableVulnerability`. `regular` correspond aux distributions régulières de SEF.txt ; `balanced` conserve le test historique plus large.
- Faits par couleur : `length.*`, `honors.*`, `bigHonors.*`, `sideBigHonors.*`, `sideQueens.*`, `topHonors.*`, `stopper.*`, `ace.*`, `king.*`, `queen.*`. Les sélecteurs comprennent les quatre couleurs, `opening`, `partner`, `opponent`, `firstResponse`, `openerRebid`, `candidate`, `longest`, `longestMajor`, `longestMinor`, `otherMajor`, `otherMinor`, `fourthSuit`, `shortest` et `trump`. `shape.5332` teste une distribution indépendamment des couleurs.
- `minimum`/`maximum` définissent les bornes inclusives ; `minimumFact`/`maximumFact` comparent deux faits, par exemple les longueurs de deux couleurs. Un fait indisponible empêche la règle de s'appliquer.
- `candidateSuits` et `suitOrder` permettent de choisir une couleur par longueur et ordre économique : `LongestThenHighest`, `LongestThenLowest`, `Lowest`, `Highest`.
- Une réponse peut être `Bid`, `Pass`, `Double` ou `Redouble`. Son palier peut être fixe, économique (`Cheapest`), à saut (`Jump`), à la manche (`Game`) ou au chelem (`SmallSlam`, `GrandSlam`). `minimumLevel`/`maximumLevel` bornent le résultat.
- Chaque `response` doit déclarer explicitement `forcing`: `NonForcing`, `OneRound`, `SelfForcing` ou `GameForcing`. L'omission ou une valeur inconnue invalide le profil ; aucune règle n'hérite silencieusement d'un statut.
- `OneRound` oblige le partenaire à produire une enchère à la prochaine occasion ; `SelfForcing` oblige l'auteur à reparler à son prochain tour ; `GameForcing` interdit à chaque partenaire de passer jusqu'à ce que leur camp atteigne la manche. Quand aucune règle de continuation n'est applicable, l'IA choisit une enchère légale de secours respectant cette obligation. Si aucune enchère qui satisfait l'obligation n'est légale, Passe redevient possible.
- Passe, Contre et Surcontre sont refusés par le modèle d'enchères tant qu'une obligation active exige une enchère à la couleur ou à SA. Les applications annotent les appels humains avant de les enregistrer et leur signalent ce refus.

Le profil est copié et figé à la construction du moteur. La vulnérabilité est
transmise pour chaque donne par les applications. Les infobulles et la trace
identifient la règle sélectionnée et les conditions configurées. Modifier une zone
ne recalcule pas les autres : les réponses et continuations doivent rester cohérentes.
Les simulations de légalité ne remplacent pas la validation bridge de chaque accord.

### Passages à arbitrer

`SEF.txt` contient notamment « 11-2HL », « convention 2022 » dans la section 2SA
et une phrase tronquée sur le décalage des réponses après 2♣-2♦-2SA. Le catalogue
utilise 11-12 HL, le nom `convention2012` du moteur existant et des seuils explicites
éditables pour les redemandes fortes. Les retours à 3SA proposés après des demandes
au palier de 4 sont illégaux et ne sont jamais joués par le moteur. Ces passages
ne permettent pas de revendiquer une conformité littérale exhaustive sans arbitrage
bridge ; ils doivent être précisés avant de figer un profil de référence définitif.

### Stayman Après Redemande Forte à SA

Après `2♣-2♦-2SA-3♣-3♦`, l'ouvreur a décrit 22-23 HL et nié une majeure quatrième.
Le répondant conclut à `3SA` avec les valeurs de manche : avec 5 H, Nord ne doit
pas passer sur `3♦`, qui est une réponse artificielle au Stayman, pas un contrat
naturel à jouer. La règle `strong-nt.stayman.conclude-no-major` reprend le minimum
de 2 HL de la conclusion directe après cette redemande forte, sans intervention.
Ce seuil reste configurable et ne modifie ni l'ouverture directe de `2SA`, ni
les développements après une réponse majeure au Stayman.

### Texas Mineur : Courte et Contrôles

Après `1SA-3♣-3♦-3♥`, l'atout potentiel est carreau et `3♥` décrit une courte,
pas une nouvelle couleur naturelle. Avec un fit, `4♣` montre un contrôle trèfle
et agrée les carreaux : ce n'est ni un autre Texas, ni un changement d'atout.
Le répondant peut annoncer un contrôle non encore montré (par exemple `4♠` avec
une main positive), sinon conclure à `5♦`. La majeure courte ne doit pas être
répétée comme un contrôle nouveau. Le Blackwood et sa conclusion gardent l'atout
carreau, notamment pour compter le Roi d'atout.

Après un Texas trèfle (`1SA-2♠-3♣`), `4♣` est au contraire un soutien de l'atout
trèfle. Sans fit confirmé, l'ouvreur peut revenir à `3SA` avec un arrêt dans la
majeure courte. Les continuations sont les règles `one-nt.minor-short.*` ; les seuils
de recherche de contrôle/chelem restent configurables. Désactiver `minorTransfers`
désactive également cette reconnaissance et les suites associées.

Les infobulles décrivent l'action survolée, même si l'IA aurait choisi une autre
action. Elles affichent les conditions et le caractère forcing en français, sans
utiliser l'identifiant technique comme libellé. Passe, Contre et Surcontre disposent
des mêmes explications contextuelles ; Passe est signalé comme exclu dans la suite
forcing de manche avant qu'une manche ne soit atteinte. Cette explication du sens
d'une enchère n'affirme pas que la main du joueur en remplit les conditions.

### Chapitre 7 : Arbitrages Validés

Les arbitrages du 18 octobre 2026 sont consignés à la fin du chapitre 7 de
`SEF.txt`. Les règles `opener.*` du catalogue déclaratif les appliquent ; le mode
historique sans profil conserve ses anciens accords. Les entrées historiques
incompatibles de répétition, soutien direct fort et invitation à 3M ont été désactivées
dans le catalogue pour ne pas contourner les nouvelles bornes.

- Après soutien simple, 3M est un barrage, pas une invitation ; essai naturel ou 2SA à 17-19 HLD, manche à 20+ HLD.
- Bicolore économique : non forcing après réponse au palier 1, forcing après 2 sur 1. Bicolore à saut : forcing de manche. Bicolore cher : forcing un tour après une couleur au palier 1, forcing de manche après 1SA ou 2 sur 1.
- Soutiens spécifiques en H prioritaires sur les équivalences indicatives HLD ; super-forcing généralisé et faux bicolore puis soutien dans la zone forte non couverte.
- Splinters sur 1♣-1♦ à 18 H et plus, sans plafond ; soutien différé corrigé en 1♠-2♥-2♠-2SA-3♥.
- Main semi-régulière : 5422 ou 6322, soit deux doubletons, pas deux singletons. Mini-maxi 1♦-2♣-2SA forcing un tour ; 2SA à saut 18-19 HL.
- Répétition simple après 2 sur 1 forcing un tour ; saut prioritaire à 17 HL avec six cartes. Bicolore 5-5 à saut : 75 % des H dans les deux couleurs.
- En compétition, les faits `forcedRebid`/`freeRebid` tiennent compte de l'ordre des appels non passés. Le Sputnik est traité comme une réponse de l'autre majeure seulement lorsque cette majeure est identifiée par l'intervention adverse ; les seuils sont ajustés de 2 ou 4 points selon le palier.

Les nouveaux faits comprennent `responseFitPoints` (fit dans la couleur du répondant),
`agreedFitPoints` (fit dans la couleur d'ouverture), `adjustedH`, `adjustedHL`,
`adjustedResponseFitPoints`, `semiRegular`, `oneLevelMajorAvailable`, `candidateReverse`,
`candidateEconomic`, `concentratedHonorsPercent`, `repeatNeedsSix`, `trialSpace`,
`shortSuitStoppers`, `force.*` et `control.*`. Ces valeurs utilisent l'évaluation du profil.
Les modes `DoubleJump`, `Support` et `SupportJump` expriment les doubles sauts et les
soutiens après contre sans confondre annonce de la majeure et soutien.

Les conventions `openerTrialBids`, `openerTrialDouble`, `openerSuperForcing` et
`openerFitJump` peuvent être désactivées dans `customConventions`. Les seuils restent
éditables dans les règles, y compris ceux des essais et des redemandes compétitives.

### Mode historique

Le fichier `bidding-profile.json` et la construction de `new SefBiddingEngine()`
sans profil injecté conservent le comportement antérieur. Ce mode configure :

- `oneNoTrump` : zone d'ouverture de 1SA en **H** et autorisation des majeures cinquièmes.
- `twoNoTrump` : zone d'ouverture de 2SA en **HL**, conformément au calcul historique du moteur, et autorisation des majeures cinquièmes.
- `conventions.stayman` : activation sur les ouvertures directes de 1SA et 2SA, avec seuils distincts `staymanMinimumHL` et `twoNoTrumpStaymanMinimumHL`.
- `conventions.majorTransfers` / `minorTransfers` : activation des Texas sur ces ouvertures, avec leurs rectifications et continuations.
- `responses` : seuils HL des réponses quantitatives à 1SA. Dans l'ordre croissant : invitation à `2SA`, manche à `3SA`, invitation au petit chelem à `4SA`, petit chelem à `6SA`, invitation au grand chelem à `5SA`, grand chelem à `7SA`. `maximumHL` borne la dernière zone et conserve par défaut la limite historique de 25 HL.

Le profil fourni reproduit le comportement antérieur. Un JSON partiel complète
les propriétés manquantes par les valeurs standard ; il n'y a pas d'héritage entre
fichiers. `schemaVersion` vaut 1. Les propriétés inconnues, sections nulles,
zones inversées ou chevauchantes sont refusées. La zone de 1SA commence au moins
à 10 H ; celle de 2SA commence au moins à 18 HL et reste sous les 22 HL de 2♣.
Ces bornes évitent de reconfigurer implicitement les ouvertures fortes.

Changer la zone d'ouverture ne recalcule pas automatiquement les zones de réponse :
adapter également `responses` et les seuils Stayman selon les accords du camp.
L'ouvreur accepte les invitations à SA avec le maximum de sa zone configurée.
Les infobulles des ouvertures et des demandes Stayman utilisent les seuils actifs ;
le CSV reste un catalogue de textes, pas un moteur de règles.

Lorsque les Texas sont désactivés, les réponses de remplacement sont naturelles :
sur 1SA, une majeure cinquième faible se nomme à `2M` (à `3♠` si `2♠` reste réservé
au Texas mineur), et une mineure sixième faible à `3m`. Sur 2SA, une majeure
cinquième se nomme à `4M` ; une mineure sixième de 16+ HL à `5m`.
L'ouvreur ne rectifie plus les anciennes enchères conventionnelles et passe sur
ces conclusions naturelles. Sans Stayman, les mains sans autre convention adaptée
utilisent les réponses à SA disponibles.

Dans ce mode historique uniquement, les réponses et continuations des ouvertures fortes `2♣`/`2♦`, les interventions,
Rubensohl, Roudi et Blackwood gardent leurs règles actuelles. La configuration
reste limitée aux paramètres nommés ci-dessus. Pour configurer les autres familles,
utiliser le catalogue déclaratif avec `useStandardFallback: false`.

## Évaluation

- Points H : As 4, Roi 3, Dame 2, Valet 1.
- Points L : un point par carte à partir de la cinquième.
- Points D : chicane 3, singleton 2, doubleton 1.
- Revalorisation après fit : +2 points pour le 9e atout du camp, +1 par atout supplémentaire.
- Les décisions sans fit utilisent H ou HL; les soutiens utilisent HLD revalorisés.
- Par défaut, 2SA exclut une majeure cinquième. À 1SA, le moteur autorise certaines distributions régulières avec majeure cinquième, comme le décrivent les exceptions ci-dessous.

## Ouvertures

| Ouverture | Signification |
|---|---|
| `1♣` / `1♦` | Meilleure mineure, 12 H ou 11 H et 13 HL |
| `1♥` / `1♠` | Majeure au moins cinquième, 13-21 HL, hors exceptions d'ouverture à SA |
| `1SA` | Main régulière, 15-17 H |
| `2♣` | Fort indéterminé, forcing un tour : 22-23 H réguliers, ou très belle couleur au moins cinquième de 20-23 HL |
| `2♦` | Forcing de manche : 24+ H(L), ou unicolore à neuf levas majeures / dix mineures |
| `2♥` / `2♠` | Deux faible : six cartes exactement, belle couleur, 5-10 H, au plus un gros honneur annexe |
| `2SA` | Main régulière, 20-21 HL |
| `3SA` | Gambling : mineure maîtresse de sept cartes (ARD), rien d'autre qu'au plus une dame annexe |
| Palier de 3/4 | Barrage : sept cartes et 4-10 H au palier 3, huit cartes et 5-10 H au palier 4 |

Les barrages exigent une belle couleur et dénient une majeure annexe quatrième
ainsi que deux gros honneurs annexes.

À égalité entre deux majeures cinquièmes, le moteur ouvre `1♠`. Entre mineures égales, il ouvre `1♦` avec deux mineures quatrièmes et `1♣` avec deux mineures troisièmes.

## Développements après un barrage

Sur un 2 faible :

- Soutiens = barrage (loi des atouts) : `3M` avec trois atouts, `4M` avec quatre (l'ouvreur passe).
- `2SA` : relais forcing, espoir de manche (15+ HLD). Redemandes de l'ouvreur : minimum → répétition de la couleur ; maximum (8-10 H) → gros honneur annexe au palier 3, sinon `3SA` (couleur pleine). Le répondant conclut à la manche avec un fit, sinon `3SA`.
- Changement de couleur forcing : cinq cartes en majeure, six en mineure, 13+ HL. L'ouvreur soutient avec trois cartes ou répète sa couleur.
- `3SA` : conclusion.

Sur un barrage au palier 3 en majeure : le fit troisième prolonge à la manche (loi des atouts). `4SA` direct sur un barrage est un Blackwood dans la couleur d'ouverture.

Après intervention adverse sur un barrage, le contre est punitif. Sur un 2 faible,
`2SA` reste un relais avec espoir de manche; un changement de couleur est forcing.

Sur `3SA` gambling, `4♣`/`5♣` demandent à l'ouvreur de passer avec les trèfles ou
de corriger à carreau. `4♦` demande de nommer une courte; l'ouvreur répond `4SA`
sans singleton ni chicane, `5♣` avec une courte mineure, ou `4♥`/`4♠` avec une
courte majeure. L'enchère de demande `4♦` n'a pas de seuil d'honneur supplémentaire.

Après `2♣ - 2♦`, redemander `2♥`/`2♠` avec une majeure cinquième et 21-23 HL,
ou une majeure sixième avec 8-8,5 levées de jeu et deux levées défensives. Une
majeure septième se redemande au palier de 3 avec 8-8,5 levées et deux levées
défensives ; une mineure septième demande 8,5-9,5 levées. `2SA` décrit 22-23 HL
semi-réguliers lorsqu'aucune description de majeure ne s'applique. Pour les
seuils fractionnaires, le profil utilise les perdantes, avec un bonus de 0,5 si
la main contient au moins trois As, trois 10, ou deux 10 et trois 9.

## Redemandes de l'ouvreur

Après un changement de couleur 2 sur 1 (`1♠ - 2♥`, cinq cœurs promis) :

| Main de l'ouvreur | Redemande |
|---|---|
| Fit troisième, 15+ HLD | `3♥` (soutien direct, non minimum) |
| Fit quatrième, 17-19 HLD | `4♥` (soutien à saut) |
| 12-14 HLD | Répétition `2♠`, soutien différé au tour suivant |
| Régulier 15-17 H | `2SA` |
| Régulier 18-19 H | `2SA` avec saut (`3SA` sur réponse au palier de 2) |

Autres redemandes : bicolore à la couleur la plus économique, répétition avec six cartes (avec saut à 17+ HL), répétition de la majeure cinquième faute de mieux.

Redemandes naturelles détaillées :

- Après une réponse au palier de 1, le changement de couleur au palier de 1 est non forcing (12-19 HL) ; le bicolore économique au palier de 2 est forcing et illimité.
- Le bicolore cher est forcing : 18-23 HL après une réponse au palier de 1, 17+ HL après une réponse au palier de 2, et 19-23 HL après une réponse à `1SA`.
- Le bicolore à saut est forcing de manche : 20-23 HL après une réponse au palier de 1, 17-23 HL après une réponse au palier de 2, avec un bicolore 5-5.
- Après une réponse au palier de 1, la répétition simple montre 13-17 HL ; elle promet six cartes seulement après `1SA` ou lorsqu'elle est collée à la couleur d'ouverture. Après une réponse au palier de 2, elle est forcing et promet au moins cinq cartes.
- La répétition à saut promet six cartes et au moins 17 HL.

Après un soutien mineur simple (`1♣ - 2♣`, cinq atouts 5-10 HLD) :

- Avec moins de 17 HL, l'ouvreur passe.
- Avec 17+ HL, une enchère sous `3SA` annonce une force et dirige vers ce contrat ; au-dessus de `3SA`, l'enchère est un contrôle en vue du chelem.
- Le soutien d'une majeure répondue au palier de 1 promet quatre cartes : `2M` avec 12-16 HLD, `3M` avec 17-19 HLD et un jeu irrégulier.

L'ouvreur d'un barrage (2 faible inclus, hors relais `2SA`) ne reparle jamais après un soutien.

## Deuxième enchère du répondant

- La répétition de la couleur du répondant promet six cartes.
- Après `1m - 1M - 2m`, la troisième couleur au palier de 2 est forcing pour un tour à partir de 11 HL et ne promet pas de longueur.
- Après `1m - 1M - 1SA`, une majeure cinquième avec moins de 11 HL utilise le ping-pong dans l'autre majeure ; à partir de 11 HL, `2♣` est Roudi.
- Après une redemande différente de l'ouvreur, la répétition de la couleur du répondant montre une sixième et se fait au palier le plus économique.
- Après `1M - 2x - 2M`, le soutien différé de la majeure d'ouverture au palier de 3 montre au moins trois cartes et une ambition de chelem.
- Le splinter après une ouverture majeure montre un fit quatrième, 13+ HLD et un singleton dans la couleur annoncée par saut.

### Soutiens majeurs et mineurs

- Après `1m - 1M - 2M`, le répondant conclut à `4M` avec 13-14 HLD. Avec 9-12 HLD, il essaie la manche : `3m` si l'ouverture mineure a au moins trois cartes, sinon une couleur latérale faible à compléter au palier de 3, sinon `2SA` comme essai généralisé. À partir de 15 HLD, `2SA` est un essai de chelem.
- Après `1m - 1M - 3M`, une nouvelle couleur montre un contrôle, en commençant par le moins cher ; le contrôle peut être montré avec n'importe quelle force. Après `1m - 1x - 3m`, une couleur sous `3SA` montre un arrêt, dans l'ordre économique ; `3SA` est proposé si tous les arrêts nécessaires sont présents.

### Redemande à 1SA

- Une nouvelle couleur économique au palier de 2 est naturelle et non forcing ; la répétition de la couleur de réponse n'exige que cinq cartes.
- `2♦` montre `5♦` et cinq cartes dans la majeure répondue après `1♣ - 1M - 1SA`, ou `4♦` et quatre cartes dans cette majeure après `1♦ - 1M - 1SA`. Après `1♥ - 1♠ - 1SA`, `2♥` montre cinq piques et quatre cœurs.
- Avec cinq cartes dans la majeure répondue et au moins 11 HL, `2♣` est Roudi ; cette convention est prioritaire sur les développements naturels génériques.
- Les SA sont quantitatifs, dénient cinq cartes dans la majeure répondue et demandent l'arrêt dans la dernière couleur : `2SA` avec 11-12 HL, `3SA` avec 13-18 HL. Une majeure cinquième sous 11 HL se décrit par ping-pong.
- Après `1♣/1♦ - 1♠ - 2SA`, `3♥` montre un 5-5 majeur et une ambition de chelem ; `4♥` montre le même bicolore, limité à la manche, et laisse l'ouvreur passer ou corriger à `4♠`. Après `1m - 1M - 2SA`, l'autre mineure au palier de 3 est toujours le relais forcing de manche ; l'ouvreur renseigne d'abord sur les cœurs, puis les piques, puis `3SA`.

### Redemandes à la couleur

- Avec 3-4 cartes dans la seconde couleur de l'ouvreur et 5-6 HL, le répondant passe.
- Soutien de la mineure d'ouverture : `2m` avec 6-10 H ; `3m` à partir de 12 H avec une main irrégulière, sans majeure de réponse cinquième.
- Soutien de la majeure d'ouverture : `2M` avec 6-10 HLD, `3M` avec 11-12 HLD, `4M` avec 13-15 HLD. Les mêmes zones s'appliquent au soutien de la seconde majeure de l'ouvreur. Un splinter montre quatre atouts, 16-19 HLD et une courte.
- Répéter sa couleur : `2M` avec six cartes et 6-10 HL ; `3M` avec six cartes et 11-12 HL. Une nouvelle couleur sans saut est forcing un tour à partir de 10 HL ; la quatrième couleur est forcing un tour à partir de 11 H.
- Les SA demandent l'arrêt dans la dernière couleur : `1SA` avec 6-10 HL, `2SA` avec 11-12 HL, `3SA` avec 12 H ou 13-15 HL.

### Forcing et séquences spéciales

- Après une réponse 2 sur 1, toutes les enchères sont forcing de manche, sauf la répétition à 3 de la couleur répondue avec 11-12 HL, qui est non forcing. La quatrième couleur reste forcing de manche.
- Après un bicolore cher, `2SA` modérateur avec 8 HL ou moins est forcing un tour. `3SA` avec 10-11 H et l'arrêt dans la dernière couleur, les répétitions et les soutiens suivent les règles naturelles du chapitre 8.
- Après un bicolore à saut, `2SA` est naturel sans force minimale ; `3SA` avec 10-11 HL exige les arrêts. La répétition promet cinq cartes ; la quatrième couleur reste forcing un tour par défaut.

Le `2SA fitté` est une convention distincte : après `1♥` ou `1♠`, il montre exactement trois atouts et 11-12 points de soutien. L'ouvreur choisit `3M` avec une main minimale ou `4M` avec une main plus forte.

Après un soutien majeur :

- Face à `2M`, l'ouvreur conclut avec 20+ HLD ; entre 17 et 19 HLD, il fait un essai naturel à la couleur, ou un essai généralisé à `2SA` s'il n'a pas de couleur d'essai.
- Face à `3M`, l'ouvreur passe ou conclut à `4M` ; toute autre enchère est un contrôle montrant une ambition de chelem.
- Face à `2SA` fitté, `3M`/`4M` conclut ; une mineure est naturelle avec espoir de chelem ; l'autre majeure cherche un fit 4-4 forcing ; `3SA` propose de jouer 3SA.
- Face à `3SA` fitté, l'ouvreur conclut à `4M` avec une main sans ambition de chelem, sinon il annonce son premier contrôle.

## Réponses naturelles

- Une réponse à la couleur promet quatre cartes : au palier de 1 dès 6 HL ; au palier de 2 sans saut avec 11 H ou 12 HL. Exception : `1♠ - 2♥` promet cinq cœurs.
- À longueur égale, la couleur cinquième est annoncée avant la quatrième ; parmi les quatrièmes, la couleur la plus économique est choisie ; parmi les cinquièmes, la plus chère est choisie.
- Un changement de couleur à saut promet six cartes et 18 HL. Un double ou triple saut est un barrage inférieur à une ouverture.
- Après une ouverture mineure, la couleur la plus longue est annoncée; à longueur égale, la plus économique parmi les quatrièmes et la plus chère parmi les cinquièmes.
- En zone 6-12 HL, la majeure quatrième est prioritaire sur une mineure même plus longue; il faut 13 HL pour annoncer une mineure avant une majeure.
- Après une ouverture mineure, le soutien promet cinq cartes : `2m` avec 6-9 H, `3m` avec 10-11 H ; à partir de 12 H, commencer par un changement de couleur.
- Après une ouverture majeure, le soutien promet au moins trois cartes : `2M` avec 6-10 HLD ; `2SA fitté` avec trois atouts et 11-12 HLD ; `3M` avec quatre atouts et 11-12 HLD ; `3SA fitté` avec quatre atouts réguliers et 13-15 HLD ; splinter avec singleton ; changement de couleur à partir de 16 HLD.
- Les changements de couleur au niveau de 2 nécessitent 13 HL; `2♥` sur `1♠` garantit cinq cœurs.
- Les réponses à SA et les soutiens de barrage utilisent les zones SEF usuelles.

## Développements après ouverture mineure

Redemandes de l'ouvreur sur une réponse de `1♥`/`1♠` :

| Situation | Redemande |
|---|---|
| Fit quatrième, 12-16 HLD | Soutien simple `2M` |
| Fit quatrième, 17-19 HLD | `3M` |
| Fit quatrième, 20-23 HLD | `4M` |
| Sans fit, majeure quatrième nommable au palier de 1 | `1M`, 12-19 HL, non forcing |
| Régulier 12-14 H sans majeure nommable | `1SA` |
| Régulier 18-19 H sans majeure nommable | `2SA` |

Deuxième enchère du répondant :

- Sur soutien `2M` : passe avec 6-10 HLD, `3M` (enchère d'essai) avec 11-12 HLD, `4M` avec 13-15 HLD.
- Sur soutien `3M` : passe avec 6-8 HLD, `4M` avec 9-14 HLD.
- Sur la majeure de l'ouvreur au palier de 1, avec quatre atouts : `2M` 6-10 HLD, `3M` 11-12 HLD, `4M` 13-15 HLD.

## Développements après ouverture majeure

Soutien direct du répondant fitté (trois atouts et plus, HLD revalorisés) :

| Zone | Soutien |
|---|---|
| 6-10 HLD | `2M` |
| 11-12 HLD | `3M` |
| 13-15 HLD | `4M` |
| 16+ HLD et 13+ HL | Changement de couleur puis soutien (zone chelem) |

Redemande de l'ouvreur après soutien (HLD revalorisés) :

| Soutien | Redemande de l'ouvreur | Suite du répondant |
|---|---|---|
| `2M` | Passe 14-16, `3M` essai 17-19, `4M` 20+ | Sur `3M` : passe 6-8, `4M` 9-10 |
| `3M` | Passe ≤14, `4M` 15+, `5M` à partir de 21 comme essai de chelem | Passe ou contrôle |
| `4M` | Passe 14-17, `5M` essai de chelem 18+ | Sur `5M` : passe ou contrôle |

## Stayman

Après `1SA`, `2♣` est prioritaire à partir de 8 HL avec une majeure quatrième, même avec une majeure cinquième ou une mineure sixième :

| Réponse | Distribution de l'ouvreur |
|---|---|
| `2♦` | Aucune majeure quatrième |
| `2♥` | Quatre cœurs |
| `2♠` | Quatre piques |
| `2SA` | Les deux majeures quatrièmes |

Après `2SA`, la même structure est décalée d'un palier : `3♣` demande, puis réponses `3♦`, `3♥`, `3♠` ou `3SA`.

Après `1SA - 2♣ - 2SA`, `3♣` et `3♦` sont respectivement des transferts vers les cœurs et les piques. `3♥`/`3♠` montrent un fit et une ambition de chelem. Après `2♥` ou `2♠`, un saut à `4♣`/`4♦` (et `4♥` sur `2♠`) est un splinter; l'autre majeure au palier de 3 est la convention 2012.

Le moteur conclut ensuite à la manche, propose la manche ou joue à SA selon le fit et la force du répondant.

## Réponses à l'ouverture de 1SA

L'ouverture de `1SA` exige 15-17 H, ni chicane ni singleton, pas deux doubletons, pas de majeure cinquième.

Exceptions historiques conservées dans le profil standard : le moteur accepte
une majeure cinquième avec un seul doubleton ; certaines distributions mineures
5422 ou 6322 sont aussi admises. Ces formes non standard ne s'ouvrent pas à 1SA
avec le maximum de la zone (17 H par défaut). L'option `allowFiveCardMajor: false`
interdit les exceptions à majeure cinquième sans supprimer les exceptions mineures.

Réponses du répondant :

| Main | Réponse |
|---|---|
| 0-7 HL sans couleur longue | Passe |
| 8-9 HL sans majeure quatrième | `2SA` |
| 10-15 HL | `3SA` |
| 16-17 HL | `4SA` quantitatif |
| 18-19 HL | `6SA` |
| 20-21 HL | `5SA` |
| 22-25 HL | `7SA` |
| Majeure quatrième, 8+ HL | Stayman `2♣` |
| Majeure cinquième | Texas dès 0 point |
| Mineure sixième, 0-9 HL | Texas mineur (`2♠` → trèfles, `3♣` → carreaux) |
| Mineure cinquième avec courte ou ambition de chelem | Texas mineur |
| Saut à `3♦`, `3♥` ou `3♠` | Unicolore sixième avec espoir de chelem |
| Bicolore majeur 5-4, 0-7 H | Texas sur la couleur cinquième |
| Bicolore majeur 5-4, 8+ H | Stayman |
| Bicolore majeur 5-5, 0-5 H | Texas `♥` puis passe |
| Bicolore majeur 5-5, 6-9 H | `4♦` : l'ouvreur choisit la manche majeure |
| Bicolore majeur 5-5, 10+ H | Texas `♠` puis annonce des cœurs (`4♥`, ou `3♥` en zone de chelem) |

Développements après Stayman :

- Réponse `2♦` avec bicolore 5-4 : majeure cinquième au palier de 2 (8-9 H, non forcing, l'ouvreur passe fitté ou revient à `2SA`) ; **chassé-croisé** avec 10+ H : saut à 3 dans la majeure quatrième, l'ouvreur rectifie à la manche avec trois cartes dans l'autre majeure ou revient à `3SA`.
- Réponse `2M` fittée : soutien `3M` 8-9 H (l'ouvreur conclut avec 17 H), manche directe 10-15 HLD.
- Sans fit : `2SA` 8-9 HL, `3SA` 10-15 HL, puis les zones quantitatives ci-dessus.

## Texas

Après `1SA` :

| Réponse | Transfert vers |
|---|---|
| `2♦` | Cœurs, cinq cartes ou plus |
| `2♥` | Piques, cinq cartes ou plus |
| `2♠` | Trèfles, six cartes ou plus |
| `3♣` | Carreaux, six cartes ou plus |

Après `2SA`, les transferts deviennent `3♦`, `3♥`, `3♠` et `4♣`. Les Texas majeurs sur `2SA` sont rectifiés seulement avec trois cartes; sinon l'ouvreur annonce `3SA`.

Après rectification d'un Texas majeur : passe jusqu'à 8 HL, annonces quantitatives selon les zones de réponse directe, répétition à partir de six cartes, splinter avec six cartes et courte en zone 15-17 HLD. Après rectification d'un Texas mineur : `3♥`/`3♠` décrivent une courte, `3SA` une courte dans l'autre mineure sans ambition de chelem, `4SA` une ambition de chelem.

## Intervention sur 1SA

Après une intervention adverse, le contre est d'appel à partir de 8 H lorsqu'aucune description naturelle n'est disponible; le surcontre après un contre est punitif à partir de 10 H. Une couleur au palier de 2 est naturelle et non forcing avec six cartes. Sur `2♣` Landy, `2♥` ou `2♠` annonce la majeure la plus longue quand les deux majeures sont présentes. `2SA` est un transfert Rubensohl, les couleurs à partir de `3♣` sont des transferts successifs et `3♠` demande un arrêt pour jouer `3SA`.

Après un contre du Stayman : surcontre pour jouer, `2♦` avec cinq carreaux, `2♥`/`2♠` avec l'autre majeure, `2SA` avec une main maximale sans majeure, `3♣` avec les deux majeures; passe sinon. Après un contre du Texas : surcontre fitté pour jouer, rectification fittée, passe sans fit.

## Ouvertures fortes

`2SA` promet 20-21 HL réguliers sans majeure cinquième. Réponses : Stayman `3♣` dès 4 HL, Texas majeur `3♦/3♥`, Texas mineur `3♠/4♣` avec espoir de chelem, `4♦` pour un bicolore majeur 5-5 (l'ouvreur choisit), `4♥/4♠` conclusifs avec six cartes et `3SA` naturel sans fit majeur, `4SA` quantitatif à 12 HL, `5SA` à 16 HL et `6SA` à 13-15 HL. La rectification du Texas majeur promet un fit; sans fit, l'ouvreur revient à `3SA`.

Après `1SA - 2♣ - 2SA`, `3♣/3♦` sont des transferts propositionnels : rectification à 3 ou 4 avec fit, retour à `3SA` sans fit. `4♣/4♦` sont les transferts forcing de manche vers les cœurs et les piques; sans fit, l'ouvreur joue `3SA`.

`2♣` est auto-forcing, avec relais obligatoire à `2♦`. À partir de 24 HL ou à une levée de la manche avec deux levées défensives, ouvrir `2♦` forcing de manche. Après le relais `2♦`, `2♥/2♠` décrivent une couleur cinquième ou sixième, `2SA` une main semi-régulière de 22-23 HL, `3M` une majeure septième, et `3SA/4♣/4♦` les bicolores majeurs à trois perdantes. Les réponses distinguent relais forcing, couleurs positives, soutiens d'espoir de chelem et splinters.

Sur `2♦`, les réponses décrivent les As : `2♥` sans As (0-7 H ou 8+ H avec une courte), `2SA` sans As et régulier, `2♠` avec un As majeur, `3♣/3♦` avec l'As correspondant, `3♥/3♠/3SA` pour les groupes de deux As. Les redemandes sont naturelles; `2SA` est la redemande par défaut et les séquences restent forcing jusqu'à la manche.

## Roudi

Après `1m - 1M - 1SA`, le répondant annonce automatiquement `2♣` avec une majeure
cinquième et au moins 11 HL. L'ouvreur précise alors :

- `2♦` : deux cartes dans la majeure du répondant;
- `2M` : soutien troisième et ouverture minimum;
- `2` dans l'autre majeure : soutien troisième et ouverture maximum.

Le répondant conclut ensuite à la manche avec au moins 12 H, ou fait une proposition
avec 11 H, à la majeure si elle est fittée et à SA sinon.

## Quatrième couleur forcing

Quand les trois premières annonces naturelles du camp ont nommé trois couleurs
différentes, le répondant possédant au moins 11 HL annonce automatiquement la
quatrième couleur au palier le plus économique. L'ouvreur indique en priorité :

1. un soutien troisième dans la première couleur du répondant;
2. quatre cartes dans la quatrième couleur;
3. une main régulière à SA;
4. une sixième carte dans sa couleur d'ouverture;
5. à défaut, sa deuxième couleur.

Le répondant conclut ensuite à la manche majeure ou à `3SA`.

## Blackwood 5 clés 30-41

Après agrément d'un atout et avec un potentiel de chelem, `4SA` interroge sur les
quatre As et le Roi d'atout :

| Réponse | Clés et Dame d'atout |
|---|---|
| `5♣` | 0 ou 3 clés |
| `5♦` | 1 ou 4 clés |
| `5♥` | 2 ou 5 clés sans la Dame d'atout |
| `5♠` | 2 ou 5 clés avec la Dame d'atout |

Une chicane utile est annoncée à `5SA` avec 2 ou 4 clés, ou au palier de 6 avec
1 ou 3 clés. Le poseur conclut au petit chelem lorsque le compte permet au moins
quatre clés dans la ligne, sinon il revient à l'atout au plus bas. Le grand
chelem nécessite encore une séquence dédiée de contrôles ; Blackwood ne le
conclut pas automatiquement.

## Enchères compétitives

- Intervention naturelle avec une couleur cinquième et 9-18 HL.
- Intervention au palier de 2 avec six cartes robustes, ou cinq cartes solides avec une valeur d'ouverture.
- Intervention à `1SA` avec 16-18 H réguliers et une garde dans la couleur adverse ; avec deux majeures quatrièmes, préférer le contre.
- `2♣` Landy sur `1SA` avec les deux majeures au moins quatrièmes.
- Contre d'appel avec courte dans la couleur adverse et soutien des autres couleurs, ou toute distribution à partir de 19 HL.
- Contre, surcontre et légalité par camp sont gérés par le modèle d'enchères.

Réveils : après une ouverture adverse et deux passes, une couleur cinquième est
annonçable dès 7 HL ; le contre de réveil est possible dès 10 points. Ces seuils
sont allégés par rapport à une intervention directe.

Réponses compétitives :

Après une ouverture mineure et une intervention à la couleur :

- Changement de couleur au palier de 1 : au moins quatre cartes et 7 HL, forcing.
- Changement de couleur au palier de 2 : au moins cinq cartes et 11 HL, auto-forcing.
- Convention « collante » : la couleur immédiatement au-dessus de l'intervention promet cinq cartes ; avec quatre cartes dans cette couleur, le répondant contre.
- Après `1♥`, `1♠` est donc une réponse collante à cinq cartes ; l'ouvreur peut soutenir avec trois piques.
- Changement de couleur à saut : barrage faible avec au moins sept cartes.
- Sans-atout : `1SA` 8-10 HL, `2SA` 11-12 HL, `3SA` 13-15 HL, avec une main régulière.
- Soutien de la mineure d'ouverture, avec cinq cartes : `2m` 6-8 H, `3m` 9-11 H.
- Contre à partir de 8 HL : sur `1♦`, sans majeure quatrième ; sur `1♥`, sans quatre piques ; sur `1♠`, avec quatre coeurs (Sputnik simple).
- Cue-bid de la couleur adverse : forcing de manche à partir de 13 HL, sans majeure quatrième non nommée.

- Après un contre d'appel, le partenaire donne priorité à une majeure quatrième ; la couleur est annoncée au palier de 1 avec 0-8 H et au palier de 2 avec 9-10 H.
- Après un Landy (`1SA - 2♣`), le partenaire annonce sa majeure cinquième au palier de 2 ; avec les deux majeures quatrièmes, `2♦` demande le choix du partenaire.
- Après une intervention naturelle du partenaire, un soutien d'au moins trois cartes est prioritaire ; sans fit, une majeure quatrième est annoncée au palier légal le plus économique.

## Vulnérabilité et score

La vulnérabilité suit le cycle officiel de 16 donnes. Le score tient compte des primes de manche et de chelem, des surlevées, des chutes, du contre et du surcontre pour un déclarant vulnérable ou non vulnérable.

## Périmètre avancé

Le moteur automatise les séquences usuelles, le Roudi, la quatrième couleur
forcing et le Blackwood 5 clés. Les contrôles détaillés, la demande ultérieure à
la Dame d'atout, le Blackwood aux Rois, les Michaël précisés et les développements
après intervention complexe ne sont pas encore automatisés.

Référence fonctionnelle : https://fr.wikipedia.org/wiki/Majeure_cinqui%C3%A8me
