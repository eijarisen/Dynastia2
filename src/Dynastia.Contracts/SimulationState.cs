namespace Dynastia.Contracts;

public static class SimulationState
{
    public const string PeripheralInactiveTag =
        "simulation.peripheral_inactive";

    public static bool IsInactive(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        return person.Tags.Has(
            PeripheralInactiveTag);
    }
}
