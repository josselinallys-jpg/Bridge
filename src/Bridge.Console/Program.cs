using Bridge.Core.Models;
using Bridge.Core.Logging;
using Bridge.AI.Bidding;
using Bridge.AI.CardPlay;
using Bridge.Engine;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Initialiser le logger
var logger = new ConsoleFileLogger("logs", LogLevel.Information);
LoggerProvider.SetLogger(logger);

logger.Information("╔════════════════════════════════════════════════════════════╗");
logger.Information("║         BRIDGE - Joueur humain (Nord) vs 3 IA            ║");
logger.Information("╚════════════════════════════════════════════════════════════╝");

// Configuration du jeu
var random = new Random();
var biddingAi = new SefBiddingEngine(logger, BiddingProfile.LoadForApplication());
var cardPlayAi = new ContractAwareCardPlayAi(logger);
var engine = new BridgeGameEngine(biddingAi, cardPlayAi, logger);

logger.Information("\nBienvenue au Bridge!");
logger.Information("Vous jouez Nord contre Est, Sud et Ouest (IA).");
logger.Information("Chaque contrat joueur = 1 donne. Si vous prenez (Nord/Sud), vous jouez le mort.\n");

int northSouthScore = 0;
int boardNumber = 0;

// Boucle de donnes
while (true)
{
    logger.Information("\n" + new string('─', 60));
    logger.Information("Donne suivante? (O/N): ");
    Console.Write("> ");
    var response = Console.ReadLine()?.Trim().ToUpperInvariant();
    if (response != "O" && response != "YES")
        break;

    PlayDeal();
}

logger.Information($"\nScore final Nord-Sud: {northSouthScore}");
logger.Information("Merci d'avoir joué!");

void PlayDeal()
{
    boardNumber++;
    var vulnerability = VulnerabilityExtensions.ForBoard(boardNumber);
    biddingAi.Vulnerability = vulnerability;
    var deal = Deal.Create(random);
    var auction = new Auction();
    var dealer = Seat.North; // Nord commence toujours
    var currentSeat = dealer;

    logger.Information("\n=== PHASE DES ENCHÈRES ===");
    logger.Information($"Donne {boardNumber} - Vulnérabilité: {FormatVulnerability(vulnerability)}");
    logger.Information("Votre main (Nord):");
    DisplayHand(deal[Seat.North]);

    // Phase des enchères
    while (!auction.IsComplete)
    {
        AuctionCall call;
        if (currentSeat == Seat.North)
        {
            logger.Information($"Enchères actuelles: {FormatAuction(auction)}");
            logger.Information($"{currentSeat} à vous d'enchérir:");
            call = ReadHumanCall(auction, currentSeat);
            call = biddingAi.AnnotateCall(currentSeat, deal[currentSeat], auction.Calls, call);
        }
        else
        {
            call = engine.DecideCall(currentSeat, deal[currentSeat], auction);
            logger.Information(call.ToString());
            logger.Information($"Règle appliquée : {biddingAi.LastDecisionRule}");
        }

        auction.AddCall(call);
        currentSeat = currentSeat.Next();
    }

    var contract = auction.GetContract();
    if (contract is null)
    {
        logger.Warning("Tout le monde a passé - donne annulée.");
        return;
    }

    logger.Information($"\nContrat final: {contract}");
    logger.Information($"Entame: {contract.Value.Declarer.Next()}");

    // Déterminer qui joue
    bool northSouthDeclare = contract.Value.Declarer.IsNorthSouth();
    bool humanPlaysAsDeclarer = contract.Value.Declarer == Seat.North;
    bool humanPlaysAsDummy = contract.Value.Dummy == Seat.North;

    logger.Information("\n=== PHASE DU JEU DE LA CARTE ===");
    if (humanPlaysAsDeclarer)
    {
        logger.Information("Vous jouez le contrat en tant que déclarant (Nord)");
    }
    else if (humanPlaysAsDummy)
    {
        logger.Information($"Votre partenaire ({contract.Value.Declarer}) joue le contrat. Vous jouez le mort.");
    }
    else
    {
        logger.Information("Vous jouez en défense contre le contrat.");
    }

    // Jeu de la carte
    var cardGame = new CardPlayEngine(deal, contract.Value, logger, auction.Calls);

    while (!cardGame.IsComplete)
    {
        var seat = cardGame.NextToPlay;
        Card card;

        if (seat == Seat.North)
        {
            // Humain joue
            DisplayHand(cardGame.Hand(Seat.North));
            logger.Information($"Levée {cardGame.TrickNumber}: {FormatTrick(cardGame.CurrentTrick)}");
            card = ReadHumanCard(cardGame.LegalCards(Seat.North));
        }
        else
        {
            // IA joue
            card = engine.ChooseCard(cardGame, seat);
        }

        var wasLastCard = cardGame.CurrentTrick.Count == 3;
        var winner = cardGame.Play(seat, card);
        
        if (wasLastCard)
        {
            logger.Information($"Levée remportée par {winner}");
        }
    }

    // Calculer le score
    var declarerMadeContract = cardGame.DeclarerTricks >= contract.Value.RequiredTricks;
    var points = BridgeScoring.Score(contract.Value, cardGame.DeclarerTricks, vulnerability);
    
    if (northSouthDeclare)
    {
        northSouthScore += declarerMadeContract ? points : -points;
        if (declarerMadeContract)
        {
            logger.Information($"✓ Contrat réussi: {cardGame.DeclarerTricks} levées. +{points} points");
        }
        else
        {
            logger.Warning($"✗ Contrat chuté: {cardGame.DeclarerTricks} levées. -{points} points");
        }
    }
    else
    {
        northSouthScore -= declarerMadeContract ? points : -points;
        if (declarerMadeContract)
        {
            logger.Warning($"✗ Est-Ouest gagne: {cardGame.DeclarerTricks} levées. -{points} points");
        }
        else
        {
            logger.Information($"✓ Vous défendez bien: défense réussit. +{points} points");
        }
    }

    logger.Information($"Score Nord-Sud: {northSouthScore}");
}

