using Dynastia.Contracts;

namespace Dynastia.Mechanics.Childhood;

[PersistedComponentId("childhood.happiness")]
public sealed class ChildHappinessComponent
{
    public int Value { get; set; } = 3;
    public int RecoveryBlockedThroughYear { get; set; } = int.MinValue;
}
