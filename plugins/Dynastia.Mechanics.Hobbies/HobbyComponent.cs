namespace Dynastia.Mechanics.Hobbies;

public sealed class HobbyComponent
{
    public int HobbyCapacity { get; set; }

    public List<string> HobbyIds { get; set; } = [];

    public int LastProcessedYear { get; set; }
}
