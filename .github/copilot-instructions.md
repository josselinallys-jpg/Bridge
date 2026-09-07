# Projet Bridge

Jeu de bridge en C#/.NET 8, avec IA d'enchères et de jeu de la carte basée sur des règles (pas de LLM).

## Structure de la solution (Bridge.sln)

- `src/Bridge.Core` : modèles de base (`Models/Models.cs`, `Auction.cs`, `BridgeScoring.cs`), stratégies (`Strategies/Strategies.cs`), logging (`Logging/Logger.cs`)
- `src/Bridge.Engine` : moteur de jeu de la carte (`CardPlayEngine.cs`)
- `src/Bridge.AI.Bidding` : IA d'enchères — `SefBiddingEngine.cs` (SEF) et `FifthMajorBiddingEngine.cs` (majeure cinquième)
- `src/Bridge.AI.CardPlay` : IA de jeu de la carte (`CardPlayAi.cs`)
- `src/Bridge.Console` : application console
- `src/Bridge.Desktop` : application WPF (Windows uniquement)
- `tests/Bridge.Tests` : tests xUnit

## Commandes

- Build : `dotnet build Bridge.sln`
- Tests : `dotnet test tests/Bridge.Tests/Bridge.Tests.csproj`
- Console : `dotnet run --project src/Bridge.Console`

## Conventions

- C# avec nullable et implicit usings activés, cible net8.0
- Les règles d'enchères sont documentées dans `BIDDING_RULES.md` ; ne pas modifier une règle sans la mettre en cohérence avec ce fichier
- Architecture modulaire : stratégies injectées via les interfaces `IBiddingStrategy` / `ICardPlayStrategy` ; ajouter une nouvelle stratégie = nouvelle classe, ne pas modifier les existantes
- Documentation d'architecture dans `ARCHITECTURE.md`, débogage dans `DEBUGGING.md`

## Directives pour l'assistant

- Répondre en français, de manière concise
- Ne pas refactorer ni ajouter de fonctionnalités non demandées
- Ajouter/mettre à jour les tests xUnit dans `tests/Bridge.Tests` pour toute modification de logique d'enchères ou de jeu
- Ignorer les dossiers `bin/`, `obj/` et `logs/`
