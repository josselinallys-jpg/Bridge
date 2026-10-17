using Bridge.Core.Models;
using Bridge.Core.Strategies;
using Bridge.Core.Logging;

namespace Bridge.AI.CardPlay;

public enum CardPlayMode
{
    DeclarerNoTrump,
    DeclarerSuit,
    DefenseNoTrump,
    DefenseSuit
}

/// <summary>Point d'entrée qui sélectionne la stratégie selon le contrat et le camp.</summary>
public sealed class ContractAwareCardPlayAi : ICardPlayStrategy
{
    private readonly ICardPlayStrategy _declarerNoTrump;
    private readonly ICardPlayStrategy _declarerSuit;
    private readonly ICardPlayStrategy _defenseNoTrump;
    private readonly ICardPlayStrategy _defenseSuit;

    public ContractAwareCardPlayAi(ILogger? logger = null)
    {
        _declarerNoTrump = new DeclarerNoTrumpCardPlayAi(logger);
        _declarerSuit = new DeclarerSuitCardPlayAi(logger);
        _defenseNoTrump = new DefenseNoTrumpCardPlayAi(logger);
        _defenseSuit = new DefenseSuitCardPlayAi(logger);
    }

    public Card ChooseCard(ICardPlayEngine game, Seat seat)
    {
        var declarerSide = seat.IsNorthSouth() == game.GetDeclarer().IsNorthSouth();
        if (game.GetTrumpSuit() is null)
            return (declarerSide ? _declarerNoTrump : _defenseNoTrump).ChooseCard(game, seat);
        return (declarerSide ? _declarerSuit : _defenseSuit).ChooseCard(game, seat);
    }
}

public sealed class DeclarerNoTrumpCardPlayAi : ICardPlayStrategy
{
    private readonly AdvancedCardPlayAi _shared;
    public DeclarerNoTrumpCardPlayAi(ILogger? logger = null) => _shared = new(logger);
    public Card ChooseCard(ICardPlayEngine game, Seat seat) => _shared.ChooseCard(game, seat);
}

public sealed class DeclarerSuitCardPlayAi : ICardPlayStrategy
{
    private readonly AdvancedCardPlayAi _shared;
    public DeclarerSuitCardPlayAi(ILogger? logger = null) => _shared = new(logger);
    public Card ChooseCard(ICardPlayEngine game, Seat seat) => _shared.ChooseCard(game, seat);
}

public sealed class DefenseNoTrumpCardPlayAi : ICardPlayStrategy
{
    private readonly AdvancedCardPlayAi _shared;
    public DefenseNoTrumpCardPlayAi(ILogger? logger = null) => _shared = new(logger);
    public Card ChooseCard(ICardPlayEngine game, Seat seat) => _shared.ChooseCard(game, seat);
}

public sealed class DefenseSuitCardPlayAi : ICardPlayStrategy
{
    private readonly AdvancedCardPlayAi _shared;
    public DefenseSuitCardPlayAi(ILogger? logger = null) => _shared = new(logger);
    public Card ChooseCard(ICardPlayEngine game, Seat seat) => _shared.ChooseCard(game, seat);
}