using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class HousePropertyState
{
    public Guid Id { get; set; }

    public TownInfo? Town { get; set; }

    public Guid? AssignedHeirId { get; set; }
}
