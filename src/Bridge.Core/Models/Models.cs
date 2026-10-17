namespace Bridge.Core.Models;

/// <summary>
/// Représente une couleur de carte (Pique, Cœur, Carreau, Trèfle).
/// </summary>
public enum Suit
{
    Clubs,
    Diamonds,
    Hearts,
    Spades
}

/// <summary>
/// Représente une dénomination d'enchère (Pique, Cœur, Carreau, Trèfle, Sans-Atout).
/// </summary>
public enum Denomination
{
    Clubs,
    Diamonds,
    Hearts,
    Spades,
    NoTrump
}

/// <summary>
/// Représente un rang de carte (2 à As).
/// </summary>
public enum Rank
{
    Two = 2,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace
}

/// <summary>
/// Représente une position à table (Nord, Est, Sud, Ouest).
/// </summary>
public enum Seat
{
    North,
    East,
    South,
    West
}

/// <summary>
/// Représente une carte de jeu (couleur + rang).
/// </summary>
public readonly record struct Card(Suit Suit, Rank Rank)
{
    public override string ToString() => $"{RankText(Rank)}{SuitText(Suit)}";

    public static string RankText(Rank rank) => rank switch
    {
        Rank.Ace => "A",
        Rank.King => "K",
        Rank.Queen => "Q",
        Rank.Jack => "J",
        Rank.Ten => "10",
        _ => ((int)rank).ToString()
    };

    public static string SuitText(Suit suit) => suit switch
    {
        Suit.Clubs => "♣",
        Suit.Diamonds => "♦",
        Suit.Hearts => "♥",
        Suit.Spades => "♠",
        _ => throw new ArgumentOutOfRangeException(nameof(suit))
    };
}

/// <summary>
/// Représente une enchère (niveau + dénomination).
/// </summary>
public readonly record struct Bid(int Level, Denomination Denomination)
{
    public override string ToString() => $"{Level}{DenominationText(Denomination)}";

    public static string DenominationText(Denomination denomination) => denomination switch
    {
        Denomination.Clubs => "♣",
        Denomination.Diamonds => "♦",
        Denomination.Hearts => "♥",
        Denomination.Spades => "♠",
        Denomination.NoTrump => "SA",
        _ => throw new ArgumentOutOfRangeException(nameof(denomination))
    };
}

public enum AuctionAction
{
    Pass,
    Bid,
    Double,
    Redouble
}

public enum Doubling
{
    None = 1,
    Doubled = 2,
    Redoubled = 4
}

public enum Vulnerability
{
    None,
    NorthSouth,
    EastWest,
    Both
}

public static class VulnerabilityExtensions
{
    private static readonly Vulnerability[] BoardCycle =
    [
        Vulnerability.None, Vulnerability.NorthSouth, Vulnerability.EastWest, Vulnerability.Both,
        Vulnerability.NorthSouth, Vulnerability.EastWest, Vulnerability.Both, Vulnerability.None,
        Vulnerability.EastWest, Vulnerability.Both, Vulnerability.None, Vulnerability.NorthSouth,
        Vulnerability.Both, Vulnerability.None, Vulnerability.NorthSouth, Vulnerability.EastWest
    ];

    public static Vulnerability ForBoard(int boardNumber)
    {
        if (boardNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(boardNumber));
        }

        return BoardCycle[(boardNumber - 1) % BoardCycle.Length];
    }

    public static bool IsVulnerable(this Vulnerability vulnerability, Seat seat) => vulnerability switch
    {
        Vulnerability.None => false,
        Vulnerability.NorthSouth => seat.IsNorthSouth(),
        Vulnerability.EastWest => seat.IsEastWest(),
        Vulnerability.Both => true,
        _ => throw new ArgumentOutOfRangeException(nameof(vulnerability))
    };
}

