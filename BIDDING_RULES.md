# Enchères SEF

Le moteur `SefBiddingEngine` applique le socle du Système d'Enseignement Français décrit dans la référence Majeure cinquième.

## Évaluation

- Points H : As 4, Roi 3, Dame 2, Valet 1.
- Points L : un point par carte à partir de la cinquième.
- Points D : chicane 3, singleton 2, doubleton 1.
- Revalorisation après fit : +2 points pour le 9e atout du camp, +1 par atout supplémentaire.
- Les décisions sans fit utilisent H ou HL; les soutiens utilisent HLD revalorisés.
- Une ouverture à SA exclut une majeure cinquième.

## Ouvertures

| Ouverture | Signification |
|---|---|
| `1♣` / `1♦` | Meilleure mineure, 12 H ou 11 H et 13 HL |
| `1♥` / `1♠` | Majeure au moins cinquième, 13-21 HL, prioritaire sur SA et mineures |
| `1SA` | Main régulière, 15-17 H |
| `2♣` | Fort indéterminé, forcing un tour : 22-23 H réguliers, ou très belle couleur au moins cinquième de 20-23 HL |
| `2♦` | Forcing de manche : 24+ H(L), ou unicolore à neuf levas majeures / dix mineures |
| `2♥` / `2♠` | Deux faible : six cartes exactement, belle couleur, 5-10 H, au plus un gros honneur annexe |
| `2SA` | Main régulière, 20-21 H |
| `3SA` | Gambling : mineure maîtresse de sept cartes (ARD), rien d'autre qu'au plus une dame annexe |
| Palier de 3/4 | Barrage : sept cartes au palier 3, huit cartes au palier 4, 5-10 H |

À égalité entre deux majeures cinquièmes, le moteur ouvre `1♠`. Entre mineures égales, il ouvre `1♦` avec deux mineures quatrièmes et `1♣` avec deux mineures troisièmes.

## Développements après un barrage

Sur un 2 faible :

