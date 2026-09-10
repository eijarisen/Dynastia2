using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class HousePropertyState
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public TownInfo? Town { get; set; }
}
