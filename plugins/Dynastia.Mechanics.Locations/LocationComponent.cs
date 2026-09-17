using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

[PersistedComponentId("locations.person")]
public sealed class LocationComponent
{
    public TownInfo? Birthplace { get; set; }

    public TownInfo? HomeTown { get; set; }

    public TownInfo? DeathTown { get; set; }
}