/// <summary>
/// Représente un contrat (enchère + déclarant).
/// </summary>
public readonly record struct Contract(Bid Bid, Seat Declarer, Doubling Doubling = Doubling.None)
{
    public Seat Dummy => Declarer.Partner();
    public int RequiredTricks => Bid.Level + 6;
    public override string ToString() => $"{Bid}{DoublingText(Doubling)} par {Declarer}";

    public static string DoublingText(Doubling doubling) => doubling switch
    {
        Doubling.None => string.Empty,
        Doubling.Doubled => " X",
        Doubling.Redoubled => " XX",
        _ => throw new ArgumentOutOfRangeException(nameof(doubling))
    };
}

/// <summary>
/// Extensions pour la classe Seat.
/// </summary>
public static class SeatExtensions
{
    public static Seat Next(this Seat seat) => (Seat)(((int)seat + 1) % 4);
    public static Seat Partner(this Seat seat) => (Seat)(((int)seat + 2) % 4);
    public static bool IsNorthSouth(this Seat seat) => seat is Seat.North or Seat.South;
    public static bool IsEastWest(this Seat seat) => seat is Seat.East or Seat.West;
}

/// <summary>
/// Appel d'enchère (siège + enchère ou passe).
/// </summary>
public readonly record struct AuctionCall(Seat Seat, AuctionAction Action, Bid? Bid = null)
{
    public bool IsPass => Action == AuctionAction.Pass;
    public override string ToString() => Action switch
    {
        AuctionAction.Pass => $"{Seat}: Passe",
        AuctionAction.Bid => $"{Seat}: {Bid}",
        AuctionAction.Double => $"{Seat}: Contre",
        AuctionAction.Redouble => $"{Seat}: Surcontre",
        _ => throw new ArgumentOutOfRangeException()
    };
}

/// <summary>Identifiant et paramètres de la règle SEF ayant produit une enchère.</summary>
public sealed record BidExplanation(
    string RuleId,
    IReadOnlyDictionary<string, string> Values,
    string Source = "SEF.txt");

/// <summary>
/// Carte jouée (siège + carte).
/// </summary>
public readonly record struct PlayedCard(Seat Seat, Card Card);

/// <summary>
/// Évaluation d'une main.
/// </summary>
public sealed class HandEvaluation
{
    public HandEvaluation(IReadOnlyList<Card> cards)
    {
        Cards = cards;
        HighCardPoints = cards.Sum(card => card.Rank switch
        {
            Rank.Ace => 4,
            Rank.King => 3,
            Rank.Queen => 2,
            Rank.Jack => 1,
            _ => 0
        });
    }

    public IReadOnlyList<Card> Cards { get; }
    public int HighCardPoints { get; }
    public int LengthPoints => Enum.GetValues<Suit>().Sum(suit => Math.Max(0, SuitLength(suit) - 4));
    public int HonorLengthPoints => HighCardPoints + LengthPoints;
    public int AceCount => Cards.Count(card => card.Rank == Rank.Ace);
    public int KingCount => Cards.Count(card => card.Rank == Rank.King);

    public int SuitLength(Suit suit) => Cards.Count(card => card.Suit == suit);

    public int DistributionPoints => Enum.GetValues<Suit>()
        .Sum(suit => Math.Max(0, 3 - SuitLength(suit)));

    public int TotalPoints => HighCardPoints + DistributionPoints;

    public bool IsBalanced => Enum.GetValues<Suit>().All(suit => SuitLength(suit) is >= 2 and <= 5);

    // SEF : ni chicane ni singleton, pas deux doubletons, pas de majeure cinquième.
    public bool IsNoTrumpOpeningShape => IsBalanced &&
        SuitLength(Suit.Hearts) < 5 && SuitLength(Suit.Spades) < 5 &&
        Enum.GetValues<Suit>().Count(suit => SuitLength(suit) == 2) <= 1;

    public bool HasFourCardMajor => SuitLength(Suit.Hearts) == 4 || SuitLength(Suit.Spades) == 4;

    public Suit LongestSuit => Enum.GetValues<Suit>()
        .OrderByDescending(SuitLength)
        .ThenByDescending(suit => suit)
        .First();
}
