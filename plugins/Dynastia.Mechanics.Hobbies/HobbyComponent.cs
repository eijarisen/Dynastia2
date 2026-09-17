using Dynastia.Contracts;

namespace Dynastia.Mechanics.Hobbies;

[PersistedComponentId("hobbies.person")]
public sealed class HobbyComponent
{
    public int HobbyCapacity { get; set; }

    public List<string> HobbyIds { get; set; } = [];

    public int LastProcessedYear { get; set; }
}
