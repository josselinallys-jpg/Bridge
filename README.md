# Bridge - Moteur de jeu multi-projets

Architecture multi-projets professionnelle pour le jeu de Bridge avec IA.

## 🏗️ Structure du projet

```
Bridge/
├── Bridge.sln                           (Solution principale)
├── src/
│   ├── Bridge.Core/                     (DLL - Modèles et logging)
│   │   ├── Models/                      Enums, Card, Bid, Contract, Auction, Deal
│   │   ├── Strategies/                  Interfaces IBiddingStrategy, ICardPlayStrategy
│   │   └── Logging/                     Système de logging (fichier + console)
│   │
│   ├── Bridge.AI.Bidding/               (DLL - IA des enchères)
│   │   ├── SefBiddingEngine             Système d'Enseignement Français
│   │   └── FifthMajorBiddingEngine      Façade de compatibilité
│   │
│   ├── Bridge.AI.CardPlay/              (DLL - IA du jeu de cartes)
│   │   ├── AdvancedCardPlayAi           Entames, impasses, signalisation et défense
│   │   └── SimpleCardPlayAi             Stratégie basique
│   │
│   ├── Bridge.Engine/                   (DLL - Moteur du jeu)
│   │   ├── CardPlayEngine               Implémentation ICardPlayEngine
│   │   └── BridgeGameEngine             Orchestrateur IA
│   │
│   ├── Bridge.Console/                  (EXE - Application console)
│   │   └── Program.cs                   Mode solo: Nord vs 3 IA
│   │
│   └── Bridge.Desktop/                  (EXE - Application WPF)
│       ├── Assets/                      Jeu de 52 cartes
│       └── MainWindow                   Table graphique interactive
│
└── tests/
    └── Bridge.Tests/                    (xUnit - Tests unitaires)
        └── Tests pour tous les composants
```

## 📦 Dépendances des projets

```
Bridge.Core
├── Bridge.AI.Bidding
├── Bridge.AI.CardPlay
└── Bridge.Engine
    └── Bridge.Console
        └── Bridge.Tests (références croisées)
```

## 🎮 Modes de jeu

### Console (Bridge.Console)
- **Joueur humain** : Nord
- **Adversaires** : Est, Sud, Ouest (IA)
- **Logique** : Si Nord/Sud prend, vous jouez le mort

### Bureau (Bridge.Desktop)
- **Interface WPF** : enchères, table, pli courant, score et journal
- **Cartes** : assets graphiques embarqués dans l'application
- **Contrôle** : Nord, plus Sud lorsque Nord-Sud joue le contrat
- **Confrontation IA** : après votre donne, l'IA rejoue d'abord toute la séquence
    d'enchères, choisit son propre contrat, puis calcule ses levées et son score
- **Replay ouvert** : les quatre mains sont visibles; un clic est requis après
    les enchères puis entre chaque pli, et le replay peut être relancé

## 🔍 Système de Logging

### Niveaux de log
- **Trace** : Détails minimes
- **Debug** : Informations internes
- **Information** : Actions principales ✓
- **Warning** : Situations anormales ⚠️
- **Error** : Erreurs ❌
- **Critical** : Erreurs critiques 🚨

