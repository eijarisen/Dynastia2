using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

[PersistedComponentId("family.nationality")]
public sealed class NationalityComponent
{
    public string NationalityId { get; set; } =
        "polish";
}