- Soutiens = barrage (loi des atouts) : `3M` avec trois atouts, `4M` avec quatre (l'ouvreur passe).
- `2SA` : relais forcing, espoir de manche (15+ HLD). Redemandes de l'ouvreur : minimum → répétition de la couleur ; maximum (8-10 H) → gros honneur annexe au palier 3, sinon `3SA` (couleur pleine). Le répondant conclut à la manche avec un fit, sinon `3SA`.
- Changement de couleur forcing : cinq cartes en majeure, six en mineure, 13+ HL. L'ouvreur soutient avec trois cartes ou répète sa couleur.
- `3SA` : conclusion.

Sur un barrage au palier 3 en majeure : le fit troisième prolonge à la manche (loi des atouts). `4SA` direct sur un barrage est un Blackwood dans la couleur d'ouverture.

Sur `3SA` gambling : passe du répondant (les corrections `4♣`/`4♦` ne sont pas encore couvertes).

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

Le `2SA fitté` est une convention distincte : après `1♥` ou `1♠`, il montre exactement trois atouts et 11-12 points de soutien. L'ouvreur choisit `3M` avec une main minimale ou `4M` avec une main plus forte.

Après un soutien majeur :

- Face à `2M`, l'ouvreur conclut avec 20+ HLD ; entre 17 et 19 HLD, il fait un essai naturel à la couleur, ou un essai généralisé à `2SA` s'il n'a pas de couleur d'essai.
- Face à `3M`, l'ouvreur passe ou conclut à `4M` ; toute autre enchère est un contrôle montrant une ambition de chelem.
- Face à `2SA` fitté, `3M`/`4M` conclut ; une mineure est naturelle avec espoir de chelem ; l'autre majeure cherche un fit 4-4 forcing ; `3SA` propose de jouer 3SA.
- Face à `3SA` fitté, l'ouvreur conclut à `4M` avec une main sans ambition de chelem, sinon il annonce son premier contrôle.

## Réponses naturelles

- Une réponse à la couleur commence à 6 HL/HLD.
- Après une ouverture mineure, la couleur la plus longue est annoncée; à longueur égale, la plus économique parmi les quatrièmes et la plus chère parmi les cinquièmes.
- En zone 6-12 HL, la majeure quatrième est prioritaire sur une mineure même plus longue; il faut 13 HL pour annoncer une mineure avant une majeure.
- Après une ouverture majeure, un fit de trois cartes permet le soutien direct jusqu'à 12 HLD.
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

Après `1SA`, `2♣` est un Stayman à partir de 8 H avec une majeure quatrième :

| Réponse | Distribution de l'ouvreur |
|---|---|
| `2♦` | Aucune majeure quatrième |
| `2♥` | Quatre cœurs |
| `2♠` | Quatre piques |
| `2SA` | Les deux majeures quatrièmes |

Après `2SA`, la même structure est décalée d'un palier : `3♣` demande, puis réponses `3♦`, `3♥`, `3♠` ou `3SA`.

Le moteur conclut ensuite à la manche, propose la manche ou joue à SA selon le fit et la force du répondant.

## Réponses à l'ouverture de 1SA

L'ouverture de `1SA` exige 15-17 H, ni chicane ni singleton, pas deux doubletons, pas de majeure cinquième.

Réponses du répondant :

| Main | Réponse |
|---|---|
| 0-7 H régulier | Passe |
| 8 H sans majeure quatrième | Règle du 8 : `2SA` si plus de huit cartes ≥ 8, sinon passe |
| 9 H | `2SA` |
| 10-15 H | `3SA` |
| 16-17 H | `4SA` quantitatif, l'ouvreur conclut `6SA` avec 17 H |
| 18+ H | `6SA` |
| Majeure quatrième, 8+ H | Stayman `2♣` |
| Majeure cinquième | Texas dès 0 point |
| Mineure sixième, 0-9 H | Texas mineur (`2♠` → trèfles, `3♣` → carreaux) |
| Bicolore majeur 5-4, 0-7 H | Texas sur la couleur cinquième |
| Bicolore majeur 5-4, 8+ H | Stayman |
| Bicolore majeur 5-5, 0-5 H | Texas `♥` puis passe |
| Bicolore majeur 5-5, 6-9 H | `4♦` : l'ouvreur choisit la manche majeure |
| Bicolore majeur 5-5, 10+ H | Texas `♠` puis annonce des cœurs (`4♥`, ou `3♥` en zone de chelem) |

Développements après Stayman :

- Réponse `2♦` avec bicolore 5-4 : majeure cinquième au palier de 2 (8-9 H, non forcing, l'ouvreur passe fitté ou revient à `2SA`) ; **chassé-croisé** avec 10+ H : saut à 3 dans la majeure quatrième, l'ouvreur rectifie à la manche avec trois cartes dans l'autre majeure ou revient à `3SA`.
- Réponse `2M` fittée : soutien `3M` 8-9 H (l'ouvreur conclut avec 17 H), manche directe 10-15 HLD.
- Sans fit : `2SA` 8-9 H, `3SA` 10-15 H, `4SA` quantitatif 16+ H (l'ouvreur accepte avec 17 H).

Non couvert pour l'instant : rectification Texas au palier de 3 (17 H, quatre atouts), transferts après réponse `2SA` (deux majeures), convention 2012, développements du Texas mineur avec singleton.

## Texas

Après `1SA` :

| Réponse | Transfert vers |
|---|---|
| `2♦` | Cœurs, cinq cartes ou plus |
| `2♥` | Piques, cinq cartes ou plus |
| `2♠` | Trèfles, six cartes ou plus |
| `3♣` | Carreaux, six cartes ou plus |

Après `2SA`, les transferts deviennent `3♦`, `3♥`, `3♠` et `4♣`. Les Texas majeurs sur `2SA` sont rectifiés seulement avec trois cartes; sinon l'ouvreur annonce `3SA`.

## Ouvertures fortes

Sur `2♣` fort indéterminé :

- Le répondant relaie à `2♦` ; l'ouvreur annonce `2SA` avec 22-23 H réguliers (développements Stayman/Texas, passe possible avec moins de 4 HL) ou sa couleur longue.
- Sur la couleur de l'ouvreur : manche avec fit majeur troisième (`4M`, `3M` avec moins de 4 H), `5m` avec fit et 6+ H, majeure cinquième du répondant, `3SA` avec 5+ H, sinon partielle ou `2SA` d'attente.

Sur `2♦` forcing de manche, réponses en As :

| Réponse | Signification |
|---|---|
| `2♥` | 0-7 H sans As |
| `2SA` | 8+ H sans As |
| `2♠` | Un As majeur |
| `3♣` / `3♦` | L'As de trèfle / de carreau |
| `3SA` | Deux As |

Développements : la majeure de l'ouvreur promet cinq cartes solides — le répondant fitté (deux cartes) conclut à la manche, ou donne le fit au palier de trois avec un espoir de chelem (8+ H) ; sans fit, majeure cinquième ou Sans-atout. La redemande à SA de l'ouvreur ouvre les développements Stayman/Texas, le répondant ne passe jamais avant la manche.

Sur `2SA` (20-21 H) : `3SA` dès 4 HL, Stayman et Texas comme sur `1SA` décalés d'un palier, `4SA` quantitatif avec 11-13 H (l'ouvreur conclut `6SA` avec 21 H), `6SA` dès 14 H, passe avec moins de 4 HL.

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
