namespace Bridge.AI.Bidding;

using Bridge.Core.Models;

/// <summary>Chapitre SEF : enchères de chelem (Blackwood 5 clés 30-41).</summary>
public sealed partial class SefBiddingEngine
{
    private static Bid BlackwoodResponse(HandEvaluation hand, Denomination trump)
    {
        var keys = hand.AceCount + (hand.Cards.Contains(new Card((Suit)trump, Rank.King)) ? 1 : 0);
        var hasTrumpQueen = hand.Cards.Contains(new Card((Suit)trump, Rank.Queen));
        var voidSuit = hand.Cards.Count == 13
            ? Enum.GetValues<Suit>()
                .Where(suit => ToDenomination(suit) != trump && hand.SuitLength(suit) == 0)
                .Cast<Suit?>()
                .FirstOrDefault()
            : null;
        if (voidSuit.HasValue && keys is 2 or 4)
            return new Bid(5, Denomination.NoTrump);
        if (voidSuit.HasValue && keys is 1 or 3)
        {
            var voidDenomination = ToDenomination(voidSuit.Value);
            return voidDenomination > trump
                ? new Bid(6, trump)
                : new Bid(6, voidDenomination);
        }
        return keys switch
        {
            0 or 3 => new Bid(5, Denomination.Clubs),
            1 or 4 => new Bid(5, Denomination.Diamonds),
            2 or 5 when !hasTrumpQueen => new Bid(5, Denomination.Hearts),
            2 or 5 => new Bid(5, Denomination.Spades),
            _ => new Bid(5, Denomination.Clubs)
        };
    }

    private static Bid? BlackwoodConclusion(HandEvaluation hand, Bid response, Denomination trump)
    {
        // Une réponse déjà au palier de 6 ne doit jamais être surenchérie
        // mécaniquement à 7 sans séquence de contrôles dédiée.
        if (response.Level >= 6)
            return null;

        var ownKeys = hand.AceCount + (hand.Cards.Contains(new Card((Suit)trump, Rank.King)) ? 1 : 0);
        var partnerKeys = response.Denomination switch
        {
            Denomination.Clubs => ownKeys >= 2 ? 3 : 0,
            Denomination.Diamonds => ownKeys >= 1 ? 4 : 1,
            Denomination.Hearts or Denomination.Spades => 2,
            _ => 0
        };
        return ownKeys + partnerKeys >= 4
            ? new Bid(6, trump)
            : CheapestBid(trump, response);
    }
}
