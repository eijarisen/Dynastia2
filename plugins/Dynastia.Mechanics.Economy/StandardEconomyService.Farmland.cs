using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public IReadOnlyList<FarmlandAssetInfo> GetFarmland(
        IPerson person)
    {
        var resolved = FindHousehold(person);
        if (resolved is null)
            return Array.Empty<FarmlandAssetInfo>();

        var household = resolved.Value.Household;

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
            Id = _random.NextGuid(),
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
            Id = farmland.Id == Guid.Empty ? _random.NextGuid() : farmland.Id,
            TownId = farmland.Town.Id,
            AcquiredYear = farmland.AcquiredYear,
            AcquisitionSource = farmland.AcquisitionSource,
            AssignedHeirId = farmland.AssignedHeirId,
            FarmTypeId = farmland.FarmTypeId,
            LivestockTypeId = farmland.LivestockTypeId
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

    public bool SetFarmlandInheritanceHeir(
        IPerson person,
        Guid farmlandId,
        Guid? heirId)
    {
        var household = GetRequiredHousehold(person);
        NormalizeFarmland(household);

        var farmland = household.Farmland
            .FirstOrDefault(candidate => candidate.Id == farmlandId);

        if (farmland is null)
            return false;

        if (heirId is Guid selectedHeirId)
        {
            var validChild = _family.GetChildren(person)
                .Any(child =>
                    child.Id == selectedHeirId
                    && child.Tags.Has("state.alive"));

            if (!validChild)
                return false;
        }

        farmland.AssignedHeirId = heirId;
        return true;
    }

    public bool SetFarmlandFlavor(
        IPerson person,
        Guid farmlandId,
        string farmTypeId,
        string? livestockTypeId)
    {
        var household = GetRequiredHousehold(person);
        NormalizeFarmland(household);

        var farmland = household.Farmland
            .FirstOrDefault(candidate => candidate.Id == farmlandId);

        if (farmland is null)
            return false;

        farmland.FarmTypeId = farmTypeId?.Trim() ?? string.Empty;
        farmland.LivestockTypeId = string.IsNullOrWhiteSpace(livestockTypeId)
            ? null
            : livestockTypeId.Trim();
        return true;
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
        var claim = FindClaim(person);
        if (claim is null)
            return Array.Empty<FarmlandAssetInfo>();

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
            Id = farmland.Id == Guid.Empty ? _random.NextGuid() : farmland.Id,
            TownId = farmland.Town.Id,
            AcquiredYear = farmland.AcquiredYear,
            AcquisitionSource = farmland.AcquisitionSource,
            AssignedHeirId = farmland.AssignedHeirId,
            FarmTypeId = farmland.FarmTypeId,
            LivestockTypeId = farmland.LivestockTypeId
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
            ? _random.NextGuid()
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
            state.AcquisitionSource,
            state.AssignedHeirId,
            state.FarmTypeId,
            LivestockTypeId: state.LivestockTypeId);
    }
}
