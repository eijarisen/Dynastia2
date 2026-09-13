namespace Dynastia.Contracts;

public sealed record PropertyTownOption(
    TownInfo Town,
    string RegionName,
    string OpportunityDescription,
    decimal HousePrice,
    decimal LivingCostIndex,
    string LivingCostLevel)
{
    public string SettlementClassLabel =>
        Town.SettlementClass switch
        {
            SettlementClass.SmallTown => "Small Town",
            SettlementClass.Town => "Town",
            SettlementClass.City => "City",
            SettlementClass.MajorCity => "Major City",
            _ => Town.SettlementClass.ToString()
        };
}
