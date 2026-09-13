namespace Dynastia.Contracts;

public sealed record TownEconomyProfile(
    TownInfo Town,
    decimal HousingIndex,
    decimal LivingCostIndex,
    decimal HousePrice,
    decimal RentCost,
    decimal LivingCostUnit)
{
    public string LivingCostLevel =>
        LivingCostIndex switch
        {
            <= 0.90m => "Low",
            <= 1.00m => "Moderate",
            <= 1.10m => "High",
            _ => "Very high"
        };
}
