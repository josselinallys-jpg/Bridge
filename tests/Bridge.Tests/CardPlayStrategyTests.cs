using Bridge.AI.CardPlay;
using Bridge.AI.Bidding;
using Bridge.Core.Models;
using Bridge.Core.Strategies;
using Bridge.Engine;

namespace Bridge.Tests;

public sealed class CardPlayStrategyTests
{
    private readonly AdvancedCardPlayAi _strategy = new();

    [Fact]
    public void OpeningLead_UsesTopOfHonorSequence()
    {
        var game = Game(Seat.East, null,
            Hand(Seat.East, "SK", "SQ", "S6", "H8", "H3"));

        Assert.Equal(Card("SK"), _strategy.ChooseCard(game, Seat.East));
    }

    [Fact]
    public void OpeningLead_UsesFourthBestAtNoTrump()
    {
        var game = Game(Seat.East, null,
            Hand(Seat.East, "SA", "SJ", "S8", "S4", "S2", "H9"));

        Assert.Equal(Card("S4"), _strategy.ChooseCard(game, Seat.East));
    }

    [Fact]
    public void Declarer_LeadsFourthBestFromThePlannedLongestSuit()
    {
        var game = Game(Seat.North, null,
            Hand(Seat.North, "SA", "SJ", "S8", "S4", "S2", "HK", "H3"));

        Assert.Equal(Card("S4"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Discard_PreservesAnAvailableTopHonor()
    {
        var game = Game(Seat.North, Suit.Spades,
            Hand(Seat.North, "HK", "H3", "D2"));
        game.Current.Add(new PlayedCard(Seat.East, Card("CA")));
        game.Current.Add(new PlayedCard(Seat.South, Card("C2")));
        game.Current.Add(new PlayedCard(Seat.West, Card("CQ")));

        Assert.Equal(Card("D2"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Declarer_TakesFinesseWithLowerHonorOfTenace()
    {
        var game = Game(Seat.South, null,
            Hand(Seat.South, "SA", "SQ", "S3"),
            Hand(Seat.North, "S2"));
        game.Current.Add(new PlayedCard(Seat.North, Card("S2")));
        game.Current.Add(new PlayedCard(Seat.East, Card("S5")));

        Assert.Equal(Card("SQ"), _strategy.ChooseCard(game, Seat.South));
    }

    [Fact]
    public void Declarer_HoldsUpWhenOpponentLeadsBeforeTheFinesse()
    {
        var game = Game(Seat.South, null,
            Hand(Seat.South, "SA", "SQ", "S3"),
            Hand(Seat.North, "S2"));
        game.Current.Add(new PlayedCard(Seat.East, Card("SK")));

        Assert.Equal(Card("S3"), _strategy.ChooseCard(game, Seat.South));
    }

    [Fact]
    public void Declarer_RuffsWithShortHandBeforeDrawingTrump()
    {
        var game = Game(Seat.North, Suit.Hearts,
            Hand(Seat.North, "H3", "S2", "D7"),
            Hand(Seat.South, "H2", "H4", "S3", "S4", "S5"));

        Assert.Equal(Card("S2"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Declarer_DoesNotDrawTrumpBeforeDevelopingWhenSureWinnersAreInsufficient()
    {
        var game = Game(Seat.North, Suit.Hearts,
            Hand(Seat.North, "HA", "HK", "H9", "S4", "S5", "S6", "S7"),
            Hand(Seat.South, "HQ", "HJ", "H10", "H8", "H7", "S8"));
        game.AuctionCalls = [
            new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(4, Denomination.Hearts)),
            new AuctionCall(Seat.East, AuctionAction.Pass),
            new AuctionCall(Seat.South, AuctionAction.Pass),
            new AuctionCall(Seat.West, AuctionAction.Pass)];

        Assert.Equal(Card("S4"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Declarer_PrefersADevelopingSuitWithDummyLengthAsAnEntry()
    {
        var game = Game(Seat.North, null,
            Hand(Seat.North, "S4", "S5", "S6", "H2", "D2"),
            Hand(Seat.South, "S7", "S8", "S9", "HA", "HK"));

        Assert.Equal(Card("S4"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Defender_CoversHonorWithHonor()
    {
        var game = Game(Seat.East, Suit.Spades,
            Hand(Seat.East, "HK", "H2"));
        game.Current.Add(new PlayedCard(Seat.North, Card("HQ")));

        Assert.Equal(Card("HK"), _strategy.ChooseCard(game, Seat.East));
    }

    [Fact]
    public void Defender_ThirdHandPlaysHighEnoughToWin()
    {
        var game = Game(Seat.West, null,
            Hand(Seat.West, "SK", "S5"));
        game.Current.Add(new PlayedCard(Seat.East, Card("S2")));
        game.Current.Add(new PlayedCard(Seat.South, Card("S9")));

        Assert.Equal(Card("SK"), _strategy.ChooseCard(game, Seat.West));
    }

    [Fact]
    public void Defender_ThirdHandKeepsKingWhenOpponentAceAlreadyWins()
    {
        var game = Game(Seat.North, Suit.Spades,
            Hand(Seat.North, "DK", "D7", "D4"));
        game.Current.Add(new PlayedCard(Seat.South, Card("DQ")));
        game.Current.Add(new PlayedCard(Seat.West, Card("DA")));

        Assert.Equal(Card("D4"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Declarer_UsesBathCoupAgainstKingLead()
    {
        var game = Game(Seat.North, null,
            Hand(Seat.North, "SA", "SJ", "S5"));
        game.Current.Add(new PlayedCard(Seat.East, Card("SK")));

        Assert.Equal(Card("S5"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Declarer_DoesNotHoldUpWhenDummyLedTheSuit()
    {
        var game = Game(Seat.North, null,
            Hand(Seat.North, "SA", "S8", "S3"));
        game.Current.Add(new PlayedCard(Seat.South, Card("SQ")));
        game.Current.Add(new PlayedCard(Seat.East, Card("SK")));

        Assert.Equal(Card("SA"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Defender_UnblocksHonorUnderPartnerWinner()
    {
        var game = Game(Seat.West, null,
            Hand(Seat.West, "SK", "S2"));
        game.Current.Add(new PlayedCard(Seat.East, Card("SA")));
        game.Current.Add(new PlayedCard(Seat.South, Card("S4")));

        Assert.Equal(Card("SK"), _strategy.ChooseCard(game, Seat.West));
    }

    [Fact]
    public void Defender_EncouragesWithHighSpotWhenPartnerWins()
    {
        var game = Game(Seat.West, null,
            Hand(Seat.West, "SQ", "S9", "S3"));
        game.Current.Add(new PlayedCard(Seat.East, Card("SK")));
        game.Current.Add(new PlayedCard(Seat.South, Card("S4")));

        Assert.Equal(Card("S9"), _strategy.ChooseCard(game, Seat.West));
    }

    [Fact]
    public void Defender_DoesNotWasteTrumpWhenOpponentTrumpAlreadyWins()
    {
        var game = Game(Seat.North, Suit.Spades,
            Hand(Seat.North, "S2", "D7"));
        game.Current.Add(new PlayedCard(Seat.North, Card("CA")));
        game.Current.Add(new PlayedCard(Seat.South, Card("C3")));
        game.Current.Add(new PlayedCard(Seat.West, Card("SK")));

        Assert.Equal(Card("D7"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Declarer_DoesNotRuffWhenDummyAlreadyWinsTheTrick()
    {
        var game = Game(Seat.North, Suit.Spades,
            Hand(Seat.North, "S2", "D7"));
        game.Current.Add(new PlayedCard(Seat.South, Card("CA")));
        game.Current.Add(new PlayedCard(Seat.West, Card("C3")));
        game.Current.Add(new PlayedCard(Seat.East, Card("C2")));

        Assert.Equal(Card("D7"), _strategy.ChooseCard(game, Seat.North));
    }

    [Fact]
    public void Defender_UsesSmallestTrumpThatCanWinTheTrick()
    {
        var game = Game(Seat.West, Suit.Hearts,
            Hand(Seat.West, "H2", "HQ", "D7"));
        game.Current.Add(new PlayedCard(Seat.North, Card("CA")));
        game.Current.Add(new PlayedCard(Seat.South, Card("C3")));

        Assert.Equal(Card("H2"), _strategy.ChooseCard(game, Seat.West));
    }

    [Fact]
    public void OpeningLead_PrioritizesPartnersNaturalSuit()
    {
        var game = Game(Seat.East, null,
            Hand(Seat.East, "D8", "D3", "S9", "S7", "S5", "S2"));
        game.AuctionCalls =
        [
            new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.Spades)),
            new AuctionCall(Seat.East, AuctionAction.Pass),
            new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(2, Denomination.Spades)),
            new AuctionCall(Seat.West, AuctionAction.Bid, new Bid(3, Denomination.Diamonds))
        ];

        Assert.Equal(Card("D3"), _strategy.ChooseCard(game, Seat.East));
    }

    [Fact]
    public void OpeningLead_AvoidsDeclarersBidSuitForUnbidSuit()
    {
        var game = Game(Seat.East, null,
            Hand(Seat.East, "SK", "SQ", "S4", "C9", "C6", "C2"));
        game.AuctionCalls =
        [
            new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.Spades)),
            new AuctionCall(Seat.East, AuctionAction.Pass),
            new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(2, Denomination.Spades)),
            new AuctionCall(Seat.West, AuctionAction.Pass)
        ];

        Assert.Equal(Card("C2"), _strategy.ChooseCard(game, Seat.East));
    }

    [Fact]
    public void OpeningLead_InterpretsPartnersTexasAsTransferredSuit()
    {
        var game = Game(Seat.East, null,
            Hand(Seat.East, "H8", "H3", "C9", "C7", "C5", "C2"));
        game.AuctionCalls =
        [
            new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(1, Denomination.Clubs)),
            new AuctionCall(Seat.East, AuctionAction.Bid, new Bid(1, Denomination.NoTrump)),
            new AuctionCall(Seat.South, AuctionAction.Bid, new Bid(2, Denomination.Clubs)),
            new AuctionCall(Seat.West, AuctionAction.Bid, new Bid(2, Denomination.Diamonds)),
            new AuctionCall(Seat.North, AuctionAction.Bid, new Bid(3, Denomination.Clubs))
        ];

        Assert.Equal(Card("H3"), _strategy.ChooseCard(game, Seat.East));
    }

    [Fact]
    public void AdvancedStrategy_CompletesFullDealsWithOnlyLegalCards()
    {
        var random = new Random(1847);
        for (var dealIndex = 0; dealIndex < 20; dealIndex++)
        {
            var deal = Deal.Create(random);
            var contract = new Contract(
                new Bid(1 + dealIndex % 4, (Denomination)(dealIndex % 5)),
                (Seat)(dealIndex % 4));
            var game = new CardPlayEngine(deal, contract);

            while (!game.IsComplete)
            {
                var seat = game.NextToPlay;
                var legalCards = game.LegalCards(seat);
                var card = _strategy.ChooseCard(game, seat);
                Assert.Contains(card, legalCards);
                game.Play(seat, card);
            }

            Assert.Equal(13, game.DeclarerTricks + game.DefenderTricks);
        }
    }

    [Fact]
    public void AnalyzeDeal_PreservesInitialDealAndReturnsCompleteAiLine()
    {
        var deal = Deal.Create(new Random(42));
        var before = Enum.GetValues<Seat>().ToDictionary(seat => seat, seat => deal[seat].ToList());
        var gameEngine = new BridgeGameEngine(new SefBiddingEngine(), _strategy);

        var analysis = gameEngine.AnalyzeDeal(
            deal,
            new Contract(new Bid(3, Denomination.NoTrump), Seat.North));

        Assert.Equal(52, analysis.Plays.Count);
        Assert.Equal(13, analysis.DeclarerTricks + analysis.DefenderTricks);
        foreach (var seat in Enum.GetValues<Seat>())
            Assert.Equal(before[seat], deal[seat]);
    }

    [Fact]
    public void AnalyzeFullDeal_ReplaysAuctionAndResultFromInitialDeal()
    {
        var deal = Deal.Create(new Random(2026));
        var gameEngine = new BridgeGameEngine(new SefBiddingEngine(), _strategy);

        var analysis = gameEngine.AnalyzeFullDeal(deal);

        Assert.True(analysis.AuctionCalls.Count >= 4);
        var auction = new Auction();
        foreach (var call in analysis.AuctionCalls)
            auction.AddCall(call);
        Assert.True(auction.IsComplete);
        Assert.Equal(auction.GetContract(), analysis.Contract);
        if (analysis.Contract.HasValue)
        {
            Assert.NotNull(analysis.CardPlay);
            Assert.Equal(52, analysis.CardPlay!.Plays.Count);
        }
    }

    private static FakeCardPlayEngine Game(Seat nextToPlay, Suit? trump, params (Seat Seat, Card[] Cards)[] hands)
    {
        var game = new FakeCardPlayEngine(nextToPlay, Seat.North, trump);
        foreach (var (seat, cards) in hands)
            game.Hands[seat] = cards.ToList();
        return game;
    }

    private static (Seat, Card[]) Hand(Seat seat, params string[] cards) =>
        (seat, cards.Select(Card).ToArray());

    private static Card Card(string value)
    {
        var suit = value[0] switch
        {
            'C' => Suit.Clubs,
            'D' => Suit.Diamonds,
            'H' => Suit.Hearts,
            'S' => Suit.Spades,
            _ => throw new ArgumentException("Couleur inconnue.", nameof(value))
        };
        var rank = value[1..] switch
        {
            "A" => Rank.Ace,
            "K" => Rank.King,
            "Q" => Rank.Queen,
            "J" => Rank.Jack,
            "10" => Rank.Ten,
            _ => (Rank)int.Parse(value[1..])
        };
        return new Card(suit, rank);
    }

    private sealed class FakeCardPlayEngine : ICardPlayEngine
    {
        public FakeCardPlayEngine(Seat nextToPlay, Seat declarer, Suit? trump)
        {
            NextToPlay = nextToPlay;
            Declarer = declarer;
            Trump = trump;
            foreach (var seat in Enum.GetValues<Seat>())
                Hands[seat] = [];
        }

        public Dictionary<Seat, List<Card>> Hands { get; } = [];
        public List<PlayedCard> Current { get; } = [];
        public List<PlayedCard> History { get; } = [];
        public Seat Declarer { get; }
        public Suit? Trump { get; }
        public Seat NextToPlay { get; }
        public int DeclarerTricks => 0;
        public int DefenderTricks => 0;
        public int TrickNumber => History.Count / 4 + 1;
        public bool IsComplete => false;
        public IReadOnlyList<PlayedCard> CurrentTrick => Current;
        public IReadOnlyList<PlayedCard> PlayedCards => History;
        public IReadOnlyList<AuctionCall> AuctionCalls { get; set; } = [];
        public IReadOnlyList<Card> Hand(Seat seat) => Hands[seat];

        public IReadOnlyList<Card> LegalCards(Seat seat)
        {
            var hand = Hands[seat];
            if (Current.Count == 0)
                return hand;
            var leadSuit = Current[0].Card.Suit;
            var following = hand.Where(card => card.Suit == leadSuit).ToList();
            return following.Count > 0 ? following : hand;
        }

        public Seat Play(Seat seat, Card card) => throw new NotSupportedException();
        public Suit? GetTrumpSuit() => Trump;
        public Seat GetDeclarer() => Declarer;
    }
}
