# Architecture technique - Bridge Multi-Projets

## 🏛️ Vue d'ensemble architecturale

### Principes de design
1. **Séparation des responsabilités** : Chaque projet = 1 responsabilité
2. **Injection de dépendances** : Interfaces pour l'extensibilité
3. **Logging transversal** : System observabilité complète
4. **Testabilité** : Code isolé et testable

## 📦 Détails des projets

### Bridge.Core
**Responsabilité** : Définitions de base et contrats

```csharp
Models/
├── Suit, Denomination, Rank, Seat (énums)
├── Card (struct record)
├── Bid (struct record)
├── Contract (struct record)
├── AuctionCall, PlayedCard (records)
├── HandEvaluation (class)
├── Deal (sealed class)
└── Auction (sealed class)

Strategies/
├── IBiddingStrategy (interface)
├── ICardPlayStrategy (interface)
└── ICardPlayEngine (interface)

Logging/
├── LogLevel (enum)
├── ILogger (interface)
├── ConsoleFileLogger (implementation)
└── LoggerProvider (singleton factory)
```

**Dépendances** : Aucune (foundation)

---

### Bridge.AI.Bidding
**Responsabilité** : Stratégies d'enchères

```csharp
SefBiddingEngine : ICompetitiveBiddingStrategy
├── DecideBid(seat, hand, auction) → Bid?
├── DecideCall(seat, hand, auction) → AuctionCall
├── Opening / FirstResponse / OpenerRebid / ResponderRebid
└── Stayman, Texas, ouvertures fortes, barrages et interventions
```

**Algorithme** :
1. Évaluer la main (PH + PD)
2. Si ouverture : chercher majeure 5+, puis SA balancé, puis mineure
3. Si réponse : soutenir partenaire ou chercher majeure
4. Vérifier légalité et retourner bid

**Dépendances** : Bridge.Core

---

### Bridge.AI.CardPlay
**Responsabilité** : Stratégies de jeu de cartes

```csharp
AdvancedCardPlayAi : ICardPlayStrategy
├── ChooseCard(game, seat) → Card
├── ChooseLead(...) → Card (séquence d'enchères, contrat et distribution)
├── ChooseFollow(...) → Card (impasse, Bath, honneur sur honneur, 3e main)
└── Signalisation, déblocage, coupe et défausse

SimpleCardPlayAi : ICardPlayStrategy
└── ChooseCard(game, seat) → Card (bas d'abord, suit si possible)
```

**Stratégies** :
- **Advanced** : historique des enchères et plis, entame raisonnée, jeu du déclarant et défense coopérative
- **Simple** : Joue petit, suit la couleur

**Concepts de jeu couverts** :
- Déclarant : plan de couleur longue, affranchissement, impasse, laisser-passer,
  coup de Bath, conservation des honneurs et coupe de la main courte avant le retrait des atouts.
- Défense : entame selon les enchères, retour dans la couleur du partenaire,
  troisième main, honneur sur honneur, déblocage, signalisation, coupe utile et refus
  de gaspiller un atout lorsque le pli est déjà perdu.
- Les techniques avancées dépendant d’une analyse complète des distributions
  adverses (squeeze, élimination-mise en main, coups de Deschapelles/Merrimac,
  compte exact des mains) restent hors du périmètre de l’heuristique actuelle.

**Dépendances** : Bridge.Core

---

### Bridge.Engine
**Responsabilité** : Moteur et orchestration

```csharp
CardPlayEngine : ICardPlayEngine
├── Properties: NextToPlay, DeclarerTricks, DefenderTricks, TrickNumber
├── Methods:
│   ├── Hand(seat) → IReadOnlyList<Card>
│   ├── LegalCards(seat) → IReadOnlyList<Card>
│   ├── Play(seat, card) → Seat (winner)
│   ├── GetTrumpSuit() → Suit?
│   └── GetDeclarer() → Seat
└── Private: DetermineWinner(), Beats()

BridgeGameEngine
├── _biddingStrategy : IBiddingStrategy
├── _cardPlayStrategy : ICardPlayStrategy
├── DecideBid(...) → Bid? (delegue à stratégie)
├── ChooseCard(...) → Card (delegue à stratégie)
├── AnalyzeDeal(...) → CardPlayAnalysis (ligne de carte sur un contrat donné)
└── AnalyzeFullDeal(...) → AiDealAnalysis (enchères, contrat et 52 cartes sur une copie)
```

**Logique** :
- Play() : valide légalité, met à jour état, détermine gagnant
- Beats() : règles de prise (atout > couleur > autres)

**Dépendances** : Bridge.Core, Bridge.AI.Bidding, Bridge.AI.CardPlay

---

