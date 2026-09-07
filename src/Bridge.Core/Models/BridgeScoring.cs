namespace Bridge.Core.Models;

public static class BridgeScoring
{
    public static int Score(Contract contract, int declarerTricks, Vulnerability vulnerability = Vulnerability.None)
    {
        var isVulnerable = vulnerability.IsVulnerable(contract.Declarer);
        var shortfall = contract.RequiredTricks - declarerTricks;
        if (shortfall > 0)
        {
            return UndertrickPenalty(shortfall, contract.Doubling, isVulnerable);
        }

        var multiplier = (int)contract.Doubling;
        var undoubledContractPoints = contract.Bid.Level * TrickValue(contract.Bid.Denomination) +
            (contract.Bid.Denomination == Denomination.NoTrump ? 10 : 0);
        var contractPoints = undoubledContractPoints * multiplier;
        var overtricks = declarerTricks - contract.RequiredTricks;
        var overtrickPoints = contract.Doubling switch
        {
            Doubling.None => overtricks * TrickValue(contract.Bid.Denomination),
            Doubling.Doubled => overtricks * (isVulnerable ? 200 : 100),
            Doubling.Redoubled => overtricks * (isVulnerable ? 400 : 200),
            _ => 0
        };
        var gameOrPartScoreBonus = contractPoints >= 100 ? (isVulnerable ? 500 : 300) : 50;
        var slamBonus = contract.Bid.Level switch
        {
            6 => isVulnerable ? 750 : 500,
            7 => isVulnerable ? 1500 : 1000,
            _ => 0
        };
        var insultBonus = contract.Doubling switch
        {
            Doubling.Doubled => 50,
            Doubling.Redoubled => 100,
            _ => 0
        };

        return contractPoints + overtrickPoints + gameOrPartScoreBonus + slamBonus + insultBonus;
    }

    private static int TrickValue(Denomination denomination) => denomination switch
    {
        Denomination.Clubs or Denomination.Diamonds => 20,
        Denomination.Hearts or Denomination.Spades or Denomination.NoTrump => 30,
        _ => throw new ArgumentOutOfRangeException(nameof(denomination))
    };

    private static int UndertrickPenalty(int shortfall, Doubling doubling, bool isVulnerable) => doubling switch
    {
        Doubling.None => shortfall * (isVulnerable ? 100 : 50),
        Doubling.Doubled when isVulnerable => 200 + (shortfall - 1) * 300,
        Doubling.Doubled => shortfall switch
        {
            1 => 100,
            2 => 300,
            3 => 500,
            _ => 500 + (shortfall - 3) * 300
        },
        Doubling.Redoubled => 2 * UndertrickPenalty(shortfall, Doubling.Doubled, isVulnerable),
        _ => throw new ArgumentOutOfRangeException(nameof(doubling))
    };
}