using Dynastia.Contracts;
using static Dynastia.Mechanics.Inheritance.EstatePlanBuilder;

namespace Dynastia.Mechanics.Inheritance;

/// <summary>
/// Validates the entire captured estate before its first write. YearProcessor's existing
/// rollback remains responsible for unexpected service/subscriber failures during apply.
/// </summary>
internal sealed class EstateSettlementApplier(
    IFamilyService family, IEconomyService economy, IHeirloomService heirlooms, IGameEventBus events)
{
    private readonly EstateHeirResolver _heirs = new(family);
    private readonly EstateRecipientRules _recipients = new(family, economy);

    public void Apply(IGameState state, EstateSettlementPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var people = Validate(state, plan);
        var head = people[plan.Snapshot.HeadId];
        // Construct even event text before taking assets: the apply phase only executes
        // validated instructions, not calculations that could discover an invalid plan.
        var news = EstateEventFactory.Create(plan);
        if (plan.Kind == EstateSettlementKind.MissingFinance)
        {
            economy.DissolveHousehold(head);
            return;
        }

        var houses = economy.TakeAllHouses(head).ToArray();
        var farmland = economy.TakeAllFarmland(head).ToArray();
        var items = heirlooms.TakeAll(head).ToArray();
        // Get/TakeAll must describe the same ordered identities. Normalization during
        // TakeAll may clear a dead house designation; use the actual taken metadata.
        Require(houses.Select(asset => asset.Id).SequenceEqual(plan.Snapshot.Houses.Select(asset => asset.Id))
            && farmland.Select(asset => asset.Id).SequenceEqual(plan.Snapshot.Farmland.Select(asset => asset.Id))
            && items.Select(asset => asset.Id).SequenceEqual(plan.Snapshot.Heirlooms.Select(asset => asset.Id)),
            "Estate asset service returned a different estate during removal.");

        foreach (var allocation in plan.Cash)
        {
            if (allocation.Amount == 0m) continue;
            var heir = people[allocation.RecipientId];
            if (allocation.IsPending)
                economy.ChangePendingInheritance(heir, allocation.Amount);
            else if (allocation.Amount > 0m)
                economy.ChangeWealth(heir, allocation.Amount);
            else
                economy.ChangeWealthAllowDebt(heir, allocation.Amount);
        }
        var isInheritance = plan.Kind == EstateSettlementKind.Inheritance;
        for (var index = 0; index < plan.Houses.Count; index++)
        {
            var allocation = plan.Houses[index];
            var asset = isInheritance ? houses[index] with { AssignedHeirId = null } : houses[index];
            if (allocation.IsPending)
                economy.AddPendingHouse(people[allocation.RecipientId], asset);
            else
                economy.AddExistingHouse(people[allocation.RecipientId], asset);
        }
        for (var index = 0; index < plan.Farmland.Count; index++)
        {
            var allocation = plan.Farmland[index];
            var asset = isInheritance ? farmland[index] with
            {
                AcquiredYear = state.Year,
                AcquisitionSource = "inheritance",
                AssignedHeirId = null
            } : farmland[index];
            if (allocation.IsPending)
                economy.AddPendingFarmland(people[allocation.RecipientId], asset);
            else
                economy.AddExistingFarmland(people[allocation.RecipientId], asset);
        }
        for (var index = 0; index < plan.Heirlooms.Count; index++)
        {
            var allocation = plan.Heirlooms[index];
            var asset = isInheritance ? items[index] with { AssignedHeirId = null } : items[index];
            if (allocation.IsPending)
                heirlooms.AddPending(people[allocation.RecipientId], asset, state.Year,
                    isInheritance ? "inherited_pending" : "household_transfer_pending");
            else
                heirlooms.AddExisting(people[allocation.RecipientId], asset, state.Year, allocation.RecipientId,
                    isInheritance ? "inherited" : "household_transfer");
        }

        foreach (var item in news.RecipientEvents) events.Publish(item);
        foreach (var item in news.DisadvantageEvents) events.Publish(item);
        economy.SetWealth(head, 0m);
        if (news.Summary is not null) events.Publish(news.Summary);
        economy.DissolveHousehold(head);
    }

    private Dictionary<Guid, IPerson> Validate(IGameState state, EstateSettlementPlan plan)
    {
        var snapshot = plan.Snapshot;
        ValidateInstructions(plan);
        var people = state.People.ToDictionary(person => person.Id);
        Require(state.Year == snapshot.Year && people.ContainsKey(snapshot.HeadId),
            "Estate year or head changed after planning.");
        var head = people[snapshot.HeadId];
        Require(economy.HasHousehold(head) && economy.IsEstateReady(head),
            "Estate is no longer ready or has already been settled.");
        Require(economy.GetHouseholdId(head) == snapshot.HouseholdId
            && economy.GetHouseholdDynastyAnchorId(head) == snapshot.AnchorId,
            "Estate household or dynasty anchor changed after planning.");
        var finance = economy.GetHousehold(head);
        Require(finance?.Wealth == snapshot.Wealth, "Estate balance changed after planning.");
        if (plan.Kind == EstateSettlementKind.MissingFinance) return people;

        var anchor = snapshot.AnchorId is Guid id ? people.GetValueOrDefault(id) : null;
        Require((anchor ?? head).Id == snapshot.SourceId
            && (anchor is not null && anchor.Tags.Has("state.alive")) == snapshot.IsLivingAnchor,
            "Estate source or living-anchor state changed after planning.");
        var currentHeirs = snapshot.IsLivingAnchor
            ? new EstateHeirResolver.EstateHeirResolution([anchor!], "the living bloodline anchor")
            : _heirs.Resolve(state, anchor);
        Require(currentHeirs.Heirs.Select(heir => heir.Id).SequenceEqual(snapshot.Recipients.Select(heir => heir.Id))
            && currentHeirs.Description == snapshot.HeirDescription,
            "Estate heir eligibility or ordering changed after planning.");
        Require(family.GetDisplayName(anchor ?? head) == snapshot.SourceName,
            "Estate source name changed after planning.");
        foreach (var recipient in snapshot.Recipients)
        {
            Require(people.TryGetValue(recipient.Id, out var heir) && heir.Tags.Has("state.alive"),
                "A planned estate recipient is missing or dead.");
            var isEstablished = snapshot.IsLivingAnchor
                ? _recipients.HasLivingAnchorHousehold(heir!, snapshot.HouseholdId)
                : _recipients.HasEstablishedHouseholdOutsideEstate(heir!, snapshot.HouseholdId);
            Require(recipient.IsPending == !isEstablished
                && economy.GetHouseholdId(heir!) == recipient.HouseholdId
                && family.GetDisplayName(heir!) == recipient.DisplayName,
                "A planned estate recipient's household or identity changed.");
        }

        // Validate every category before *any* TakeAll call, including no-heir estates.
        Require(economy.GetHouses(head).SequenceEqual(snapshot.Houses), "Estate houses changed after planning.");
        Require(economy.GetFarmland(head).SequenceEqual(snapshot.Farmland), "Estate farmland changed after planning.");
        Require(SameHeirlooms(heirlooms.GetHeirlooms(head), snapshot.Heirlooms),
            "Estate heirlooms changed after planning.");
        return people;
    }

    private static bool SameHeirlooms(IReadOnlyList<HeirloomAssetInfo> current,
        IReadOnlyList<HeirloomAssetInfo> captured) => current.Count == captured.Count
        && current.Select((item, index) =>
            (item with { OwnershipHistory = captured[index].OwnershipHistory }) == captured[index]
            && item.OwnershipHistory.SequenceEqual(captured[index].OwnershipHistory)).All(equal => equal);

    private static void ValidateInstructions(EstateSettlementPlan plan)
    {
        var snapshot = plan.Snapshot;
        var recipients = snapshot.Recipients.ToDictionary(heir => heir.Id);
        var expectedKind = snapshot.Wealth is null ? EstateSettlementKind.MissingFinance
            : snapshot.IsLivingAnchor ? EstateSettlementKind.LivingAnchorTransfer
            : recipients.Count == 0 ? EstateSettlementKind.LostEstate : EstateSettlementKind.Inheritance;
        Require(plan.Kind == expectedKind, "Estate plan kind does not match its snapshot.");
        var allocates = plan.Kind is EstateSettlementKind.Inheritance or EstateSettlementKind.LivingAnchorTransfer;
        ValidateAssets(plan.Houses, allocates ? snapshot.Houses.Select(asset => asset.Id) : [], recipients);
        ValidateAssets(plan.Farmland, allocates ? snapshot.Farmland.Select(asset => asset.Id) : [], recipients);
        ValidateAssets(plan.Heirlooms, allocates ? snapshot.Heirlooms.Select(asset => asset.Id) : [], recipients);
        Require(plan.Cash.Select(item => item.RecipientId).SequenceEqual(
            allocates ? snapshot.Recipients.Select(heir => heir.Id) : []),
            "Estate cash allocations do not match its ordered recipients.");
        Require(plan.Cash.All(item => recipients[item.RecipientId].IsPending == item.IsPending),
            "Estate cash destination does not match its recipient.");
        var expectedCash = !allocates ? 0m : plan.Kind == EstateSettlementKind.LivingAnchorTransfer
            ? snapshot.Wealth!.Value : Math.Round(snapshot.Wealth!.Value, 0, MidpointRounding.AwayFromZero);
        Require(plan.Cash.Sum(item => item.Amount) == expectedCash, "Estate cash/debt allocations are not conserved.");
    }

    private static void ValidateAssets(IReadOnlyList<EstateAssetAllocation> allocations,
        IEnumerable<Guid> expectedIds, IReadOnlyDictionary<Guid, EstateRecipient> recipients)
    {
        Require(allocations.Select(item => item.AssetId).SequenceEqual(expectedIds)
            && allocations.Select(item => item.AssetId).Distinct().Count() == allocations.Count,
            "An estate asset is missing, duplicated, or out of order in the plan.");
        Require(allocations.All(item => recipients.TryGetValue(item.RecipientId, out var heir)
            && heir.IsPending == item.IsPending), "An estate asset has an invalid recipient.");
    }
}
