using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

[PersistedComponentId("reproduction.active_conception_attempt")]
public sealed class ActiveConceptionAttemptComponent
{
    public int AttemptYear { get; set; }
    public Guid MotherId { get; set; }
    public int MotherAgeAtAttempt { get; set; }
}
