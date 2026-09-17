using Dynastia.Contracts;

namespace Dynastia.Mechanics.StatImprovements;

[PersistedComponentId("stat_improvements.paid")]
public sealed class PaidStatImprovementComponent
{
    public int? LastImprovementYear { get; set; }
}
