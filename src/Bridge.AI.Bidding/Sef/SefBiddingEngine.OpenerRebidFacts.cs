namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

public sealed partial class SefBiddingEngine
{
    private int? OpenerRebidFact(string fact, Seat seat, HandEvaluation hand, Auction auction,
        IReadOnlyList<AuctionCall> sideBids, Denomination? candidate, int highCards, int honorLength, int distribution)
    {
        var opening = sideBids.FirstOrDefault().Bid;
        if (opening is null) return null;
        var response = sideBids.ElementAtOrDefault(1).Bid;
        var partnerCall = auction.Calls.LastOrDefault(call => call.Seat == seat.Partner() && !call.IsPass);
        var partnerIndex = auction.Calls.ToList().FindLastIndex(call => call.Seat == seat.Partner() && !call.IsPass);
        var lastCall = auction.Calls.LastOrDefault(call => !call.IsPass);
        var free = partnerIndex >= 0 && !SameSide(lastCall.Seat, seat);
        var sputnik = partnerCall.Action == AuctionAction.Double && opening.Value.Level == 1 &&
            auction.Calls.Take(Math.Max(0, partnerIndex)).Any(call => !SameSide(call.Seat, seat) && call.Bid.HasValue);
        var opponentBid = auction.Calls.Take(Math.Max(0, partnerIndex))
            .LastOrDefault(call => !SameSide(call.Seat, seat) && call.Bid.HasValue).Bid;
        var adjustment = sputnik ? (opponentBid?.Level == 1 ? 8 : 10) - 6 : 0;
        var responseDenomination = response?.Denomination ?? (sputnik ? ResolveSputnikMajor(auction, seat) : null);
        var drury = opening.Value.Denomination is Denomination.Hearts or Denomination.Spades &&
            response == new Bid(2, Denomination.Clubs) && Profile.IsConventionEnabled("drury") &&
            auction.Calls.TakeWhile(call => !call.Bid.HasValue).Count() >= 2;
        var responsePoints = responseDenomination is { } responseSuit && responseSuit != Denomination.NoTrump
            ? honorLength + distribution + FitBonus(hand.SuitLength((Suit)responseSuit) +
                (opening == new Bid(1, Denomination.Spades) && response == new Bid(2, Denomination.Hearts) ? 5 : 4))
            : (int?)null;
        var supportLength = response?.Denomination == Denomination.NoTrump || response?.Level == 2 ? 3 : 4;
        var supportPoints = opening.Value.Denomination != Denomination.NoTrump
            ? honorLength + distribution + FitBonus(hand.SuitLength((Suit)opening.Value.Denomination) + supportLength)
            : (int?)null;
        var repeated = auction.HighestBid is { } highest ? CheapestBid(opening.Value.Denomination, highest) : opening.Value;
        var candidateBid = candidate is { } color && auction.HighestBid is { } current ? CheapestBid(color, current) : (Bid?)null;
        var canTry = Enum.GetValues<Suit>().Any(suit => (Denomination)suit != opening.Value.Denomination &&
            auction.HighestBid is { } current &&
            Auction.Compare(CheapestBid((Denomination)suit, current), new Bid(3, opening.Value.Denomination)) < 0);
        var forced = !free && (sputnik || partnerCall.Bid is { } partnerBid &&
            (partnerBid.Denomination != opening.Value.Denomination && partnerBid.Denomination != Denomination.NoTrump ||
                partnerBid == new Bid(2, Denomination.NoTrump) && Profile.IsConventionEnabled("fittedTwoNoTrump")));
        var candidateHonors = candidate is { } second
            ? hand.Cards.Where(card => (Denomination)card.Suit == second || (Denomination)card.Suit == opening.Value.Denomination)
                .Sum(card => card.Rank switch
                {
                    Rank.Ace => Profile.Evaluation.Ace, Rank.King => Profile.Evaluation.King,
                    Rank.Queen => Profile.Evaluation.Queen, Rank.Jack => Profile.Evaluation.Jack, _ => 0
                })
            : 0;
        return fact switch
        {
            "responseFitPoints" => responsePoints,
            "agreedFitPoints" => supportPoints,
            "adjustedHL" => honorLength + adjustment,
            "adjustedH" => highCards + adjustment,
            "adjustedResponseFitPoints" => responsePoints + adjustment,
            "semiRegular" => hand.Cards.Count == 13 && Enum.GetValues<Suit>().Count(suit => hand.SuitLength(suit) == 2) == 2 &&
                Enum.GetValues<Suit>().All(suit => hand.SuitLength(suit) >= 2) ? 1 : 0,
            "oneLevelMajorAvailable" => new[] { Suit.Hearts, Suit.Spades }.Any(suit => hand.SuitLength(suit) >= 4 &&
                (Denomination)suit != opening.Value.Denomination && (Denomination)suit != responseDenomination &&
                auction.IsLegal(new Bid(1, (Denomination)suit))) ? 1 : 0,
            "candidateCheapestLevel" => candidateBid?.Level,
            "repeatCheapestLevel" => repeated.Level,
            "candidateReverse" => candidateBid.HasValue ? candidateBid.Value.Level > opening.Value.Level &&
                Auction.Compare(candidateBid.Value, repeated) > 0 ? 1 : 0 : null,
            "candidateEconomic" => candidateBid.HasValue ? candidateBid.Value.Level > opening.Value.Level &&
                Auction.Compare(candidateBid.Value, repeated) < 0 ? 1 : 0 : null,
            "concentratedHonorsPercent" => candidate.HasValue && highCards > 0 ? candidateHonors * 100 / highCards : null,
            "repeatNeedsSix" => response is { } answer && (answer.Denomination == Denomination.NoTrump ||
                answer.Level == 1 && (int)answer.Denomination == (int)opening.Value.Denomination + 1) ? 1 : 0,
            "freeRebid" => free ? 1 : 0,
            "forcedRebid" => forced ? 1 : 0,
            "partnerSputnik" => sputnik ? 1 : 0,
            "freeActionRequired" => honorLength + adjustment >= 15 ||
                hand.SuitLength((Suit)opening.Value.Denomination) >= 6 && repeated.Level <= 2 ||
                Enum.GetValues<Suit>().Count(suit => hand.SuitLength(suit) >= 5) >= 2 ? 1 : 0,
            "openerControlFit" => responseDenomination is { } fitSuit && fitSuit != Denomination.NoTrump &&
                hand.SuitLength((Suit)fitSuit) >= 4 ? 1 : 0,
            "naturalResponse" => !drury && responseDenomination is { } naturalSuit && naturalSuit != Denomination.NoTrump &&
                naturalSuit != opening.Value.Denomination ? 1 : 0,
            "fourthSeatActed" => free ? 1 : 0,
            "partnerResponseIsSuit" => responseDenomination is { } natural && natural != Denomination.NoTrump ? 1 : 0,
            "opponentsRaised" => auction.Calls.Where(call => !SameSide(call.Seat, seat) && call.Bid.HasValue)
                .GroupBy(call => call.Bid!.Value.Denomination).Any(group => group.Count() >= 2) ? 1 : 0,
            "trialSpace" => canTry ? 1 : 0,
            "shortSuitStoppers" => Enum.GetValues<Suit>().Where(suit => hand.SuitLength(suit) == 2)
                .All(suit => HasGuard(hand, suit)) ? 1 : 0,
            "shortHonor" => Enum.GetValues<Suit>().Any(suit => hand.SuitLength(suit) == 1 &&
                hand.Cards.Any(card => card.Suit == suit && card.Rank >= Rank.Queen)) ? 1 : 0,
            "partnerSupport" => responseDenomination is { } fit && fit != Denomination.NoTrump ? hand.SuitLength((Suit)fit) : null,
            "openerFakeJump" => sideBids.Count >= 4 && sideBids[2].Bid is { } fake && response is { } firstResponse &&
                fake.Denomination != opening.Value.Denomination && fake.Denomination != firstResponse.Denomination &&
                fake.Level == CheapestBid(fake.Denomination, firstResponse).Level + 1 ? 1 : 0,
            _ => null
        };
    }

    private int FitBonus(int totalTrumps) => totalTrumps >= 9
        ? Profile.Evaluation.NinthTrump + (totalTrumps - 9) * Profile.Evaluation.ExtraTrump : 0;

    private static Denomination? ResolveSputnikMajor(Auction auction, Seat seat)
    {
        var opponent = auction.Calls.LastOrDefault(call => !SameSide(call.Seat, seat) && call.Bid.HasValue).Bid;
        return opponent?.Denomination switch
        {
            Denomination.Hearts => Denomination.Spades,
            Denomination.Spades => Denomination.Hearts,
            _ => null
        };
    }
}