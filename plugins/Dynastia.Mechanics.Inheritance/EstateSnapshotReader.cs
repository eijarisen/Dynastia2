using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

/// <summary>Captures already-reconciled state without taking assets or writing components.</summary>
internal sealed class EstateSnapshotReader(
    IFamilyService family, IEconomyService economy, IHeirloomService heirlooms,
    Func<IFarmingService?> farmingResolver)
{
    private readonly EstateHeirResolver _heirs = new(family);
    private readonly EstateRecipientRules _recipients = new(family, economy);

    public EstateSnapshot Capture(IGameState state, IPerson head)
    {
        var anchorId = economy.GetHouseholdDynastyAnchorId(head);
        var anchor = anchorId is Guid id ? state.People.FirstOrDefault(person => person.Id == id) : null;
        var finance = economy.GetHousehold(head);
        var householdId = economy.GetHouseholdId(head);
        var source = anchor ?? head;
        if (finance is null)
            return new(state.Year, head.Id, householdId, anchorId, source.Id, string.Empty,
                false, null, "no heirs", [], [], [], [], [], 0m);

        var isLivingAnchor = anchor is not null && anchor.Tags.Has("state.alive");
        // These read APIs use the same ordering as their TakeAll counterparts.
        var houses = economy.GetHouses(head);
        var farmland = economy.GetFarmland(head);
        var items = heirlooms.GetHeirlooms(head);
        var resolution = isLivingAnchor
            ? new EstateHeirResolver.EstateHeirResolution([anchor!], "the living bloodline anchor")
            : _heirs.Resolve(state, anchor);
        var recipients = resolution.Heirs.Select(heir => new EstateRecipient(
            heir.Id, family.GetDisplayName(heir),
            !(isLivingAnchor
                ? _recipients.HasLivingAnchorHousehold(heir, householdId)
                : _recipients.HasEstablishedHouseholdOutsideEstate(heir, householdId)),
            economy.GetHouseholdId(heir))).ToArray();
        var snapshot = new EstateSnapshot(state.Year, head.Id, householdId, anchorId,
            source.Id, family.GetDisplayName(source), isLivingAnchor, finance.Wealth,
            resolution.Description, recipients, houses, farmland, items,
            new decimal[houses.Count], 0m);
        if (!isLivingAnchor && EstatePlanBuilder.HasExplicitDesignation(snapshot))
            snapshot = snapshot with
            {
                HouseValues = houses.Select(house => Math.Max(0m, economy.GetHouseValue(house))).ToArray(),
                FarmlandValue = Math.Max(0m, farmingResolver()?.PurchasePrice ?? 0m)
            };
        return snapshot.Freeze();
    }
}
