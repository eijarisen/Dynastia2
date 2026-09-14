using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public sealed class RareEventAvailabilityCatalog
{
    private const string DataPath =
        "Common/rare_event_availability.csv";

    private readonly IReadOnlyDictionary<string, AvailabilityRule> _rules;

    private RareEventAvailabilityCatalog(
        IReadOnlyDictionary<string, AvailabilityRule> rules)
    {
        _rules = rules;
    }

    public static RareEventAvailabilityCatalog Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var rules = Parse(data.ReadText(DataPath));
        Validate(rules);

        return new RareEventAvailabilityCatalog(
            rules.ToDictionary(
                rule => rule.EventId,
                StringComparer.OrdinalIgnoreCase));
    }

    public bool IsAvailable(
        string eventId,
        int year)
    {
        if (!_rules.TryGetValue(eventId, out var rule))
        {
            throw new InvalidDataException(
                $"{DataPath} has no availability row for '{eventId}'.");
        }

        return year >= rule.StartYear
            && (rule.EndYear is null || year <= rule.EndYear.Value);
    }

    private static IReadOnlyList<AvailabilityRule> Parse(
        string text)
    {
        var lines = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2
            || !lines[0].Equals(
                "EventId,StartYear,EndYear",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{DataPath} has an unexpected header or is empty.");
        }

        var rules = new List<AvailabilityRule>();

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');

            if (fields.Length != 3)
            {
                throw new InvalidDataException(
                    $"Invalid {DataPath} row {index + 1}: expected 3 fields.");
            }

            rules.Add(
                new AvailabilityRule(
                    fields[0].Trim(),
                    ParseInt(fields[1], index),
                    string.IsNullOrWhiteSpace(fields[2])
                        ? null
                        : ParseInt(fields[2], index)));
        }

        return rules;
    }

    private static void Validate(
        IReadOnlyList<AvailabilityRule> rules)
    {
        if (rules.Count == 0)
        {
            throw new InvalidDataException(
                $"{DataPath} contains no rules.");
        }

        if (rules.Select(rule => rule.EventId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != rules.Count)
        {
            throw new InvalidDataException(
                $"{DataPath} contains duplicate event IDs.");
        }

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.EventId))
            {
                throw new InvalidDataException(
                    $"{DataPath} contains an empty event ID.");
            }

            if (rule.StartYear < GameCalendarConfiguration.GameStartYear)
            {
                throw new InvalidDataException(
                    $"{rule.EventId}: start year may not precede " +
                    $"{GameCalendarConfiguration.GameStartYear}.");
            }

            if (rule.EndYear is int endYear
                && endYear < rule.StartYear)
            {
                throw new InvalidDataException(
                    $"{rule.EventId}: end year precedes start year.");
            }
        }
    }

    private static int ParseInt(
        string value,
        int rowIndex)
    {
        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new InvalidDataException(
                $"Invalid year in {DataPath} row {rowIndex + 2}.");
        }

        return parsed;
    }

    private sealed record AvailabilityRule(
        string EventId,
        int StartYear,
        int? EndYear);
}
