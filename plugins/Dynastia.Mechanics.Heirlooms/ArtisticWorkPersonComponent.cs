using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

[PersistedComponentId("heirlooms.artistic_works")]
public sealed class ArtisticWorkPersonComponent
{
    public List<string> MasterGuaranteeCompletedCraftIds { get; set; } = [];

    public Dictionary<string, int> LastProductionYearByCraft { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
