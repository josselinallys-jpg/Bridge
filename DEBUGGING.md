# Guide de Débogage et Logging

Utiliser le système de logging pour diagnostiquer les problèmes et suivre l'exécution du jeu.

## 🔍 Niveaux de Log

### LogLevel.Trace (0)
- Détails minimes
- Chaque décision, chaque carte jouée
- ⚠️ **Très verbeux** - À utiliser seulement pour débogage profond

**Exemple** :
```
[14:23:45.123] [Trace      ] [CardPlay] North joue 5♥
```

### LogLevel.Debug (1)
- Décisions principales
- Actions de l'IA
- État du jeu

**Exemple** :
```
[14:23:45.100] [Debug      ] [Bidding] North évalue sa main: PH=14, PD=1, Total=15
[14:23:45.102] [Debug      ] [Bidding] North ouvre 1♠ (5+ piques)
[14:23:45.150] [Debug      ] [CardPlay] North entame avec A♠
```

### LogLevel.Information (2) - Défaut
- Actions du joueur et de l'IA
- Contrats, enchères finales
- Résultats de levées

**Exemple** :
```
[14:23:45.200] [Information] [Bidding] East: Passe
[14:23:45.250] [Information] [Engine] Contrat: 3♠ par Nord
[14:23:45.300] [Information] [Engine] Levée 1 gagnée par North
```

### LogLevel.Warning (3)
- Situations anormales (rare)
- Contrats chutés
- Anomalies de jeu

**Exemple** :
```
[14:23:50.500] [Warning    ] ✗ Contrat chuté: 9 levées. -50 points
```

### LogLevel.Error (4)
- Erreurs de jeu
- Exceptions non-fatales
- Problèmes d'I/O

**Exemple** :
```
[14:23:51.000] [Error      ] Erreur lors de l'écriture du log: Permission denied
```

### LogLevel.Critical (5)
- Erreurs fatales
- État du jeu compromis
- Crash imminent

**Exemple** :
```
[14:23:52.000] [Critical   ] Exception non gérée: NullReferenceException
```

## ⚙️ Configuration du logging

### Initialisation standard
```csharp
// Par défaut: Information level, console + fichier
var logger = new ConsoleFileLogger("logs", LogLevel.Information);
LoggerProvider.SetLogger(logger);
```

### Logging verbose (débogage)
```csharp
// Affiche tous les détails
var logger = new ConsoleFileLogger("logs", LogLevel.Trace);
LoggerProvider.SetLogger(logger);
```

### Logging discret (production)
```csharp
// Seulement les avertissements et erreurs
var logger = new ConsoleFileLogger("logs", LogLevel.Warning);
LoggerProvider.SetLogger(logger);
```

## 📁 Fichiers de log

### Localisation
```
Bridge/
└── logs/
    ├── bridge-2026-08-29.log
    ├── bridge-2026-08-30.log
    └── bridge-2026-08-31.log
```

### Rotation
- Un fichier par jour
- Nommage: `bridge-YYYY-MM-DD.log`
- Append automatique si le fichier existe

### Consultation
```bash
# Voir les dernières 50 lignes
Get-Content logs/bridge-2026-08-29.log -Tail 50

# Voir aujourd'hui
Get-Content logs/bridge-$(Get-Date -Format 'yyyy-MM-dd').log

# Rechercher par siège
Select-String "East" logs/bridge-*.log

# Compter les actions
(Select-String "Encheres finales" logs/bridge-*.log).Count
```

## 🔍 Analyse des logs

### Pattern: Suivi d'une donne

```bash
# Afficher une donne complète (toutes les entrées jusqu'à "=== Donne")
$log = Get-Content logs/bridge-2026-08-29.log
$start = $log.Count - 1
while ($start -gt 0 -and $log[$start] -notlike "*=== Donne*") { $start-- }
$log[$start..$($log.Count-1)]
```

### Pattern: Erreurs d'enchères

```bash
Select-String "Enchere invalide|insuffisante" logs/bridge-*.log
```

### Pattern: Enchères d'une partie

```bash
Select-String "\[Bidding\]" logs/bridge-2026-08-29.log
```

### Pattern: Jeu de cartes

```bash
Select-String "\[CardPlay\]|\[Engine\]" logs/bridge-2026-08-29.log
```

## 🐛 Scénarios de débogage courants

