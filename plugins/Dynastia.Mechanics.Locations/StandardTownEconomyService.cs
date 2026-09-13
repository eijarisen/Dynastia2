using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed class StandardTownEconomyService :
    ITownEconomyService
{
    private const decimal BaseHousePrice = 10000m;
    private const decimal BaseLivingCost = 250m;
    private const decimal BaseRentCost = 250m;

    private readonly ITownDirectoryService _towns;

    public StandardTownEconomyService(
        ITownDirectoryService towns)
    {
        _towns = towns;
    }

    public TownEconomyProfile GetProfile(
        TownInfo town)
    {
        ArgumentNullException.ThrowIfNull(town);

        var (housing, living) = town.SettlementClass switch
        {
            SettlementClass.SmallTown => (0.75m, 0.85m),
            SettlementClass.Town => (0.90m, 0.95m),
            SettlementClass.City => (1.10m, 1.05m),
            SettlementClass.MajorCity => (1.35m, 1.20m),
            _ => (1.00m, 1.00m)
        };

        return new TownEconomyProfile(
            town,
            housing,
            living,
            RoundMoney(BaseHousePrice * housing),
            RoundMoney(BaseRentCost * housing),
            RoundMoney(BaseLivingCost * living));
    }

    public TownEconomyProfile? GetProfile(
        string townId)
    {
        var town = _towns.FindTown(townId);
        return town is null ? null : GetProfile(town);
    }

    public IReadOnlyList<TownEconomyProfile> GetAllProfiles() =>
        _towns.GetAllTowns()
            .Select(GetProfile)
            .ToList();

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
