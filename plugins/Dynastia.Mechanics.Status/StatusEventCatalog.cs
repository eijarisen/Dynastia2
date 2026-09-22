using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Status;

internal sealed class StatusEventCatalog
{
    private readonly Dictionary<string, (double Renown, double Reputation)> _events;
    private readonly Dictionary<string, (double Renown, double Reputation)> _crimes;
    private readonly Dictionary<string, (double Renown, double Reputation)> _historical;

    private StatusEventCatalog(
        Dictionary<string, (double, double)> events,
        Dictionary<string, (double, double)> crimes,
        Dictionary<string, (double, double)> historical)
    {
        _events = events;
        _crimes = crimes;
        _historical = historical;
    }

    public static StatusEventCatalog Load(IGameDataService data)
    {
        var events = Parse(
            data.ReadText("LocalSociety/status_event_effects.csv"),
            "EventType");
        foreach (var entry in Parse(
            data.ReadText("LocalSociety/status_extension_event_effects.csv"),
            "EventType"))
        {
            events[entry.Key] = entry.Value;
        }

        return new StatusEventCatalog(
            events,
            Parse(data.ReadText("LocalSociety/crime_status_effects.csv"), "CrimeCategory"),
            Parse(data.ReadText("LocalSociety/historical_status_effects.csv"), "HistoricalEventId"));
    }

    public bool TryGetEvent(string id, out (double Renown, double Reputation) delta) => _events.TryGetValue(id, out delta);
    public bool TryGetCrime(string id, out (double Renown, double Reputation) delta) => _crimes.TryGetValue(id, out delta);
    public bool TryGetHistorical(string id, out (double Renown, double Reputation) delta) => _historical.TryGetValue(id, out delta);

    private static Dictionary<string, (double Renown, double Reputation)> Parse(string text, string keyHeader)
    {
        var lines = text.Replace("\uFEFF", string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
            return new(StringComparer.OrdinalIgnoreCase);
        var headers = lines[0].Split(',');
        var keyIndex = Array.FindIndex(headers, h => h.Equals(keyHeader, StringComparison.OrdinalIgnoreCase));
        var renownIndex = Array.FindIndex(headers, h => h.StartsWith("RenownDelta", StringComparison.OrdinalIgnoreCase));
        var reputationIndex = Array.FindIndex(headers, h => h.StartsWith("ReputationDelta", StringComparison.OrdinalIgnoreCase));
        var result = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(',');
            if (keyIndex < 0 || renownIndex < 0 || reputationIndex < 0 || cells.Length <= Math.Max(keyIndex, Math.Max(renownIndex, reputationIndex)))
                continue;
            var key = cells[keyIndex].Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;
            _ = double.TryParse(cells[renownIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var renown);
            _ = double.TryParse(cells[reputationIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var reputation);
            result[key] = (renown, reputation);
        }
        return result;
    }
}