### Bridge.Console
**Responsabilité** : Interface utilisateur console

```csharp
Main()
├── Initialiser logger
├── Initialiser stratégies IA
├── Boucle donnes:
│   ├── PlayDeal()
│   │   ├── Phase enchères (Nord humain, autres IA)
│   │   ├── Phase jeu (Nord humain/IA, autres IA)
│   │   └── Calculer score
│   └── Demander continuation
└── Afficher score final

UI Helpers:
├── DisplayHand()
├── ReadHumanBid()
├── ReadHumanCard()
├── FormatAuction()
├── FormatTrick()
├── TryParseBid()
└── ScoreContract()
```

**Interactions** :
- Humain : Enchères et jeu (si Nord)
- IA : Automatique

**Dépendances** : Tous les autres

---

### Bridge.Tests
**Responsabilité** : Validation

```csharp
ModelTests
├── Card_ToString_ReturnsCorrectFormat()
├── Deal_Create_Distributes52Cards()
└── Seat_Partner_ReturnsCorrectPartner()

AuctionTests
├── Auction_AddCall_IncreasesCallCount()
├── Auction_IsLegal_...()
├── Auction_IsComplete_...()
└── Auction_GetContract_...()

FifthMajorBiddingTests
├── FifthMajor_OpeningWith5Spades_Returns1Spades()
├── FifthMajor_WeakHand_ReturnsNull()
└── FifthMajor_BalancedHand15To17Points_...()

CardPlayEngineTests
├── CardPlayEngine_PlayValidCard_...()
├── CardPlayEngine_PlayInvalidCard_...()
└── CardPlayEngine_LegalCards_...()
```

**Dépendances** : Tous les autres

---

## 🔄 Flux d'exécution

### Initialization
```
Program.Main()
  ├─ ConsoleFileLogger("logs", LogLevel.Information)
  ├─ LoggerProvider.SetLogger(logger)
  ├─ FifthMajorBiddingEngine(logger)
  ├─ AdvancedCardPlayAi(logger)
  └─ BridgeGameEngine(bidding, cardplay, logger)
```

### PlayDeal()
```
1. Deal.Create(random) → Distribute 52 cards
2. Auction() → Historique enchères

BIDDING PHASE:
  Loop: Seat -> North, East, South, West, repeat
    ├─ If North: ReadHumanBid()
    ├─ Else: BridgeGameEngine.DecideBid(seat, hand, auction)
    └─ auction.AddCall(seat, bid)
    Until: auction.IsComplete

  contract = auction.GetContract()
  If null: Return (passed out)

CARD PLAY PHASE:
  game = CardPlayEngine(deal, contract)
  Loop: game.NextToPlay
    ├─ If North: ReadHumanCard()
    ├─ Else: BridgeGameEngine.ChooseCard(game, seat)
    └─ winner = game.Play(seat, card)
    Until: game.IsComplete

3. Score:
   ├─ declarerMade = game.DeclarerTricks >= contract.RequiredTricks
   └─ points = ScoreContract(contract, game.DeclarerTricks)
```

## 🧵 Thread Safety

### Logging
- **Thread-safe** : Utilise lock dans ConsoleFileLogger
- **Async** : Non-blocking pour console

### Game Logic
- **Mono-thread** : Pas de concurrence (design game)
- **État** : Mutable (intentionnel pour performance)

## 🔌 Points d'extension

### Ajouter une stratégie d'enchères
```csharp
public class CustomBiddingStrategy : IBiddingStrategy
{
    public Bid? DecideBid(Seat seat, IReadOnlyList<Card> hand, Auction auction)
    {
        // Votre logique
    }
}
```

### Ajouter une stratégie de jeu
```csharp
public class CustomCardPlayAi : ICardPlayStrategy
{
    public Card ChooseCard(ICardPlayEngine game, Seat seat)
    {
        // Votre logique
    }
}
```

### Utiliser dans console
```csharp
var engine = new BridgeGameEngine(
    new CustomBiddingStrategy(),
    new CustomCardPlayAi(),
    logger
);
```

## 📊 Métriques de qualité

| Métrique | Valeur |
|----------|--------|
| Projets | 6 (Core, 2x AI, Engine, Console, Tests) |
| Classes | ~15 |
| Interfaces | 4 |
| Lignes de code | ~1500 |
| Couverture tests | 70%+ |
| Complexité | O(1-n) selon composant |

## 🎯 Objectifs architecturaux

✓ Séparation claire des responsabilités
✓ Testabilité complète
✓ Extensibilité par interfaces
✓ Logging intégré et observable
✓ Performance acceptable
✓ Code lisible et maintenable

---

**Dernière mise à jour** : 2026-08-29
