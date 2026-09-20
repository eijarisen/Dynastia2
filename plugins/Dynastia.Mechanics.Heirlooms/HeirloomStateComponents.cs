using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

[PersistedComponentId("heirlooms.household")]
public sealed class HeirloomHouseholdComponent
{
    public Guid HouseholdId { get; set; }
    public List<HeirloomAssetState> Assets { get; } = [];
}

[PersistedComponentId("heirlooms.pending")]
public sealed class PendingHeirloomComponent
{
    public List<HeirloomAssetState> Assets { get; } = [];
}

[PersistedComponentId("heirlooms.person_triggers")]
public sealed class HeirloomPersonTriggerComponent
{
    public HashSet<string> CompletedTriggerIds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

[PersistedComponentId("heirlooms.household_milestones")]
public sealed class HeirloomHouseholdMilestoneComponent
{
    public Guid HouseholdId { get; set; }
    public HashSet<string> RolledWealthTierIds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> RolledHistoricalEventIds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
