using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Wellbeing;

internal sealed class RecoveryActivityCatalog
{
    private const int TechnologyFreezeYear =
        2026;

    private const string DataPath =
        "Common/recovery_activities_time_based.json";

    private readonly IReadOnlyList<
        RecoveryActivityDefinition> _activities;

    private RecoveryActivityCatalog(
        IReadOnlyList<RecoveryActivityDefinition> activities)
    {
        _activities =
            activities;
    }

    public static RecoveryActivityCatalog Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(
            data);

        var text =
            data.ReadText(
                DataPath);

        var activities =
            JsonSerializer.Deserialize<
                List<RecoveryActivityDefinition>>(
                text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive =
                        true
                })
            ?? [];

        Validate(
            activities);

        return new RecoveryActivityCatalog(
            activities);
    }

    public RecoveryActivityDefinition Select(
        int gameYear,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(
            random);

        var effectiveYear =
            Math.Min(
                gameYear,
                TechnologyFreezeYear);

        var available =
            _activities
                .Where(
                    activity =>
                        activity.StartYear
                            <= effectiveYear
                        && (
                            activity.EndYear
                                is null
                            || activity.EndYear.Value
                                >= effectiveYear
                        ))
                .ToList();

        if (available.Count == 0)
        {
            throw new InvalidOperationException(
                $"No recovery activity is available " +
                $"for year {gameYear}.");
        }

        var totalWeight =
            available.Sum(
                activity =>
                    activity.Weight);

        var roll =
            random.NextDouble()
            * totalWeight;

        foreach (var activity in
            available)
        {
            if (roll
                < activity.Weight)
            {
                return activity;
            }

            roll -=
                activity.Weight;
        }

        return available[^1];
    }

    private static void Validate(
        IReadOnlyList<
            RecoveryActivityDefinition> activities)
    {
        if (activities.Count == 0)
        {
            throw new InvalidDataException(
                $"{DataPath} is empty.");
        }

        if (activities
            .Select(
                activity =>
                    activity.Id)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Count()
            != activities.Count)
        {
            throw new InvalidDataException(
                $"{DataPath} contains duplicate IDs.");
        }

        foreach (var activity in
            activities)
        {
            if (string.IsNullOrWhiteSpace(
                    activity.Id)
                || string.IsNullOrWhiteSpace(
                    activity.Text))
            {
                throw new InvalidDataException(
                    $"{DataPath} contains an activity " +
                    "without an ID or text.");
            }

            if (activity.StartYear
                < GameCalendarConfiguration.GameStartYear)
            {
                throw new InvalidDataException(
                    $"{activity.Id}: startYear may not " +
                    $"precede {GameCalendarConfiguration.GameStartYear}.");
            }

            if (activity.EndYear is int endYear
                && endYear < activity.StartYear)
            {
                throw new InvalidDataException(
                    $"{activity.Id}: endYear precedes " +
                    "startYear.");
            }

            if (activity.Weight <= 0)
            {
                throw new InvalidDataException(
                    $"{activity.Id}: weight must be " +
                    "positive.");
            }
        }

        foreach (var probeYear in
            new[]
            {
                GameCalendarConfiguration.GameStartYear,
                1905,
                1925,
                1943,
                1965,
                1998,
                2015,
                2026
            })
        {
            if (!activities.Any(
                    activity =>
                        activity.StartYear
                            <= probeYear
                        && (
                            activity.EndYear
                                is null
                            || activity.EndYear.Value
                                >= probeYear
                        )))
            {
                throw new InvalidDataException(
                    $"{DataPath} has no recovery pool " +
                    $"for year {probeYear}.");
            }
        }
    }
}
