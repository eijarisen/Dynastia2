using Dynastia.Contracts;

namespace Dynastia.Mechanics.Church;

[PersistedComponentId("church.household_state")]
public sealed class ChurchHouseholdStateComponent
{
    public int LastWelfareYear { get; set; } = int.MinValue;
}
