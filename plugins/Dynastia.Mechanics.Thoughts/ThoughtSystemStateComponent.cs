using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

[PersistedComponentId("thoughts.system_state")]
public sealed class ThoughtSystemStateComponent
{
    public int LastProcessedEventCount { get; set; }

    public int LastGeneratedYear { get; set; }
}
