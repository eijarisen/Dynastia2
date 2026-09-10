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

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

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
            JsonSerializer.Deserialize<
                List<BirthConditionDefinition>>(
                    data.ReadText(
                        BirthConditionsPath),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })
            ?? throw new InvalidDataException(
                $"Could not read {BirthConditionsPath}.");

        ValidateBirthConditions(
            birthConditions);

        actions.Register(
            CreateTryForBabyAction(
                family));

        systems.Register(
            new ReproductionYearSystem(
                family,
                stats,
                health,
                data,
                random,
                calendar,
                events,
                birthConditions));

        context.Log(
            "Reproduction mechanics registered.");
    }

    private static GameActionDefinition
        CreateTryForBabyAction(
            IFamilyService family)
    {
        return new GameActionDefinition
        {
            Id =
                "reproduction.try_for_baby",

            Label =
                "Try for a Baby",

            Description =
                "Attempt to have a child with your spouse. " +
                "Success depends on both partners' Fertility " +
                "and the woman's age. Pregnancy remains possible " +
                "through age 45, but becomes rare after 40.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                {
                    if (actionContext.Actor.Id
                        != actionContext.Target.Id)
                    {
                        return false;
                    }

                    var actor =
                        actionContext.Actor;

                    if (!actor.Tags.Has(
                            "state.alive")
                        || !actor.Tags.Has(
                            "control.playable"))
                    {
                        return false;
                    }

                    var spouse =
                        family.GetSpouse(
                            actor);

                    return spouse is not null
                        && spouse.Tags.Has(
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

    private static void ValidateBirthConditions(
        IReadOnlyList<BirthConditionDefinition> definitions)
    {
        if (definitions.Count == 0)
        {
            throw new InvalidDataException(
                "Birth condition data is empty.");
        }

        var ids =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var totalProbability =
            0.0;

        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(
                    definition.Id)
                || string.IsNullOrWhiteSpace(
                    definition.Name)
                || string.IsNullOrWhiteSpace(
                    definition.HealthConditionId))
            {
                throw new InvalidDataException(
                    "Every birth condition needs id, name " +
                    "and healthConditionId.");
            }

            if (!ids.Add(
                definition.Id))
            {
                throw new InvalidDataException(
                    $"Duplicate birth condition ID " +
                    $"'{definition.Id}'.");
            }

            if (definition.Probability <= 0
                || definition.Probability > 1)
            {
                throw new InvalidDataException(
                    $"Birth condition '{definition.Id}' " +
                    "must have probability > 0 and <= 1.");
            }

            totalProbability +=
                definition.Probability;
        }

        if (totalProbability > 1.0 + 1e-12)
        {
            throw new InvalidDataException(
                $"Birth condition probabilities add up to " +
                $"{totalProbability:P4}. They must total 100% or less.");
        }
    }
}
