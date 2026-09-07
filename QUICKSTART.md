# Guide de démarrage rapide

Lancez votre première partie de Bridge en 2 minutes!

## 🚀 Installation et démarrage

### Prérequis
- .NET 8.0 SDK installé
- Windows PowerShell 5.1+ (ou Linux/Mac avec .NET cross-platform)

### Télécharger et compiler
```bash
cd c:\Users\josse\Documents\Repo\bridge

# Compiler la solution
dotnet build

# ✓ La génération a réussi (0 erreurs)
```

### Exécuter le jeu
```bash
dotnet run --project src/Bridge.Console/Bridge.Console.csproj
```

## 🎮 Première partie

### Écran d'accueil
```
╔════════════════════════════════════════════════════════════╗
║         BRIDGE - Joueur humain (Nord) vs 3 IA            ║
╚════════════════════════════════════════════════════════════╝

Bienvenue au Bridge!
Vous jouez Nord contre Est, Sud et Ouest (IA).
Chaque contrat joueur = 1 donne.

Donne suivante? (O/N):
```

### Phase 1: Les enchères

```
=== PHASE DES ENCHÈRES ===
Votre main (Nord):
  ♠: A K Q J 9
  ♥: K Q
  ♦: A K
  ♣: Q J 10

Enchères actuelles: -
North à vous d'enchérir:
Votre enchère (P ou 1C-7SA): 1S
[Bidding] North ouvre 1♠ (5+ piques)

East: Passe
South: 3♠
West: Passe
North: Passe

Contrat final: 3♠ par North
Entame: East
```

### Phase 2: Le jeu de la carte

```
=== PHASE DU JEU DE LA CARTE ===
Vous jouez le contrat en tant que déclarant (Nord)

Votre main:
  ♠: A K Q J 9
  ♥: K Q
  ♦: A K
  ♣: Q J 10

Levée 1: -
Carte à jouer (A♠ K♠ Q♠ J♠ 9♠ K♥ Q♥ A♦ K♦ Q♣ J♣ 10♣): A♠

East joue 2♠
South joue 5♠
West joue 3♠

Levée remportée par North
...
```

### Résultat
```
✓ Contrat réussi: 9 levées. +120 points
Score Nord-Sud: 120

Donne suivante? (O/N):
```

## 📝 Commandes d'enchères

### Format
```
1C, 1D, 1H, 1S    = 1♣, 1♦, 1♥, 1♠ (premières enchères)
1SA, 1NT          = 1 Sans-Atout
2C, 2D, ..., 7SA  = Autres enchères
P, PASS           = Passer
```

### Exemples valides
```
✓ 1S       (1 Pique)
✓ 2H       (2 Cœurs)
✓ 3SA      (3 Sans-Atout)
✓ 7NT      (7 Sans-Atout)
✓ P        (Passe)
```

### Exemples invalides
```
❌ 1s       (minuscule, accepté après conversion)
❌ 8C       (niveau > 7)
❌ 0S       (niveau < 1)
❌ 1X       (couleur invalide)
```

## 🎴 Commandes de jeu de cartes

### Format
```
2C, 3D, 10H, AS   = Couleur + Rang
```

### Valeurs de carte
```
A  = As
K  = Roi
Q  = Dame
J  = Valet
10 = Dix
2-9 = Chiffre
```

### Exemples valides
```
✓ AS       (As de Pique)
✓ KH       (Roi de Cœur)
✓ 10D      (Dix de Carreau)
✓ 2C       (Deux de Trèfle)
```

## 🔗 Gestion du mort

Si **Nord/Sud prend le contrat** :
- **Nord** : Joue et regarde le jeu
- **Sud** : Joue comme mort (l'IA le joue automatiquement)

Si **Est/Ouest prend le contrat** :
- **Nord** : Joue en défense
- **Sud** : Joue en défense (IA)

## 📊 Système de notation

### Contrat réussi
```
Bid Level × Trick Value + Bonus + Overtricks

Exemple: 3♠ fait 9 levées (exact)
  Bid: 3 × 30 = 90 points
  Bonus: (car >= 100) = 50 points
  Overtricks: 0 × 30 = 0 points
  Total: 90 + 50 = 140 points
```

### Contrat chuté
```
Chute × 50 points

Exemple: 3♠ fait 8 levées (chute de 1)
  Pénalité: 1 × 50 = -50 points
```

## 🧪 Lancer les tests

```bash
# Exécuter tous les tests
dotnet test

# Afficher les résultats
# ✓ ModelTests.Card_ToString_ReturnsCorrectFormat
# ✓ AuctionTests.Auction_AddCall_IncreasesCallCount
# ✓ FifthMajorBiddingTests.FifthMajor_WeakHand_ReturnsNull
# ✓ CardPlayEngineTests.CardPlayEngine_PlayValidCard_RemovesFromHand
# ...
# Test Run Successful. Total tests: 10, Passed: 10
```

## 📁 Structure de fichiers générée

```
Bridge/
├── logs/                               (Créé automatiquement)
│   └── bridge-2026-08-29.log         (Logs de la session)
└── src/Bridge.Console/bin/Debug/net8.0/
    ├── Bridge.Console.exe
    ├── Bridge.Core.dll
    ├── Bridge.AI.Bidding.dll
    ├── Bridge.AI.CardPlay.dll
    └── Bridge.Engine.dll
```

## 🔧 Options avancées

### Augmenter le niveau de logging
Modifiez Bridge.Console/Program.cs :
```csharp
// Avant (par défaut)
var logger = new ConsoleFileLogger("logs", LogLevel.Information);

// Après (verbose)
var logger = new ConsoleFileLogger("logs", LogLevel.Debug);
```

### Utiliser une autre stratégie d'IA
```csharp
// Avant
var cardPlayAi = new AdvancedCardPlayAi(logger);

// Après
var cardPlayAi = new SimpleCardPlayAi(logger);
```

## ❓ Dépannage

### Problème: "dotnet: command not found"
```bash
# Solution: Installer .NET 8.0 SDK
# https://dotnet.microsoft.com/en-us/download/dotnet/8.0
```

### Problème: Contrat toujours chuté
```bash
# Vérifier les enchères de l'IA
# Logs: bridge-YYYY-MM-DD.log
# Chercher: [Bidding] pour les décisions
```

### Problème: IA joue une carte illégale
```bash
# Cela ne devrait jamais arriver
# Contactez le support avec les logs
```

### Problème: Fichiers logs introuvables
```bash
# Créer le dossier logs
mkdir logs

# Relancer le jeu
dotnet run --project src/Bridge.Console/Bridge.Console.csproj
```

## 📚 Ressources

| Document | Contenu |
|----------|---------|
| README.md | Vue d'ensemble générale |
| ARCHITECTURE.md | Architecture technique détaillée |
| BIDDING_RULES.md | Convention Fifth Major |
| DEBUGGING.md | Guide de débogage |
| QUICKSTART.md | Ce document |

## 🎯 Prochain pas

1. ✓ Lancez une partie
2. Lire BIDDING_RULES.md pour comprendre les enchères
3. Lire DEBUGGING.md pour déboguer vos problèmes
4. Lancer dotnet test pour valider le système
5. Modifier les stratégies d'IA pour expérimenter

## 📞 Support

Pour l'aide:
- **Enchères** : BIDDING_RULES.md
- **Architecture** : ARCHITECTURE.md
- **Débogage** : DEBUGGING.md
- **Erreurs** : Vérifier les logs (bridge-YYYY-MM-DD.log)

---

**Version** : 1.0.0  
**Dernière mise à jour** : 2026-08-29  
**Bon jeu!** 🎮