### Fichiers
- Sortie **console** : colorée et formatée
- Fichiers **logs/** : `bridge-YYYY-MM-DD.log`

### Initialisation
```csharp
var logger = new ConsoleFileLogger("logs", LogLevel.Information);
LoggerProvider.SetLogger(logger);
```

## 🧪 Tests Unitaires

### Exécuter les tests
```bash
dotnet test
```

### Couverture
- ✓ Models (Card, Bid, Auction, Deal)
- ✓ Fifth Major Bidding
- ✓ Card Play Engine
- ✓ Logging

## 🚀 Compilation et exécution

### Build complet
```bash
cd c:\Users\josse\Documents\Repo\bridge
dotnet build
```

### Exécuter l'interface graphique
```bash
dotnet run --project src/Bridge.Desktop/Bridge.Desktop.csproj
```

### Exécuter le jeu console
```bash
dotnet run --project src/Bridge.Console/Bridge.Console.csproj
```

### Exécuter les tests
```bash
dotnet test tests/Bridge.Tests/Bridge.Tests.csproj
```

## 📝 Convention Fifth Major

### Principes
- **Majeures 5+** : Priorité absolue (1♠ ou 1♥)
- **Sans-atout balancé** : 15-17 PH
- **Mineures 4+** : Par défaut
- **Soutien 3+** : Suffisant pour soutenir
- **Sans-atout** : zones 15-17, 20-21, 22-23 et 24+ H
- **Conventions** : Stayman quatre réponses et Texas majeurs/mineurs
- **Conventions avancées** : Roudi, quatrième couleur forcing et Blackwood 5 clés 30-41
- **Ouvertures fortes** : 2♣ fort indéterminé, 2♦ forcing de manche
- **Barrages** : 2 faibles majeurs et ouvertures au palier de 3/4
- **Compétitives** : interventions naturelles, 1SA, Landy, contre et surcontre

## Jeu de la carte avancé

- **Entame issue des enchères** : priorité à la couleur naturelle du partenaire,
    évitement des couleurs du déclarant et préférence pour une couleur non nommée.
- **Entames techniques** : tête de séquence, quatrième meilleure à SA, singleton
    à la couleur et petite carte d'une couleur longue.
- **Déclarant** : impasse vers les tenaces A-D ou R-V, retrait des atouts,
    laissez-passer et coup de Bath.
- **Défense** : honneur sur honneur, troisième main haute, déblocage d'honneur,
    coupe économique et retour dans la couleur appelée.
- **Signalisation** : appel/refus en pair-impair simplifié et défausse raisonnée.

### Points
- As = 4, Roi = 3, Dame = 2, Valet = 1
- +1 point par longueur > 3 cartes

## 🏃 Flux principal (Console)

```
1. Initialisation du logger
2. Boucle de donnes:
   - Création donne aléatoire
   - Phase enchères (Nord joue, 3 IA)
   - Déduction du contrat
   - Phase jeu de cartes:
     * Nord joue ou regarde
     * 3 IA jouent automatiquement
   - Calcul du score
3. Affichage score final
```

## 🔧 Configuration et extensibilité

### Ajouter une nouvelle stratégie d'enchères
```csharp
public class MyBiddingStrategy : IBiddingStrategy
{
    public Bid? DecideBid(Seat seat, IReadOnlyList<Card> hand, Auction auction)
    {
        // Votre logique
    }
}

var engine = new BridgeGameEngine(
    new MyBiddingStrategy(),
    new AdvancedCardPlayAi(),
    logger
);
```

### Ajouter une nouvelle stratégie de jeu
```csharp
public class MyCardPlayAi : ICardPlayStrategy
{
    public Card ChooseCard(ICardPlayEngine game, Seat seat)
    {
        // Votre logique
    }
}
```

## 📊 Performance

| Composant | Complexité | Notes |
|-----------|-----------|-------|
| FifthMajorBiddingEngine | O(1) | Décision instantanée |
| AdvancedCardPlayAi | O(n) | n < 13 cartes légales |
| CardPlayEngine | O(1) | Par carte jouée |
| Logging | O(1) | Thread-safe |

## 🐛 Débogage

### Activer verbose logging
```csharp
var logger = new ConsoleFileLogger("logs", LogLevel.Trace);
LoggerProvider.SetLogger(logger);
```

### Consulter les logs
```bash
Get-Content logs/bridge-2026-08-29.log -Tail 50
```

## 📋 Checklist de développement

- ✓ Structure multi-projets
- ✓ Système de logging
- ✓ Modèles et énums
- ✓ Interfaces stratégies
- ✓ IA enchères Fifth Major
- ✓ IA jeu de cartes (Simple + Advanced)
- ✓ Moteur de jeu
- ✓ Application console
- ✓ Application graphique WPF
- ✓ Tests unitaires
- ✓ Documentation
- ⏳ Optimisations
- ⏳ Mode IA vs IA

## 📞 Support

Pour toute question sur:
- **Architecture** : Voir `ARCHITECTURE.md`
- **Convention** : Voir `BIDDING_RULES.md`
- **Tests** : Exécuter `dotnet test`

---

**Dernière mise à jour** : 2026-08-29  
**Version** : 1.0.0  
**Status** : ✓ Production-ready
