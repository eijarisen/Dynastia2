namespace Dynastia.Mechanics.Economy;

public sealed class HousePropertyState
{
    public Guid Id { get; set; }

    public string TownId { get; set; } = string.Empty;

    public Guid? AssignedHeirId { get; set; }
}
