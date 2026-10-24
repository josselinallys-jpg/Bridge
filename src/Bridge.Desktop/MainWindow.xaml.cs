using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bridge.AI.Bidding;
using Bridge.AI.CardPlay;
using Bridge.Core.Logging;
using Bridge.Core.Models;
using Bridge.Engine;

namespace Bridge.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly Random _random = new();
    private readonly ILogger _logger;
    private readonly SefBiddingEngine _biddingEngine;
    private readonly BridgeGameEngine _bridgeEngine;
    private Deal? _deal;
    private Deal? _initialDeal;
    private Auction? _auction;
    private CardPlayEngine? _cardGame;
    private Contract? _contract;
    private AiDealAnalysis? _aiAnalysis;
    private TaskCompletionSource<bool>? _replayGate;
    private Seat _currentBidder;
    private int _dealVersion;
    private int _boardNumber;
    private int _northSouthScore;
    private Vulnerability _vulnerability;
    private bool _isBusy;
    private bool _southRevealed;
    private bool _openCards;
    private bool _waitingReplayClick;
    private string _statusText = "Préparation de la table…";
    private string _contractText = "—";
    private string _turnText = "Distribution";
    private string _resultText = string.Empty;
    private string? _northTrick;
    private string? _eastTrick;
    private string? _southTrick;
    private string? _westTrick;
    private GamePhase _phase = GamePhase.Bidding;

    public MainWindow()
    {
        InitializeComponent();
        _logger = new ConsoleFileLogger("logs", LogLevel.Information);
        LoggerProvider.SetLogger(_logger);
        _biddingEngine = new SefBiddingEngine(_logger, BiddingProfile.LoadForApplication());
        _bridgeEngine = new BridgeGameEngine(
            _biddingEngine,
            new ContractAwareCardPlayAi(_logger),
            _logger);

        CreateBidOptions();
        DataContext = this;
        Loaded += async (_, _) => await StartNewDealAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CardItem> NorthCards { get; } = [];
    public ObservableCollection<CardItem> SouthCards { get; } = [];
    public ObservableCollection<CardItem> EastCards { get; } = [];
    public ObservableCollection<CardItem> WestCards { get; } = [];
    public ObservableCollection<BidOption> BidOptions { get; } = [];
    public ObservableCollection<AuctionItem> AuctionCalls { get; } = [];
    public ObservableCollection<string> Activity { get; } = [];

    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
    public string ContractText { get => _contractText; private set => SetField(ref _contractText, value); }
    public string TurnText { get => _turnText; private set => SetField(ref _turnText, value); }
    public string ResultText { get => _resultText; private set => SetField(ref _resultText, value); }
    public string ScoreText => _northSouthScore.ToString("+#;-#;0");
    public string VulnerabilityText => $"{_boardNumber} · {VulnerabilityName(_vulnerability)}";
    public string EastCountText => CountText(Seat.East);
    public string WestCountText => CountText(Seat.West);
    public string TrickNumberText => _cardGame is null ? "TABLE" : $"LEVÉE {_cardGame.TrickNumber}";
    public int DeclarerTricks => _cardGame?.DeclarerTricks ?? 0;
    public int DefenderTricks => _cardGame?.DefenderTricks ?? 0;
    public string? NorthTrick { get => _northTrick; private set => SetField(ref _northTrick, value); }
    public string? EastTrick { get => _eastTrick; private set => SetField(ref _eastTrick, value); }
    public string? SouthTrick { get => _southTrick; private set => SetField(ref _southTrick, value); }
    public string? WestTrick { get => _westTrick; private set => SetField(ref _westTrick, value); }
    public Visibility BiddingVisibility => _phase == GamePhase.Bidding && _currentBidder == Seat.North && !_isBusy
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility ResultVisibility => _phase == GamePhase.Complete ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OpenCardsVisibility => _openCards ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ClosedCardsVisibility => _openCards ? Visibility.Collapsed : Visibility.Visible;
    public bool CanReplay => _phase == GamePhase.Complete && _aiAnalysis is not null &&
        (!_isBusy || _waitingReplayClick);
    public string ReplayButtonText => _waitingReplayClick ? "Continuer" : "Revoir le replay IA";
    public bool CanDouble => !_isBusy && _phase == GamePhase.Bidding && _currentBidder == Seat.North &&
        (_auction?.CanDouble(Seat.North) ?? false);
    public bool CanRedouble => !_isBusy && _phase == GamePhase.Bidding && _currentBidder == Seat.North &&
        (_auction?.CanRedouble(Seat.North) ?? false);

    private async void NewDeal_Click(object sender, RoutedEventArgs e) => await StartNewDealAsync();

    private async void Pass_Click(object sender, RoutedEventArgs e) => await SubmitHumanBidAsync(null);

    private async void Double_Click(object sender, RoutedEventArgs e) => await SubmitHumanActionAsync(AuctionAction.Double);

    private async void Redouble_Click(object sender, RoutedEventArgs e) => await SubmitHumanActionAsync(AuctionAction.Redouble);

    private async void ReplayAi_Click(object sender, RoutedEventArgs e)
    {
        if (_waitingReplayClick)
        {
            _waitingReplayClick = false;
            _replayGate?.TrySetResult(true);
            OnPropertyChanged(nameof(ReplayButtonText));
            OnPropertyChanged(nameof(CanReplay));
            return;
        }

        await ReplayAiLineAsync();
    }

    private async void Bid_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BidOption option } && option.IsEnabled)
        {
            await SubmitHumanBidAsync(option.Bid);
        }
    }

    private async void Card_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _phase != GamePhase.Playing || _cardGame is null ||
            sender is not Button { Tag: CardItem { Card: { } card } item } || !item.IsPlayable)
        {
            return;
        }

        var version = _dealVersion;
        var seat = _cardGame.NextToPlay;
        _isBusy = true;
        RefreshView();
        await PlayCardAsync(seat, card, version);
        if (version == _dealVersion && _phase == GamePhase.Playing)
        {
            await ProcessAiPlayAsync(version);
        }
    }

    private async Task StartNewDealAsync()
    {
        _replayGate?.TrySetResult(false);
        _waitingReplayClick = false;
        var version = ++_dealVersion;
        _boardNumber++;
        _vulnerability = VulnerabilityExtensions.ForBoard(_boardNumber);
        _biddingEngine.Vulnerability = _vulnerability;
        _deal = Deal.Create(_random);
        _initialDeal = _deal.Clone();
        _auction = new Auction();
        _cardGame = null;
        _contract = null;
        _aiAnalysis = null;
        _currentBidder = Seat.North;
        _phase = GamePhase.Bidding;
        _isBusy = false;
        _southRevealed = false;
        _openCards = false;
        ContractText = "—";
        ResultText = string.Empty;
        TurnText = "Nord ouvre";
        Activity.Clear();
        AuctionCalls.Clear();
        ClearTrick();
        AddActivity($"Donne {_boardNumber}. Vulnérabilité : {VulnerabilityName(_vulnerability)}.");
        LogDealHands(_deal);
        StatusText = "À vous d’enchérir. Sélectionnez un contrat ou passez.";
        OnPropertyChanged(nameof(VulnerabilityText));
        RefreshView();
        await Task.CompletedTask;

        if (version != _dealVersion)
        {
            return;
        }
    }

    private async Task SubmitHumanBidAsync(Bid? bid)
    {
        if (_isBusy || _phase != GamePhase.Bidding || _currentBidder != Seat.North || _auction is null)
        {
            return;
        }

        if (bid.HasValue && !_auction.IsLegal(bid.Value))
        {
            StatusText = "Cette enchère ne dépasse pas le contrat actuel.";
            return;
        }
        if (!bid.HasValue && _auction.ForcingFor(Seat.North) != AuctionForcing.None)
        {
            StatusText = "Cette séquence est forcing : vous devez enchérir.";
            return;
        }

        _isBusy = true;
        AddAuctionCall(new AuctionCall(Seat.North, bid.HasValue ? AuctionAction.Bid : AuctionAction.Pass, bid));
        _currentBidder = _currentBidder.Next();
        RefreshView();
        await ProcessAiAuctionAsync(_dealVersion);
    }

    private async Task SubmitHumanActionAsync(AuctionAction action)
    {
        if (_isBusy || _phase != GamePhase.Bidding || _currentBidder != Seat.North || _auction is null)
        {
            return;
        }
        if (_auction.ForcingFor(Seat.North) != AuctionForcing.None)
        {
            StatusText = "Cette séquence est forcing : vous devez enchérir.";
            return;
        }
        if (
            action == AuctionAction.Double && !_auction.CanDouble(Seat.North) ||
            action == AuctionAction.Redouble && !_auction.CanRedouble(Seat.North))
        {
            return;
        }

        _isBusy = true;
        AddAuctionCall(new AuctionCall(Seat.North, action));
        _currentBidder = _currentBidder.Next();
        RefreshView();
        await ProcessAiAuctionAsync(_dealVersion);
    }

    private async Task ProcessAiAuctionAsync(int version)
    {
        if (_auction is null || _deal is null)
        {
            return;
        }

        while (!_auction.IsComplete && _currentBidder != Seat.North && version == _dealVersion)
        {
            TurnText = $"{SeatName(_currentBidder)} réfléchit";
            StatusText = $"{SeatName(_currentBidder)} analyse sa main…";
            await Task.Delay(380);
            if (version != _dealVersion)
            {
                return;
            }

            var call = _bridgeEngine.DecideCall(_currentBidder, _deal[_currentBidder], _auction);
            AddAuctionCall(call);
            _currentBidder = _currentBidder.Next();
            RefreshView();
        }

        if (version != _dealVersion)
        {
            return;
        }

        if (_auction.IsComplete)
        {
            await StartCardPlayAsync(version);
            return;
        }

        _isBusy = false;
        TurnText = "À vous";
        StatusText = "À vous d’enchérir.";
        RefreshView();
    }

    private async Task StartCardPlayAsync(int version)
    {
        if (_auction is null || _deal is null)
        {
            return;
        }

        _contract = _auction.GetContract();
        if (_contract is null)
        {
            _phase = GamePhase.Complete;
            _isBusy = false;
            ContractText = "Passe général";
            ResultText = "Les quatre joueurs ont passé. La donne est annulée.";
            StatusText = "Passe général. Lancez une nouvelle donne.";
            TurnText = "Donne annulée";
            AddActivity("Passe général.");
            RefreshView();
            return;
        }

        ContractText = $"{_contract.Value.Bid}{Contract.DoublingText(_contract.Value.Doubling)} · {SeatName(_contract.Value.Declarer)}";
        _cardGame = new CardPlayEngine(_deal, _contract.Value, _logger, _auction.Calls);
        _phase = GamePhase.Playing;
        _isBusy = true;
        AddActivity($"Contrat {_contract.Value.Bid} par {SeatName(_contract.Value.Declarer)}.");
        RefreshView();
        await ProcessAiPlayAsync(version);
    }

    private async Task ProcessAiPlayAsync(int version)
    {
        if (version != _dealVersion || _cardGame is null)
        {
            return;
        }

        while (version == _dealVersion && !_cardGame.IsComplete && !IsHumanControlled(_cardGame.NextToPlay))
        {
            var seat = _cardGame.NextToPlay;
            TurnText = $"{SeatName(seat)} joue";
            StatusText = $"{SeatName(seat)} choisit une carte…";
            RefreshView();
            await Task.Delay(430);
            if (version != _dealVersion)
            {
                return;
            }

            var card = _bridgeEngine.ChooseCard(_cardGame, seat);
            await PlayCardAsync(seat, card, version);
        }

        if (version != _dealVersion || _phase != GamePhase.Playing || _cardGame.IsComplete)
        {
            return;
        }

        _isBusy = false;
        var controlledSeat = _cardGame.NextToPlay;
        TurnText = controlledSeat == Seat.North ? "À vous · Nord" : "À vous · Sud";
        StatusText = controlledSeat == Seat.North
            ? "Choisissez une carte éclairée dans votre main."
            : "Jouez une carte de Sud, affichée en haut de la table.";
        RefreshView();
    }

    private async Task PlayCardAsync(Seat seat, Card card, int version)
    {
        if (_cardGame is null)
        {
            return;
        }

        var displayedTrick = _cardGame.CurrentTrick.Append(new PlayedCard(seat, card)).ToList();
        var completesTrick = displayedTrick.Count == 4;
        var winner = _cardGame.Play(seat, card);
        AddActivity($"{SeatName(seat)} joue {card}.");

        if (!_southRevealed && _contract?.Declarer.IsNorthSouth() == true)
        {
            _southRevealed = true;
        }

        SetTrick(displayedTrick);
        RefreshHands();
        NotifyGameState();

        if (completesTrick)
        {
            AddActivity($"Pli pour {SeatName(winner)}.");
            TurnText = $"Pli pour {SeatName(winner)}";
            await Task.Delay(650);
            if (version != _dealVersion)
            {
                return;
            }

            SetTrick(_cardGame.CurrentTrick);
        }

        if (_cardGame.IsComplete)
        {
            await CompleteDealAsync();
        }
    }

    private async Task CompleteDealAsync()
    {
        if (_cardGame is null || _contract is null)
        {
            return;
        }

        var made = _cardGame.DeclarerTricks >= _contract.Value.RequiredTricks;
        var points = BridgeScoring.Score(_contract.Value, _cardGame.DeclarerTricks, _vulnerability);
        var northSouthDeclared = _contract.Value.Declarer.IsNorthSouth();
        var scoreDelta = northSouthDeclared
            ? (made ? points : -points)
            : (made ? -points : points);
        _northSouthScore += scoreDelta;
        _aiAnalysis = _initialDeal is null
            ? null
            : _bridgeEngine.AnalyzeFullDeal(_initialDeal);
        var comparison = AiResultText(_aiAnalysis, _vulnerability);
        _phase = GamePhase.Complete;
        _isBusy = false;
        ResultText = made
            ? $"Contrat réussi avec {_cardGame.DeclarerTricks} levées. Score de la donne : {scoreDelta:+#;-#;0}. {comparison}"
            : $"Contrat chuté de {_contract.Value.RequiredTricks - _cardGame.DeclarerTricks}. Score de la donne : {scoreDelta:+#;-#;0}. {comparison}";
        StatusText = "Donne terminée.";
        TurnText = made ? "Contrat réussi" : "Contrat chuté";
        AddActivity(ResultText);
        OnPropertyChanged(nameof(ScoreText));
        RefreshView();

        var version = _dealVersion;
        await Task.Delay(1400);
        if (version == _dealVersion && !_isBusy)
            await ReplayAiLineAsync();
    }

    private async Task ReplayAiLineAsync()
    {
        if (_aiAnalysis is null || _initialDeal is null || _isBusy)
            return;

        var version = _dealVersion;
        _isBusy = true;
        _openCards = true;
        _southRevealed = true;
        _deal = _initialDeal.Clone();
        _cardGame = null;
        _contract = _aiAnalysis.Contract;
        AuctionCalls.Clear();
        ClearTrick();
        ContractText = "—";
        AddActivity("Replay des enchères IA à quatre mains ouvertes.");
        RefreshView();

        var replayCalls = new List<AuctionCall>();
        foreach (var call in _aiAnalysis.AuctionCalls)
        {
            if (version != _dealVersion)
                return;

            var text = CallText(call);
            AuctionCalls.Add(new AuctionItem(SeatName(call.Seat), text, CallBrush(call),
                ExplainCallText(call, replayCalls, _initialDeal[call.Seat])));
            replayCalls.Add(call);
            TurnText = $"{SeatName(call.Seat)} · {text}";
            StatusText = $"Enchères IA : {SeatName(call.Seat)} annonce {text}.";
            await Task.Delay(280);
        }

        ContractText = _aiAnalysis.Contract.HasValue
            ? $"{_aiAnalysis.Contract.Value.Bid}{Contract.DoublingText(_aiAnalysis.Contract.Value.Doubling)} · {SeatName(_aiAnalysis.Contract.Value.Declarer)}"
            : "Passe général";
        TurnText = "Enchères IA terminées";
        if (!await WaitForReplayClickAsync(version,
                _aiAnalysis.Contract.HasValue
                    ? $"Contrat IA : {_aiAnalysis.Contract}. Cliquez sur Continuer pour jouer les cartes."
                    : "Passe général de l’IA. Cliquez sur Continuer pour terminer le replay."))
            return;

        if (!_aiAnalysis.Contract.HasValue || _aiAnalysis.CardPlay is null)
        {
            FinishReplay("L’IA a passé la donne.");
            return;
        }

        _cardGame = new CardPlayEngine(_deal, _aiAnalysis.Contract.Value, _logger, _aiAnalysis.AuctionCalls);
        var plays = _aiAnalysis.CardPlay.Plays;
        for (var playIndex = 0; playIndex < plays.Count; playIndex += 4)
        {
            Seat winner = default;
            foreach (var play in plays.Skip(playIndex).Take(4))
            {
                if (version != _dealVersion)
                    return;

                var displayedTrick = _cardGame.CurrentTrick.Append(play).ToList();
                winner = _cardGame.Play(play.Seat, play.Card);
                TurnText = $"{SeatName(play.Seat)} · {play.Card}";
                StatusText = $"Ligne IA : {SeatName(play.Seat)} joue {play.Card}.";
                SetTrick(displayedTrick);
                RefreshHands();
                NotifyGameState();
                await Task.Delay(150);
            }

            if (version != _dealVersion)
                return;

            var trickNumber = playIndex / 4 + 1;
            TurnText = $"Pli {trickNumber} · {SeatName(winner)}";
            if (playIndex + 4 < plays.Count &&
                !await WaitForReplayClickAsync(version,
                    $"Pli {trickNumber} remporté par {SeatName(winner)}. Cliquez sur Continuer."))
                return;
            SetTrick(_cardGame.CurrentTrick);
        }

        if (version != _dealVersion)
            return;

        FinishReplay($"L’IA réalise {_aiAnalysis.CardPlay.DeclarerTricks} levées pour le déclarant.");
    }

    private async Task<bool> WaitForReplayClickAsync(int version, string message)
    {
        _waitingReplayClick = true;
        _replayGate = new TaskCompletionSource<bool>();
        StatusText = message;
        OnPropertyChanged(nameof(ReplayButtonText));
        OnPropertyChanged(nameof(CanReplay));
        var shouldContinue = await _replayGate.Task;
        _waitingReplayClick = false;
        _replayGate = null;
        OnPropertyChanged(nameof(ReplayButtonText));
        OnPropertyChanged(nameof(CanReplay));
        return shouldContinue && version == _dealVersion;
    }

    private void FinishReplay(string message)
    {
        _isBusy = false;
        TurnText = "Replay terminé";
        StatusText = message;
        AddActivity(StatusText);
        RefreshView();
    }

    private static string AiResultText(AiDealAnalysis? analysis, Vulnerability vulnerability)
    {
        if (analysis?.Contract is not { } contract || analysis.CardPlay is not { } cardPlay)
            return "Ligne IA : passe général.";

        var made = cardPlay.ContractMade;
        var points = BridgeScoring.Score(contract, cardPlay.DeclarerTricks, vulnerability);
        var northSouthScore = contract.Declarer.IsNorthSouth()
            ? (made ? points : -points)
            : (made ? -points : points);
        return $"Ligne IA : contrat {contract}, {cardPlay.DeclarerTricks} levées déclarant, " +
            $"contrat {(made ? "réussi" : "chuté")}, score Nord-Sud {northSouthScore:+#;-#;0}.";
    }

    private void AddAuctionCall(AuctionCall call)
    {
        if (_auction is null)
        {
            return;
        }

        var previousCalls = _auction.Calls.ToList();
        if (_deal is not null)
            call = _biddingEngine.AnnotateCall(call.Seat, _deal[call.Seat], previousCalls, call);
        _auction.AddCall(call);
        var text = CallText(call);
        AuctionCalls.Add(new AuctionItem(SeatName(call.Seat), text, CallBrush(call),
            ExplainCallText(call, previousCalls, _deal?[call.Seat])));
        AddActivity($"{SeatName(call.Seat)} : {text}.");
    }

    private void LogDealHands(Deal deal)
    {
        foreach (var seat in Enum.GetValues<Seat>())
        {
            var cards = deal[seat]
                .OrderByDescending(card => card.Suit)
                .ThenByDescending(card => card.Rank);
            _logger.Information($"[Desktop] Main {SeatName(seat)} : {string.Join(' ', cards)}");
        }
    }

    private string ExplainCallText(AuctionCall call, IReadOnlyList<AuctionCall> previousCalls,
        IReadOnlyList<Card>? hand)
    {
        if (hand is null)
            return "Enchère non prévue";
        var explanation = _biddingEngine.ExplainCall(call.Seat, hand, previousCalls, call);
        return AuctionTooltipRules.Get(explanation.RuleId, explanation.Values);
    }

    private static string ExplainBid(AuctionCall call, IReadOnlyList<AuctionCall> previousCalls,
        IReadOnlyList<Card>? hand)
    {
        const string unknown = "Enchère non prévue";
        if (call.Action == AuctionAction.Pass)
            return Rule("pass");
        if (call.Action == AuctionAction.Double)
            return Rule("double");
        if (call.Action == AuctionAction.Redouble)
            return Rule("redouble");
        if (call.Bid is not { } bid)
            return unknown;

        var evaluation = hand is null ? null : new HandEvaluation(hand);
        var partnerBids = previousCalls.Where(existing =>
            existing.Action == AuctionAction.Bid && existing.Seat == call.Seat.Partner()).ToList();
        var ownBids = previousCalls.Where(existing =>
            existing.Action == AuctionAction.Bid && existing.Seat == call.Seat).ToList();
        var has4Nt = ownBids.Any(existing => existing.Bid == new Bid(4, Denomination.NoTrump));
        var lastPartnerBid = partnerBids.LastOrDefault().Bid;

        if (!previousCalls.Any(existing => existing.Action == AuctionAction.Bid))
            return ExplainOpening(bid, evaluation) ?? unknown;

        if (ownBids.Count == 0 && partnerBids.Count == 0)
            return ExplainIntervention(bid, evaluation);

        var oneNoTrumpRule = ExplainOneNoTrumpBid(call, bid, previousCalls, evaluation, unknown);
        if (oneNoTrumpRule is not null)
            return oneNoTrumpRule;

        if (bid == new Bid(4, Denomination.NoTrump) && HasAgreedSuit(previousCalls, call.Seat))
            return Rule("blackwood-ask");
        if (has4Nt)
            return bid.Denomination switch
            {
                Denomination.Clubs => Rule("blackwood-0-3"),
                Denomination.Diamonds => Rule("blackwood-1-4"),
                Denomination.Hearts when evaluation is not null && evaluation.Cards.Any(card =>
                    card == new Card(Suit.Hearts, Rank.Queen)) => Rule("blackwood-2-5-queen"),
                Denomination.Hearts => Rule("blackwood-2-5-no-queen"),
                Denomination.Spades => Rule("blackwood-2-5-queen"),
                _ => Rule("slam")
            };
        if (bid == new Bid(1, Denomination.NoTrump) && evaluation is not null && evaluation.IsNoTrumpOpeningShape &&
            evaluation.HighCardPoints is >= 15 and <= 17)
            return Rule("opening-1nt", evaluation);
        if (bid.Level is >= 5 and <= 7 && (has4Nt || HasAgreedSuit(previousCalls, call.Seat)))
            return Rule("slam");
        if (lastPartnerBid is { } partnerBid &&
            partnerBid.Denomination is Denomination.Hearts or Denomination.Spades)
        {
            if (bid.Denomination == partnerBid.Denomination && bid.Level is >= 2 and <= 4)
                return ExplainMajorSupport(bid, evaluation);
            if (bid.Denomination is Denomination.Hearts or Denomination.Spades &&
                bid.Denomination != partnerBid.Denomination && bid.Level is 1 or 2 or 3)
                return ExplainMajorChange(bid, evaluation);
        }
        if (bid.Denomination is Denomination.Hearts or Denomination.Spades && bid.Level == 4 &&
            lastPartnerBid is { Denomination: Denomination.Hearts or Denomination.Spades })
            return Rule("major-support-4", evaluation, ("cartes", "fit"));
        if (bid.Denomination == Denomination.NoTrump && bid.Level is >= 1 and <= 4 &&
            (evaluation?.IsBalanced == true || bid.Level == 4))
            return Rule("nt", evaluation, ("palier", bid.Level.ToString()));
        if (bid.Denomination is Denomination.Clubs or Denomination.Diamonds &&
            bid.Level is 1 or 2 or 3 &&
            lastPartnerBid is { Denomination: Denomination.Clubs or Denomination.Diamonds })
            return Rule("minor-change", evaluation, ("palier", bid.Level.ToString()));
        if (bid.Denomination is Denomination.Hearts or Denomination.Spades && bid.Level is 1 or 2)
            return ExplainMajorChange(bid, evaluation);
        return unknown;
    }

    private static string ExplainIntervention(Bid bid, HandEvaluation? hand)
    {
        if (bid.Level == 1 && bid.Denomination is Denomination.Hearts or Denomination.Spades)
            return Rule("intervention-major", hand,
                ("couleur", SuitName(bid.Denomination)),
                ("cartes", hand?.SuitLength((Suit)bid.Denomination).ToString() ?? "5"));
        if (bid.Level == 1 && bid.Denomination is Denomination.Clubs or Denomination.Diamonds)
            return Rule("intervention-minor", hand, ("couleur", SuitName(bid.Denomination)));
        if (bid.Level == 2 && bid.Denomination != Denomination.NoTrump && hand is not null)
        {
            var suitLength = hand.SuitLength((Suit)bid.Denomination);
            if (suitLength >= 6 && hand.HighCardPoints <= 10)
                return Rule("intervention-jump", hand,
                    ("couleur", SuitName(bid.Denomination)), ("cartes", suitLength.ToString()));
            if (suitLength >= 5 && hand.HonorLengthPoints is >= 9 and <= 18)
                return Rule("intervention-two-level", hand,
                    ("couleur", SuitName(bid.Denomination)), ("cartes", suitLength.ToString()));
        }
        if (bid.Denomination == Denomination.NoTrump && bid.Level is 1 or 2)
            return Rule("intervention-nt", hand, ("palier", bid.Level.ToString()));
        return "Enchère non prévue";
    }

    private static string? ExplainOneNoTrumpBid(AuctionCall call, Bid bid,
        IReadOnlyList<AuctionCall> previousCalls, HandEvaluation? hand, string unknown)
    {
        var opening = previousCalls.FirstOrDefault(existing =>
            existing.Action == AuctionAction.Bid && existing.Bid == new Bid(1, Denomination.NoTrump));
        if (opening.Action != AuctionAction.Bid)
            return null;

        var sideBids = previousCalls.Where(existing =>
            existing.Action == AuctionAction.Bid && existing.Seat.IsNorthSouth() == call.Seat.IsNorthSouth())
            .Select(existing => existing.Bid!.Value).ToList();
        var partnerBids = previousCalls.Where(existing =>
            existing.Action == AuctionAction.Bid && existing.Seat == call.Seat.Partner())
            .Select(existing => existing.Bid!.Value).ToList();
        var ownBids = previousCalls.Where(existing =>
            existing.Action == AuctionAction.Bid && existing.Seat == call.Seat)
            .Select(existing => existing.Bid!.Value).ToList();
        var partnerLast = partnerBids.LastOrDefault();
        var ownLast = ownBids.LastOrDefault();

        if (opening.Seat == call.Seat.Partner() && ownBids.Count == 0 && partnerLast == opening.Bid)
        {
            if (bid == new Bid(2, Denomination.Clubs) && hand is not null && hand.HasFourCardMajor &&
                hand.HonorLengthPoints >= 8)
                return Rule("one-nt-stayman", hand);
            if (bid == new Bid(2, Denomination.Diamonds) && hand is not null && hand.SuitLength(Suit.Hearts) >= 5)
                return Rule("one-nt-texas-hearts", hand, ("cartes", hand.SuitLength(Suit.Hearts).ToString()));
            if (bid == new Bid(2, Denomination.Hearts) && hand is not null && hand.SuitLength(Suit.Spades) >= 5)
                return Rule("one-nt-texas-spades", hand, ("cartes", hand.SuitLength(Suit.Spades).ToString()));
            if (bid == new Bid(2, Denomination.Spades) && hand is not null && hand.SuitLength(Suit.Clubs) >= 5)
                return Rule("one-nt-texas-minor", hand, ("couleur", "Trèfles"), ("cartes", hand.SuitLength(Suit.Clubs).ToString()));
            if (bid == new Bid(3, Denomination.Clubs) && hand is not null && hand.SuitLength(Suit.Diamonds) >= 5)
                return Rule("one-nt-texas-minor", hand, ("couleur", "Carreaux"), ("cartes", hand.SuitLength(Suit.Diamonds).ToString()));
            if (bid.Denomination == Denomination.NoTrump && bid.Level is >= 2 and <= 7)
                return Rule("one-nt-quantitative", hand, ("palier", bid.Level.ToString()));
            if (bid.Denomination is Denomination.Hearts or Denomination.Spades && bid.Level == 3 &&
                hand is not null && hand.SuitLength((Suit)bid.Denomination) >= 6)
                return Rule("one-nt-slam-suit", hand, ("couleur", SuitName(bid.Denomination)));
            if (bid == new Bid(4, Denomination.Diamonds))
                return Rule("one-nt-major-two-suited", hand, ("zone", "6-9H"));
            return unknown;
        }

        if (opening.Seat == call.Seat && ownBids.Count == 1 && ownBids[0] == opening.Bid && partnerBids.Count > 0)
        {
            if (partnerLast == new Bid(2, Denomination.Clubs))
                return bid.Denomination switch
                {
                    Denomination.Diamonds => Rule("stayman-no-major", hand),
                    Denomination.Hearts => Rule("stayman-four-hearts", hand),
                    Denomination.Spades => Rule("stayman-four-spades", hand),
                    Denomination.NoTrump => Rule("stayman-both-majors", hand),
                    _ => unknown
                };
            if (partnerLast == new Bid(2, Denomination.Diamonds))
                return bid == new Bid(2, Denomination.Hearts)
                    ? Rule("one-nt-texas-rectification", hand, ("couleur", "Cœurs")) : unknown;
            if (partnerLast == new Bid(2, Denomination.Hearts))
                return bid == new Bid(2, Denomination.Spades)
                    ? Rule("one-nt-texas-rectification", hand, ("couleur", "Piques")) : unknown;
            if (partnerLast == new Bid(2, Denomination.Spades) && bid == new Bid(3, Denomination.Clubs))
                return Rule("one-nt-texas-rectification", hand, ("couleur", "Trèfles"));
            if (partnerLast == new Bid(3, Denomination.Clubs) && bid == new Bid(3, Denomination.Diamonds))
                return Rule("one-nt-texas-rectification", hand, ("couleur", "Carreaux"));
            if (partnerLast == new Bid(4, Denomination.Diamonds) && bid.Denomination is Denomination.Hearts or Denomination.Spades)
                return Rule("one-nt-major-game-choice", hand, ("couleur", SuitName(bid.Denomination)));
        }

        if (opening.Seat != call.Seat && ownBids.Count > 0 && ownBids[0] != opening.Bid && partnerBids.Count > 0)
        {
            if (ownLast == new Bid(2, Denomination.Clubs) && partnerLast == new Bid(2, Denomination.NoTrump) &&
                bid.Denomination is Denomination.Clubs or Denomination.Diamonds)
                return Rule("stayman-two-nt-transfer", hand, ("couleur", bid.Denomination == Denomination.Clubs ? "Cœurs" : "Piques"));
            if (ownLast == new Bid(2, Denomination.Clubs) && partnerLast.Denomination is Denomination.Hearts or Denomination.Spades &&
                bid.Level == 3 && bid.Denomination is Denomination.Hearts or Denomination.Spades)
                return Rule("stayman-slam-fit", hand, ("couleur", SuitName(bid.Denomination)));
            if (partnerLast.Denomination is Denomination.Hearts or Denomination.Spades &&
                bid.Denomination == partnerLast.Denomination && bid.Level == 3)
                return Rule("texas-major-continuation", hand, ("couleur", SuitName(bid.Denomination)));
            if (partnerLast.Denomination is Denomination.Clubs or Denomination.Diamonds && bid.Level == 3)
                return Rule("texas-minor-continuation", hand, ("couleur", SuitName(bid.Denomination)));
        }

        return null;
    }

    private static string? ExplainOpening(Bid bid, HandEvaluation? hand)
    {
        if (bid.Level == 1 && (bid.Denomination is Denomination.Hearts or Denomination.Spades))
            return Rule("opening-major", hand,
                ("couleur", SuitName(bid.Denomination)),
                ("cartes", hand?.SuitLength((Suit)bid.Denomination).ToString() ?? "5"));
        if (bid.Level == 1 && (bid.Denomination is Denomination.Clubs or Denomination.Diamonds))
            return Rule("opening-minor", hand);
        if (bid == new Bid(1, Denomination.NoTrump))
            return Rule("opening-1nt", hand);
        if (bid == new Bid(2, Denomination.NoTrump))
            return Rule("opening-2nt", hand);
        if (bid.Level == 2 && (bid.Denomination is Denomination.Hearts or Denomination.Spades))
            return Rule("weak-two", hand, ("couleur", SuitName(bid.Denomination)));
        if (bid.Level is 3 or 4)
            return Rule("preempt", hand, ("couleur", SuitName(bid.Denomination)));
        return null;
    }

    private static string ExplainMajorSupport(Bid bid, HandEvaluation? hand)
    {
        var fitValues = hand is null ? Array.Empty<(string, string)>() :
            new[] { ("cartes", hand.SuitLength((Suit)bid.Denomination).ToString()), ("HLD", hand.TotalPoints.ToString()) };
        return bid.Level switch
        {
            2 => Rule("major-support-2", hand, fitValues),
            3 => Rule("major-support-3", hand, fitValues),
            4 => Rule("major-support-4", hand, fitValues),
            _ => Rule("natural", hand)
        };
    }

    private static string ExplainMajorChange(Bid bid, HandEvaluation? hand)
    {
        var length = hand?.SuitLength((Suit)bid.Denomination).ToString() ?? (bid.Level == 2 ? "5" : "4");
        return Rule(bid.Level == 2 ? "major-change-2" : "major-change-1", hand, ("cartes", length));
    }

    private static string ExplainChangeOfColor(Bid bid, HandEvaluation? hand)
    {
        if (bid.Denomination is Denomination.Hearts or Denomination.Spades)
            return Rule(bid.Level == 2 ? "major-change-2" : "major-change-1", hand,
                ("cartes", hand?.SuitLength((Suit)bid.Denomination).ToString() ?? "4"));
        if (bid.Denomination is Denomination.Clubs or Denomination.Diamonds)
            return Rule("minor-change", hand, ("palier", bid.Level.ToString()));
        return Rule("nt", hand, ("palier", bid.Level.ToString()));
    }

    private static string Rule(string key, HandEvaluation? hand = null,
        params (string Key, string Value)[] values)
    {
        var replacements = new Dictionary<string, string>
        {
            ["H"] = hand?.HighCardPoints.ToString() ?? "?",
            ["HL"] = hand?.HonorLengthPoints.ToString() ?? "?",
            ["HLD"] = hand?.TotalPoints.ToString() ?? "?"
        };
        foreach (var (name, value) in values) replacements[name] = value;
        return AuctionTooltipRules.Get(key, replacements);
    }

    private static string SuitName(Denomination denomination) => denomination switch
    {
        Denomination.Spades => "Piques",
        Denomination.Hearts => "Cœurs",
        Denomination.Diamonds => "Carreaux",
        Denomination.Clubs => "Trèfles",
        _ => "Sans-Atout"
    };

    private static bool HasAgreedSuit(IReadOnlyList<AuctionCall> calls, Seat seat) =>
        calls.Where(call => call.Action == AuctionAction.Bid &&
                call.Seat.IsNorthSouth() == seat.IsNorthSouth() && call.Bid is { })
            .GroupBy(call => call.Bid!.Value.Denomination)
            .Any(group => group.Key != Denomination.NoTrump && group.Count() >= 2);

    public string PassRule => ActionRule(AuctionAction.Pass);
    public string DoubleRule => ActionRule(AuctionAction.Double);
    public string RedoubleRule => ActionRule(AuctionAction.Redouble);

    private string ActionRule(AuctionAction action)
    {
        if (_auction is not null && _deal is not null && !_auction.IsComplete)
        {
            if (action == AuctionAction.Double && !_auction.CanDouble(Seat.North))
                return "Contre indisponible dans cette situation.";
            if (action == AuctionAction.Redouble && !_auction.CanRedouble(Seat.North))
                return "Surcontre indisponible dans cette situation.";
            return ExplainCallText(new AuctionCall(Seat.North, action), _auction.Calls, _deal[Seat.North]);
        }
        return AuctionTooltipRules.Get(action switch
        {
            AuctionAction.Pass => "pass", AuctionAction.Double => "double", _ => "redouble"
        });
    }

    private void CreateBidOptions()
    {
        for (var level = 1; level <= 7; level++)
        {
            foreach (var denomination in Enum.GetValues<Denomination>())
            {
                var bid = new Bid(level, denomination);
                BidOptions.Add(new BidOption(bid, bid.ToString(), BidBrush(bid)));
            }
        }
    }

    private void RefreshView()
    {
        RefreshHands();
        UpdateBidOptionRules();
        foreach (var option in BidOptions)
        {
            option.IsEnabled = !_isBusy && _phase == GamePhase.Bidding &&
                _currentBidder == Seat.North && (_auction?.IsLegal(option.Bid) ?? false);
        }

        NotifyGameState();
        OnPropertyChanged(nameof(BiddingVisibility));
        OnPropertyChanged(nameof(ResultVisibility));
        OnPropertyChanged(nameof(OpenCardsVisibility));
        OnPropertyChanged(nameof(ClosedCardsVisibility));
        OnPropertyChanged(nameof(CanReplay));
        OnPropertyChanged(nameof(ReplayButtonText));
        OnPropertyChanged(nameof(CanDouble));
        OnPropertyChanged(nameof(CanRedouble));
        OnPropertyChanged(nameof(PassRule));
        OnPropertyChanged(nameof(DoubleRule));
        OnPropertyChanged(nameof(RedoubleRule));
        ActivityList.ScrollIntoView(Activity.LastOrDefault());
    }

    private void UpdateBidOptionRules()
    {
        if (_auction is null || _deal is null)
        {
            return;
        }

        foreach (var option in BidOptions)
        {
            var call = new AuctionCall(Seat.North, AuctionAction.Bid, option.Bid);
            option.Rule = ExplainCallText(call, _auction.Calls, _deal[Seat.North]);
        }
    }

    private void RefreshHands()
    {
        if (_deal is null)
        {
            return;
        }

        FillHand(NorthCards, Seat.North, true);
        FillHand(SouthCards, Seat.South, _southRevealed || _openCards);
        FillHand(EastCards, Seat.East, _openCards);
        FillHand(WestCards, Seat.West, _openCards);
        OnPropertyChanged(nameof(EastCountText));
        OnPropertyChanged(nameof(WestCountText));
    }

    private void FillHand(ObservableCollection<CardItem> target, Seat seat, bool faceUp)
    {
        if (_deal is null)
        {
            return;
        }

        target.Clear();
        var cards = _deal[seat].OrderByDescending(card => card.Suit).ThenBy(card => card.Rank).ToList();
        var canPlay = !_isBusy && _phase == GamePhase.Playing && _cardGame?.NextToPlay == seat && IsHumanControlled(seat);
        var legalCards = canPlay ? _cardGame!.LegalCards(seat) : [];

        foreach (var card in cards)
        {
            var isPlayable = canPlay && legalCards.Contains(card);
            target.Add(faceUp
                ? new CardItem(card, CardImage(card), isPlayable, canPlay && !isPlayable ? 0.52 : 1)
                : new CardItem(null, "Assets/back.png", false, 1));
        }
    }

    private void NotifyGameState()
    {
        OnPropertyChanged(nameof(DeclarerTricks));
        OnPropertyChanged(nameof(DefenderTricks));
        OnPropertyChanged(nameof(TrickNumberText));
    }

    private void SetTrick(IEnumerable<PlayedCard> cards)
    {
        ClearTrick();
        foreach (var playedCard in cards)
        {
            var path = CardImage(playedCard.Card);
            switch (playedCard.Seat)
            {
                case Seat.North: NorthTrick = path; break;
                case Seat.East: EastTrick = path; break;
                case Seat.South: SouthTrick = path; break;
                case Seat.West: WestTrick = path; break;
            }
        }
    }

    private void ClearTrick()
    {
        NorthTrick = null;
        EastTrick = null;
        SouthTrick = null;
        WestTrick = null;
    }

    private bool IsHumanControlled(Seat seat) => seat == Seat.North ||
        (_contract?.Declarer.IsNorthSouth() == true && seat == Seat.South);

    private string CountText(Seat seat)
    {
        var count = _deal?[seat].Count ?? 0;
        return count == 1 ? "1 carte" : $"{count} cartes";
    }

    private void AddActivity(string message)
    {
        Activity.Add(message);
        _logger.Information($"[Desktop] {message}");
    }

    private static string CardImage(Card card)
    {
        var suit = card.Suit switch
        {
            Suit.Clubs => "C",
            Suit.Diamonds => "D",
            Suit.Hearts => "H",
            Suit.Spades => "S",
            _ => throw new ArgumentOutOfRangeException(nameof(card))
        };
        return $"Assets/{Card.RankText(card.Rank)}{suit}.png";
    }

    private static string SeatName(Seat seat) => seat switch
    {
        Seat.North => "Nord",
        Seat.East => "Est",
        Seat.South => "Sud",
        Seat.West => "Ouest",
        _ => seat.ToString()
    };

    private static string VulnerabilityName(Vulnerability vulnerability) => vulnerability switch
    {
        Vulnerability.None => "Personne",
        Vulnerability.NorthSouth => "Nord-Sud",
        Vulnerability.EastWest => "Est-Ouest",
        Vulnerability.Both => "Tous",
        _ => vulnerability.ToString()
    };

    private static Brush BidBrush(Bid? bid) => bid?.Denomination switch
    {
        Denomination.Hearts or Denomination.Diamonds => Brushes.Firebrick,
        _ => new SolidColorBrush(Color.FromRgb(23, 33, 29))
    };

    private static string CallText(AuctionCall call) => call.Action switch
    {
        AuctionAction.Pass => "Passe",
        AuctionAction.Bid => call.Bid?.ToString() ?? "—",
        AuctionAction.Double => "Contre",
        AuctionAction.Redouble => "Surcontre",
        _ => "—"
    };

    private static Brush CallBrush(AuctionCall call) => call.Action switch
    {
        AuctionAction.Double => Brushes.Firebrick,
        AuctionAction.Redouble => Brushes.DarkGoldenrod,
        _ => BidBrush(call.Bid)
    };

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private enum GamePhase
    {
        Bidding,
        Playing,
        Complete
    }
}

public sealed record CardItem(Card? Card, string ImagePath, bool IsPlayable, double Opacity);

public sealed record AuctionItem(string Seat, string Call, Brush Foreground, string Rule);

public sealed class BidOption : INotifyPropertyChanged
{
    private bool _isEnabled;
    private string _rule = string.Empty;

    public BidOption(Bid bid, string label, Brush foreground)
    {
        Bid = bid;
        Label = label;
        Foreground = foreground;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public Bid Bid { get; }
    public string Label { get; }
    public Brush Foreground { get; }
    public string Rule
    {
        get => _rule;
        set
        {
            if (_rule == value)
            {
                return;
            }

            _rule = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rule)));
        }
    }
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }
}