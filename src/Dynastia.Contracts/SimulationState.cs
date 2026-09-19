namespace Dynastia.Contracts;

public static class SimulationState
{
    public const string PeripheralInactiveTag =
        "simulation.peripheral_inactive";

    public const string ExternalResidenceTag =
        "state.external_residence";

    public static bool IsExternallyResident(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        return person.Tags.Has(
            ExternalResidenceTag);
    }

    public static bool IsInactive(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        return person.Tags.Has(
            PeripheralInactiveTag);
    }
}
