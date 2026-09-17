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

        var activities =
            CatalogValidation.DeserializeJson<
                List<RecoveryActivityDefinition>>(
                data,
                DataPath,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive =
                        true
                });

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
            throw CatalogValidation.Error(
                DataPath,
                "at least one recovery activity",
                field: "Root",
                value: activities.Count);
        }

        var ids = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < activities.Count; index++)
        {
            var activity = activities[index];
            var item = string.IsNullOrWhiteSpace(activity.Id)
                ? $"index {index}"
                : activity.Id;

            if (string.IsNullOrWhiteSpace(activity.Id))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a non-empty activity ID",
                    item: item,
                    field: "id",
                    value: activity.Id);
            }

            if (string.IsNullOrWhiteSpace(activity.Text))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "non-empty activity text",
                    item: activity.Id,
                    field: "text",
                    value: activity.Text);
            }

            if (!ids.TryAdd(activity.Id, index))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a unique ID; first defined at item index {ids[activity.Id]}",
                    item: activity.Id,
                    field: "id",
                    value: activity.Id);
            }

            if (activity.StartYear
                < GameCalendarConfiguration.GameStartYear)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a year at or after {GameCalendarConfiguration.GameStartYear}",
                    item: activity.Id,
                    field: "startYear",
                    value: activity.StartYear);
            }

            if (activity.EndYear is int endYear
                && endYear < activity.StartYear)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a year at or after startYear ({activity.StartYear})",
                    item: activity.Id,
                    field: "endYear",
                    value: endYear);
            }

            if (activity.Weight <= 0)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a number greater than 0",
                    item: activity.Id,
                    field: "weight",
                    value: activity.Weight);
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
                throw CatalogValidation.Error(
                    DataPath,
                    $"at least one recovery activity available in year {probeYear}",
                    item: $"year {probeYear}",
                    field: "era coverage",
                    value: "<missing>");
            }
        }
    }

}
