namespace Bridge.Core.Models;

/// <summary>
/// Représente une donne (distribution des cartes).
/// </summary>
public sealed class Deal
{
    private readonly Dictionary<Seat, List<Card>> _hands;

    private Deal(Dictionary<Seat, List<Card>> hands) => _hands = hands;

    public IReadOnlyList<Card> this[Seat seat] => _hands[seat];

    /// <summary>
    /// Crée une donne aléatoire.
    /// </summary>
    public static Deal Create(Random random)
    {
        var deck = Enum.GetValues<Suit>()
            .SelectMany(suit => Enum.GetValues<Rank>().Select(rank => new Card(suit, rank)))
            .OrderBy(_ => random.Next())
            .ToList();
        
        var hands = Enum.GetValues<Seat>().ToDictionary(seat => seat, _ => new List<Card>());
        for (var index = 0; index < deck.Count; index++)
        {
            hands[(Seat)(index % 4)].Add(deck[index]);
        }

        return new Deal(hands);
    }

    public Deal Clone() => new(_hands.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.ToList()));

    public List<Card> GetMutableHand(Seat seat) => _hands[seat];
}

/// <summary>
/// Représente une enchère (historique des appels).
/// </summary>
public sealed class Auction
{
    private readonly List<AuctionCall> _calls = [];

    public IReadOnlyList<AuctionCall> Calls => _calls;
    public Bid? HighestBid => _calls.LastOrDefault(call => call.Bid.HasValue).Bid;
    public bool IsComplete => HighestBid is null
        ? _calls.Count >= 4 && _calls.TakeLast(4).All(call => call.IsPass)
        : _calls.Count >= 4 && _calls.TakeLast(3).All(call => call.IsPass);
    public bool IsPassedOut => IsComplete && HighestBid is null;

    public bool IsLegal(Bid bid) => bid.Level is >= 1 and <= 7 &&
        (HighestBid is null || Compare(bid, HighestBid.Value) > 0);

    public bool CanDouble(Seat seat)
    {
        if (IsComplete || HighestBid is null)
            return false;

        var lastNonPass = _calls.Last(call => !call.IsPass);
        var declaringSeat = _calls.Last(call => call.Action == AuctionAction.Bid).Seat;
        return lastNonPass.Action == AuctionAction.Bid &&
            seat.IsNorthSouth() != declaringSeat.IsNorthSouth();
    }

    public bool CanRedouble(Seat seat)
    {
        if (IsComplete || HighestBid is null)
            return false;

        var lastNonPass = _calls.Last(call => !call.IsPass);
        var declaringSeat = _calls.Last(call => call.Action == AuctionAction.Bid).Seat;
        return lastNonPass.Action == AuctionAction.Double &&
            seat.IsNorthSouth() == declaringSeat.IsNorthSouth();
    }

    public void AddCall(Seat seat, Bid? bid) =>
        AddCall(new AuctionCall(seat, bid.HasValue ? AuctionAction.Bid : AuctionAction.Pass, bid));

    public void AddCall(Seat seat, AuctionAction action) => AddCall(new AuctionCall(seat, action));

    public void AddCall(AuctionCall call)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Les enchères sont terminées.");
        }

        if (call.Action == AuctionAction.Bid && (!call.Bid.HasValue || !IsLegal(call.Bid.Value)))
        {
            throw new ArgumentException("Cette enchère ne surenchérit pas le contrat actuel.", nameof(call));
        }

        if (call.Action != AuctionAction.Bid && call.Bid.HasValue)
        {
            throw new ArgumentException("Seule une enchère peut contenir un contrat.", nameof(call));
        }

        if (call.Action == AuctionAction.Double && !CanDouble(call.Seat))
        {
            throw new ArgumentException("Ce contre n'est pas légal.", nameof(call));
        }

        if (call.Action == AuctionAction.Redouble && !CanRedouble(call.Seat))
        {
            throw new ArgumentException("Ce surcontre n'est pas légal.", nameof(call));
        }

        _calls.Add(call);
    }

    public Contract? GetContract()
    {
        if (!IsComplete || HighestBid is null)
            return null;

        var winningBid = HighestBid.Value;
        var winningSideIsNorthSouth = _calls.Last(call => call.Bid == winningBid).Seat.IsNorthSouth();
        var declarer = _calls.First(call => call.Bid?.Denomination == winningBid.Denomination &&
            call.Seat.IsNorthSouth() == winningSideIsNorthSouth).Seat;
        var lastNonPass = _calls.Last(call => !call.IsPass);
        var doubling = lastNonPass.Action switch
        {
            AuctionAction.Double => Doubling.Doubled,
            AuctionAction.Redouble => Doubling.Redoubled,
            _ => Doubling.None
        };
        return new Contract(winningBid, declarer, doubling);
    }

    public static int Compare(Bid left, Bid right)
    {
        var levelComparison = left.Level.CompareTo(right.Level);
        return levelComparison != 0
            ? levelComparison
            : left.Denomination.CompareTo(right.Denomination);
    }
}
