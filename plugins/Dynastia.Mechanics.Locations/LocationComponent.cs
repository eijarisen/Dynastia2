using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

[PersistedComponentId("locations.person")]
public sealed class LocationComponent
{
    public string? BirthplaceId { get; set; }

    public string? HomeTownId { get; set; }

    public string? DeathTownId { get; set; }

    public string? ForeignBirthplaceCity { get; set; }

    public string? ForeignBirthplaceCountry { get; set; }
}
