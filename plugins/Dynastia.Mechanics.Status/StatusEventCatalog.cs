using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Status;

internal sealed record StatusEventEffect(
    double Renown,
    double Reputation,
    string TargetRule);

internal sealed class StatusEventCatalog
{
    private readonly Dictionary<string, IReadOnlyList<StatusEventEffect>> _events;
    private readonly Dictionary<string, (double Renown, double Reputation)> _crimes;
    private readonly Dictionary<string, (double Renown, double Reputation)> _historical;

    private StatusEventCatalog(
        Dictionary<string, IReadOnlyList<StatusEventEffect>> events,
        Dictionary<string, (double, double)> crimes,
        Dictionary<string, (double, double)> historical)
    {
        _events = events;
        _crimes = crimes;
        _historical = historical;
    }

    public static StatusEventCatalog Load(IGameDataService data)
    {
        var events = ParseEventEffects(
            data.ReadText("LocalSociety/status_event_effects.csv"));

        foreach (var entry in ParseEventEffects(
            data.ReadText("LocalSociety/status_extension_event_effects.csv")))
        {
            if (!events.TryGetValue(entry.Key, out var existing))
            {
                events[entry.Key] = entry.Value;
                continue;
            }

            events[entry.Key] = existing
                .Concat(entry.Value)
                .ToArray();
        }

        return new StatusEventCatalog(
            events,
            ParseSimple(data.ReadText("LocalSociety/crime_status_effects.csv"), "CrimeCategory"),
            ParseSimple(data.ReadText("LocalSociety/historical_status_effects.csv"), "HistoricalEventId"));
    }

    public IReadOnlyList<StatusEventEffect> GetEventEffects(string id) =>
        _events.TryGetValue(id, out var effects)
            ? effects
            : [];

    public bool TryGetCrime(string id, out (double Renown, double Reputation) delta) =>
        _crimes.TryGetValue(id, out delta);

    public bool TryGetHistorical(string id, out (double Renown, double Reputation) delta) =>
        _historical.TryGetValue(id, out delta);

    private static Dictionary<string, IReadOnlyList<StatusEventEffect>> ParseEventEffects(
        string text)
    {
        var lines = SplitLines(text);
        var result = new Dictionary<string, List<StatusEventEffect>>(
            StringComparer.OrdinalIgnoreCase);

        if (lines.Length <= 1)
            return new Dictionary<string, IReadOnlyList<StatusEventEffect>>(
                StringComparer.OrdinalIgnoreCase);

        var headers = lines[0].Split(',');
        var keyIndex = FindHeader(headers, "EventType");
        var renownIndex = FindHeaderStarting(headers, "RenownDelta");
        var reputationIndex = FindHeaderStarting(headers, "ReputationDelta");
        var targetIndex = FindHeader(headers, "TargetRule");

        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(',');
            var requiredMax = new[]
            {
                keyIndex,
                renownIndex,
                reputationIndex,
                targetIndex
            }.Max();

            if (keyIndex < 0
                || renownIndex < 0
                || reputationIndex < 0
                || targetIndex < 0
                || cells.Length <= requiredMax)
            {
                continue;
            }

            var key = cells[keyIndex].Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            _ = double.TryParse(
                cells[renownIndex],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var renown);
            _ = double.TryParse(
                cells[reputationIndex],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var reputation);

            if (!result.TryGetValue(key, out var effects))
            {
                effects = [];
                result[key] = effects;
            }

            effects.Add(new StatusEventEffect(
                renown,
                reputation,
                cells[targetIndex].Trim()));
        }

        return result.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<StatusEventEffect>)entry.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, (double Renown, double Reputation)> ParseSimple(
        string text,
        string keyHeader)
    {
        var lines = SplitLines(text);
        if (lines.Length <= 1)
            return new(StringComparer.OrdinalIgnoreCase);

        var headers = lines[0].Split(',');
        var keyIndex = FindHeader(headers, keyHeader);
        var renownIndex = FindHeaderStarting(headers, "RenownDelta");
        var reputationIndex = FindHeaderStarting(headers, "ReputationDelta");
        var result = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(',');
            if (keyIndex < 0
                || renownIndex < 0
                || reputationIndex < 0
                || cells.Length <= Math.Max(keyIndex, Math.Max(renownIndex, reputationIndex)))
            {
                continue;
            }

            var key = cells[keyIndex].Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            _ = double.TryParse(cells[renownIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var renown);
            _ = double.TryParse(cells[reputationIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var reputation);
            result[key] = (renown, reputation);
        }

        return result;
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\uFEFF", string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private static int FindHeader(string[] headers, string name) =>
        Array.FindIndex(
            headers,
            header => header.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static int FindHeaderStarting(string[] headers, string name) =>
        Array.FindIndex(
            headers,
            header => header.StartsWith(name, StringComparison.OrdinalIgnoreCase));
}