static string FormatVulnerability(Vulnerability vulnerability) => vulnerability switch
{
    Vulnerability.None => "Personne",
    Vulnerability.NorthSouth => "Nord-Sud",
    Vulnerability.EastWest => "Est-Ouest",
    Vulnerability.Both => "Tous",
    _ => vulnerability.ToString()
};

static string FormatAuction(Auction auction) => auction.Calls.Count == 0
    ? "-"
    : string.Join(" | ", auction.Calls.Select(call => call.ToString()));

static string FormatTrick(IReadOnlyList<PlayedCard> trick) => trick.Count == 0
    ? "-"
    : string.Join(" ", trick.Select(p => $"{p.Seat.ToString()[0]}:{p.Card}"));

static void DisplayHand(IReadOnlyList<Card> hand)
{
    Console.WriteLine("Votre main:");
    var sorted = hand.OrderByDescending(c => c.Suit).ThenByDescending(c => c.Rank).ToList();
    foreach (var suit in Enum.GetValues<Suit>())
    {
        var cards = sorted.Where(c => c.Suit == suit).ToList();
        if (cards.Count > 0)
        {
            Console.WriteLine($"  {Card.SuitText(suit)}: {string.Join(" ", cards.Select(c => Card.RankText(c.Rank)))}");
        }
    }
}

static AuctionCall ReadHumanCall(Auction auction, Seat seat)
{
    while (true)
    {
        var forcing = auction.ForcingFor(seat);
        Console.Write("Votre enchère (P, X, XX ou 1C-7SA): ");
        var text = Console.ReadLine()?.Trim().ToUpperInvariant();
        
        if (text is "P" or "PASS")
        {
            if (forcing == AuctionForcing.None) return new AuctionCall(seat, AuctionAction.Pass);
            Console.WriteLine("Cette séquence forcing interdit de passer.");
            continue;
        }

        if (text == "X" && forcing != AuctionForcing.None)
        {
            Console.WriteLine("Cette séquence forcing exige une enchère à la couleur ou à Sans-Atout.");
            continue;
        }

        if (text == "X" && auction.CanDouble(seat))
        {
            return new AuctionCall(seat, AuctionAction.Double);
        }

        if (text == "XX" && forcing != AuctionForcing.None)
        {
            Console.WriteLine("Cette séquence forcing exige une enchère à la couleur ou à Sans-Atout.");
            continue;
        }

        if (text == "XX" && auction.CanRedouble(seat))
        {
            return new AuctionCall(seat, AuctionAction.Redouble);
        }

        if (TryParseBid(text, out var bid) && auction.IsLegal(bid))
        {
            return new AuctionCall(seat, AuctionAction.Bid, bid);
        }

        Console.WriteLine("Enchère invalide ou insuffisante.");
    }
}

static Card ReadHumanCard(IReadOnlyList<Card> legalCards)
{
    while (true)
    {
        Console.Write($"Carte à jouer ({string.Join(" ", legalCards)}): ");
        var text = Console.ReadLine()?.Trim().ToUpperInvariant();
        
        var matching = legalCards.Where(c => c.ToString() == text).ToList();
        if (matching.Count == 1)
        {
            return matching[0];
        }

        Console.WriteLine("Carte invalide. Vous devez jouer une carte légale.");
    }
}

static bool TryParseBid(string? text, out Bid bid)
{
    bid = default;
    if (string.IsNullOrWhiteSpace(text) || text.Length < 2 || !int.TryParse(text[0].ToString(), out var level))
        return false;

    var denom = text[1..] switch
    {
        "C" => Denomination.Clubs,
        "D" => Denomination.Diamonds,
        "H" => Denomination.Hearts,
        "S" => Denomination.Spades,
        "SA" or "NT" => Denomination.NoTrump,
        _ => (Denomination?)null
    };

    if (!denom.HasValue || level is < 1 or > 7)
        return false;

    bid = new Bid(level, denom.Value);
    return true;
}

