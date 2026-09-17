using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

public sealed class ReproductionPlugin : IGamePlugin
{
    private const string BirthConditionsPath =
        "Common/birth_conditions.json";

    public void Initialize(
        IGamePluginContext context)
    {
        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable.");

        var health =
            context.GetService<IHealthService>()
            ?? throw new InvalidOperationException(
                "Health service is unavailable.");

        var appearance =
            context.GetService<IAppearanceService>()
            ?? throw new InvalidOperationException(
                "Appearance service is unavailable.");

        var marriageSatisfaction =
            context.GetService<IMarriageSatisfactionService>()
            ?? throw new InvalidOperationException(
                "Marriage satisfaction service is unavailable.");

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var historical =
            context.GetService<IHistoricalActionVariantService>()
            ?? throw new InvalidOperationException(
                "Historical action variant service is unavailable.");

        var historicalNames =
            context.GetService<IHistoricalNameService>()
            ?? throw new InvalidOperationException(
                "Historical name service is unavailable.");

        var gameState =
            context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Game random service is unavailable.");

        var calendar =
            context.GetService<IGameCalendar>()
            ?? throw new InvalidOperationException(
                "Game calendar service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        var birthConditions =
            CatalogValidation.DeserializeJson<
                List<BirthConditionDefinition>>(
                    data,
                    BirthConditionsPath,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

        ValidateBirthConditions(
            birthConditions);

        var birthConditionContext =
            BirthConditionContextCatalog.Load(
                data,
                birthConditions.Select(condition => condition.Id));

        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    CreateTryForBabyAction(
                        family,
                        RequireHistoricalVariant(
                            historical,
                            "reproduction.try_for_baby",
                            gameState.Year))
                ]);

        systems.Register(
            new ReproductionYearSystem(
                family,
                stats,
                health,
                appearance,
                marriageSatisfaction,
                historicalNames,
                random,
                calendar,
                events,
                birthConditions,
                birthConditionContext));

        context.Log(
            "Reproduction mechanics registered.");
    }

    private static GameActionDefinition
        CreateTryForBabyAction(
            IFamilyService family,
            HistoricalActionVariant variant)
    {
        return new GameActionDefinition
        {
            Id =
                "reproduction.try_for_baby",

            Label =
                variant.Label,

            Description =
                variant.Description,

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    if (!actor.Tags.Has(
                            "state.alive")
                        || !actionContext.ActorHasControl)
                    {
                        return false;
                    }

                    var spouse =
                        family.GetSpouse(
                            actor);

                    if (spouse is null
                        || (actionContext.Target.Id != actor.Id
                            && actionContext.Target.Id != spouse.Id))
                    {
                        return false;
                    }

                    return spouse.Tags.Has(
                            "state.alive")
                        && family.GetSex(
                            spouse)
                            == Sex.Female
                        && spouse.Age <= 45
                        && !spouse.Tags.Has(
                            "state.imprisoned");
                },

            Execute =
                actionContext =>
                {
                    actionContext.Actor.Tags.Add(
                        "modifier.try_for_baby");

                    return new GameActionResult(
                        true);
                }
        };
    }


    private static HistoricalActionVariant RequireHistoricalVariant(
        IHistoricalActionVariantService historical,
        string actionId,
        int year)
    {
        return historical.GetVariant(actionId, year)
            ?? throw new InvalidDataException(
                $"Missing historical action data for '{actionId}' in {year}.");
    }

    private static void ValidateBirthConditions(
        IReadOnlyList<BirthConditionDefinition> definitions)
    {
        if (definitions.Count == 0)
        {
            throw CatalogValidation.Error(
                BirthConditionsPath,
                "at least one birth condition",
                field: "Items",
                value: 0);
        }

        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalProbability = 0.0;

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var item = string.IsNullOrWhiteSpace(definition.Id)
                ? $"index {index}"
                : definition.Id;

            if (string.IsNullOrWhiteSpace(definition.Id))
                throw CatalogValidation.Error(BirthConditionsPath, "a non-empty condition ID", item: item, field: "id", value: definition.Id);
            if (string.IsNullOrWhiteSpace(definition.Name))
                throw CatalogValidation.Error(BirthConditionsPath, "a non-empty condition name", item: item, field: "name", value: definition.Name);
            if (string.IsNullOrWhiteSpace(definition.HealthConditionId))
                throw CatalogValidation.Error(BirthConditionsPath, "a non-empty health condition ID", item: item, field: "healthConditionId", value: definition.HealthConditionId);

            if (!ids.TryAdd(definition.Id, index))
            {
                throw CatalogValidation.Error(
                    BirthConditionsPath,
                    $"a unique ID; first defined at item index {ids[definition.Id]}",
                    item: definition.Id,
                    field: "id",
                    value: definition.Id);
            }

            if (definition.Probability <= 0 || definition.Probability > 1)
            {
                throw CatalogValidation.Error(
                    BirthConditionsPath,
                    "a number greater than 0 and no greater than 1",
                    item: definition.Id,
                    field: "probability",
                    value: definition.Probability);
            }

            if (definition.StartYear < GameCalendarConfiguration.GameStartYear)
            {
                throw CatalogValidation.Error(
                    BirthConditionsPath,
                    $"a year at or after {GameCalendarConfiguration.GameStartYear}",
                    item: definition.Id,
                    field: "startYear",
                    value: definition.StartYear);
            }

            if (definition.EndYear is int endYear
                && endYear < definition.StartYear)
            {
                throw CatalogValidation.Error(
                    BirthConditionsPath,
                    $"a year at or after startYear ({definition.StartYear})",
                    item: definition.Id,
                    field: "endYear",
                    value: endYear);
            }

            totalProbability += definition.Probability;
        }

        if (totalProbability > 1.0 + 1e-12)
        {
            throw CatalogValidation.Error(
                BirthConditionsPath,
                "a combined probability no greater than 1",
                field: "probability",
                value: totalProbability);
        }
    }
}
