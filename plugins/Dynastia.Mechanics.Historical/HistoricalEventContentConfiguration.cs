namespace Dynastia.Mechanics.Historical;

internal static class HistoricalEventContentConfiguration
{
    // H1/H2 keep this at 0 while the framework and migration infrastructure
    // are installed. H3 enables the pre-war catalog; H4 enables all supplied
    // content through 2026.
    public const int MaximumEnabledStartYear = 2026;

    public static bool IsEnabled(HistoricalScheduledEvent historicalEvent) =>
        MaximumEnabledStartYear >= historicalEvent.StartYear;
}