### Problème: IA ne choisit pas la bonne enchère

**Solution** :
```csharp
// Activer Trace pour voir tous les calculs
var logger = new ConsoleFileLogger("logs", LogLevel.Trace);

// Réexécuter le jeu
// Consulter logs pour voir:
// - Évaluation de la main
// - Logique de sélection
// - Décision finale
```

### Problème: IA joue une carte illégale

**Solution** :
```csharp
// Vérifier les logs CardPlay
Select-String "\[CardPlay\]" logs/bridge-*.log | Select-Object -Last 20

// Vérifier les logs Engine (LegalCards)
Select-String "LegalCards" logs/bridge-*.log

// Ajouter du logging personnalisé dans CardPlayEngine
_logger.Trace($"[Engine] Cartes légales: {string.Join(", ", legalCards)}");
```

### Problème: Score incorret

**Solution** :
```csharp
// Vérifier le calcul du contrat
Select-String "Contrat" logs/bridge-*.log

// Vérifier le nombre de levées
Select-String "Levée.*gagnée" logs/bridge-*.log | Measure-Object

// Recalculer manuellement avec ScoreContract()
```

## 🎯 Logging personnalisé dans le code

### Ajouter du logging dans une stratégie

```csharp
public class MyBiddingStrategy : IBiddingStrategy
{
    private readonly ILogger _logger;

    public MyBiddingStrategy(ILogger? logger = null)
    {
        _logger = logger ?? LoggerProvider.GetLogger();
    }

    public Bid? DecideBid(Seat seat, IReadOnlyList<Card> hand, Auction auction)
    {
        _logger.Debug($"[MyBidding] {seat} analyse sa main");
        
        var evaluation = new HandEvaluation(hand);
        _logger.Debug($"[MyBidding] Points: {evaluation.TotalPoints}");
        
        if (evaluation.TotalPoints < 12)
        {
            _logger.Information($"[MyBidding] {seat} passe (faible)");
            return null;
        }
        
        // ... reste de la logique
    }
}
```

### Utiliser dans Console

```csharp
var logger = new ConsoleFileLogger("logs", LogLevel.Debug);
LoggerProvider.SetLogger(logger);

var biddingAi = new MyBiddingStrategy(logger);
// Les logs s'afficheront maintenant
```

## 📊 Analyse statistique des logs

### Compter les passes
```bash
$logs = Get-Content logs/bridge-2026-08-29.log
($logs | Select-String "Passe").Count
```

### Moyenne d'enchères par donne
```bash
$contracts = (Select-String "Contrat" logs/bridge-*.log).Count
$deals = (Select-String "=== Donne" logs/bridge-*.log).Count
Write-Host "Moyenne enchères par donne: $(($contracts / $deals).ToString('F2'))"
```

### Taux de réussite des contrats
```bash
$success = (Select-String "Contrat réussi" logs/bridge-*.log).Count
$total = (Select-String "Contrat.*par" logs/bridge-*.log).Count
Write-Host "Taux de réussite: $($([math]::Round(($success / $total) * 100)).ToString())%"
```

## 🎨 Couleurs de log (Console)

| Niveau | Couleur |
|--------|---------|
| Trace | Gris |
| Debug | Cyan |
| Information | Vert |
| Warning | Jaune |
| Error | Rouge |
| Critical | Magenta |

## 🔐 Sécurité des logs

### Données sensibles
- Pas de données personnelles
- Pas de tokens/clés
- Seulement état du jeu

### Taille des fichiers
- Un fichier par jour
- Logs non archivés (suppression manuelle)
- Environ 5-10 MB par jour en mode Debug

### Droits d'accès
```bash
# Vérifier les permissions
Get-Item logs/bridge-*.log | Select-Object FullName, CreationTime

# Modifier les permissions si nécessaire
Set-ItemProperty logs/bridge-*.log -Name Attributes -Value "Normal"
```

## 📝 Best Practices

1. ✅ Utiliser Information pour le jeu normal
2. ✅ Utiliser Debug pour déboguer une session
3. ✅ Utiliser Trace seulement pour diagnostic profond
4. ✅ Vérifier les logs après chaque partie problématique
5. ✅ Archiver les logs intéressants
6. ❌ Ne pas ignorer les Warning/Error
7. ❌ Ne pas garder Trace activé en production

---

**Dernière mise à jour** : 2026-08-29
