using Dynastia.Contracts;

namespace Dynastia.Mechanics.Status;

[PersistedComponentId("status.person")]
public sealed class StatusComponent
{
    public double InheritedRenown { get; set; }
    public double InheritedReputation { get; set; }
    public double PersistentRenownDelta { get; set; }
    public double PersistentReputationDelta { get; set; }
    public bool AdultInheritanceSeeded { get; set; }
    public string LocalTownId { get; set; } = string.Empty;
    public int LocalTownSinceYear { get; set; }
    public Dictionary<string, double> ArtisticRenownByCraft { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> ArtisticReputationByCraft { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
