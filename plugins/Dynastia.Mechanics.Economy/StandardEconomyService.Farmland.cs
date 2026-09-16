using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public IReadOnlyList<FarmlandAssetInfo> GetFarmland(
        IPerson person)
    {
        var household = GetRequiredHousehold(person);
        NormalizeFarmland(household);

        return household.Farmland
            .Select(ToFarmlandInfo)
            .OrderBy(asset => asset.AcquiredYear)
            .ThenBy(asset => asset.Id)
            .ToList();
    }

    public FarmlandAssetInfo AddFarmland(
        IPerson person,
        TownInfo town,
        int acquiredYear,
        string acquisitionSource)
    {
        ArgumentNullException.ThrowIfNull(town);

        var household = GetRequiredHousehold(person);
        NormalizeFarmland(household);

        var state = new FarmlandAssetState
        {
            Id = Guid.NewGuid(),
            TownId = town.Id,
            AcquiredYear = acquiredYear,
            AcquisitionSource = string.IsNullOrWhiteSpace(acquisitionSource)
                ? "unknown"
                : acquisitionSource
        };

        household.Farmland.Add(state);
        return ToFarmlandInfo(state);
    }

    public void AddExistingFarmland(
        IPerson person,
        FarmlandAssetInfo farmland)
    {
        ArgumentNullException.ThrowIfNull(farmland);

        var household = GetRequiredHousehold(person);
        NormalizeFarmland(household);

        if (household.Farmland.Any(asset => asset.Id == farmland.Id))
            return;

        household.Farmland.Add(new FarmlandAssetState
        {
            Id = farmland.Id == Guid.Empty ? Guid.NewGuid() : farmland.Id,
            TownId = farmland.Town.Id,
            AcquiredYear = farmland.AcquiredYear,
            AcquisitionSource = farmland.AcquisitionSource
        });
    }

    public FarmlandAssetInfo? TakeFarmland(
        IPerson person,
        Guid farmlandId)
    {
        var household = GetRequiredHousehold(person);
        NormalizeFarmland(household);

        var index = household.Farmland.FindIndex(asset => asset.Id == farmlandId);
        if (index < 0)
            return null;

        var state = household.Farmland[index];
        household.Farmland.RemoveAt(index);
        return ToFarmlandInfo(state);
    }

    public IReadOnlyList<FarmlandAssetInfo> TakeAllFarmland(
        IPerson person)
    {
        var household = GetRequiredHousehold(person);
        NormalizeFarmland(household);

        var result = household.Farmland
            .Select(ToFarmlandInfo)
            .OrderBy(asset => asset.AcquiredYear)
            .ThenBy(asset => asset.Id)
            .ToList();

        household.Farmland.Clear();
        return result;
    }

    public IReadOnlyList<FarmlandAssetInfo> GetPendingFarmland(
        IPerson person)
    {
        var claim = GetClaim(person);
        NormalizePendingFarmland(claim);

        return claim.PendingFarmland
            .Select(ToFarmlandInfo)
            .OrderBy(asset => asset.AcquiredYear)
            .ThenBy(asset => asset.Id)
            .ToList();
    }

    public void AddPendingFarmland(
        IPerson person,
        FarmlandAssetInfo farmland)
    {
        ArgumentNullException.ThrowIfNull(farmland);

        var claim = GetClaim(person);
        NormalizePendingFarmland(claim);

        if (claim.PendingFarmland.Any(asset => asset.Id == farmland.Id))
            return;

        claim.PendingFarmland.Add(new FarmlandAssetState
        {
            Id = farmland.Id == Guid.Empty ? Guid.NewGuid() : farmland.Id,
            TownId = farmland.Town.Id,
            AcquiredYear = farmland.AcquiredYear,
            AcquisitionSource = farmland.AcquisitionSource
        });
    }

    public IReadOnlyList<FarmlandAssetInfo> TakePendingFarmland(
        IPerson person)
    {
        var claim = GetClaim(person);
        NormalizePendingFarmland(claim);

        var result = claim.PendingFarmland
            .Select(ToFarmlandInfo)
            .OrderBy(asset => asset.AcquiredYear)
            .ThenBy(asset => asset.Id)
            .ToList();

        claim.PendingFarmland.Clear();
        return result;
    }

    private void NormalizeFarmland(
        HouseholdEconomyComponent household)
    {
        foreach (var asset in household.Farmland)
            NormalizeFarmlandAsset(asset);
    }

    private void NormalizePendingFarmland(
        PersonalEstateComponent claim)
    {
        foreach (var asset in claim.PendingFarmland)
            NormalizeFarmlandAsset(asset);
    }

    private void NormalizeFarmlandAsset(
        FarmlandAssetState asset)
    {
        asset.Id = asset.Id == Guid.Empty
            ? Guid.NewGuid()
            : asset.Id;

        asset.AcquisitionSource = string.IsNullOrWhiteSpace(asset.AcquisitionSource)
            ? "legacy"
            : asset.AcquisitionSource;

        if (string.IsNullOrWhiteSpace(asset.TownId)
            || _locations.FindTown(asset.TownId) is null)
        {
            asset.TownId = _locations.GetTowns().First().Id;
        }
    }

    private FarmlandAssetInfo ToFarmlandInfo(
        FarmlandAssetState state)
    {
        var town = _locations.FindTown(state.TownId)
            ?? throw new InvalidOperationException(
                $"Farmland town '{state.TownId}' is unavailable.");

        return new FarmlandAssetInfo(
            state.Id,
            town,
            state.AcquiredYear,
            state.AcquisitionSource);
    }
}
