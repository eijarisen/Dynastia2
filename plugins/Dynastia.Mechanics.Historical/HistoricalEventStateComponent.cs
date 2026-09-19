using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

[PersistedComponentId("historical.events")]
public sealed class HistoricalEventStateComponent
{
    public HashSet<string> HandledEvents { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

[PersistedComponentId("historical.external_residence")]
public sealed class ExternalResidenceComponent
{
    public string DestinationLabel { get; set; } = string.Empty;
    public int DepartureYear { get; set; }
    public string CauseEventId { get; set; } = string.Empty;
    public bool Forced { get; set; }
}
