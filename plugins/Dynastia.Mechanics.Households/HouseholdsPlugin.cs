using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed class HouseholdsPlugin : IGamePlugin
{
    private const decimal HouseCost = 10000m;
    private const decimal RentIncome = 250m;
    private const decimal NannyCost = 250m;

    private const string FemaleNamesPath =
        "Names/polish_female.csv";

    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    public void Initialize(
        IGamePluginContext context)
    {
        var gameState =
            context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var career =
            context.GetService<ICareerService>()
            ?? throw new InvalidOperationException(
                "Career service is unavailable.");

        var education =
            context.GetService<IEducationService>()
            ?? throw new InvalidOperationException(
                "Education service is unavailable.");

        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable.");

        var health =
            context.GetService<IHealthService>()
            ?? throw new InvalidOperationException(
                "Health service is unavailable.");

        var healthModifiers =
            context.GetService<IAnnualHealthModifierRegistry>()
            ?? throw new InvalidOperationException(
                "Annual health modifier registry is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

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

        var households =
            new StandardHouseholdService(
                gameState,
                family,
                economy,
                career);

        context.AddService<IHouseholdService>(
            households);

        healthModifiers.Register(
            new HouseholdHealthModifierProvider(
                households,
                family,
                stats));

        RegisterPropertyActions(
            actions,
            family,
            economy,
            events);

        RegisterNannyActions(
            actions,
            gameState,
            family,
            economy,
            career,
            education,
            stats,
            health,
            households,
            events,
            data,
            random,
            calendar);

        context.Log(
            "Household mechanics registered.");
    }

    private static void RegisterPropertyActions(
        IActionRegistry actions,
        IFamilyService family,
        IEconomyService economy,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "household.buy_house",
                Label = "Buy a House ($10,000)",
                Description =
                    "Purchase one additional house immediately.",
                Mode = ActionExecutionMode.Immediate,

                IsAvailable = actionContext =>
                {
                    if (!CanActOnSelf(actionContext))
                        return false;

                    var household =
                        economy.GetHousehold(
                            actionContext.Actor);

                    return household is not null
                        && household.Wealth >= HouseCost;
                },

                Execute = actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var household =
                        economy.GetHousehold(actor);

                    if (household is null
                        || household.Wealth < HouseCost)
                    {
                        return new GameActionResult(false);
                    }

                    economy.ChangeWealth(
                        actor,
                        -HouseCost);

                    economy.SetHousesOwned(
                        actor,
                        household.HousesOwned + 1);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "household.house_bought",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["amount"] = HouseCost.ToString(),
                                ["text"] =
                                    $"{family.GetDisplayName(actor)} " +
                                    $"bought a new house for ${HouseCost:N0}."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "household.sell_house",
                Label = "Sell a House ($10,000)",
                Description =
                    "Sell one additional house immediately. " +
                    "The final residence cannot be sold.",
                Mode = ActionExecutionMode.Immediate,

                IsAvailable = actionContext =>
                {
                    if (!CanActOnSelf(actionContext))
                        return false;

                    var household =
                        economy.GetHousehold(
                            actionContext.Actor);

                    return household is not null
                        && household.HousesOwned > 1;
                },

                Execute = actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var household =
                        economy.GetHousehold(actor);

                    if (household is null
                        || household.HousesOwned <= 1)
                    {
                        return new GameActionResult(false);
                    }

                    economy.ChangeWealth(
                        actor,
                        HouseCost);

                    // Economy clamps RentedHouses to the new legal maximum.
                    economy.SetHousesOwned(
                        actor,
                        household.HousesOwned - 1);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "household.house_sold",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["amount"] = HouseCost.ToString(),
                                ["text"] =
                                    $"{family.GetDisplayName(actor)} " +
                                    $"sold a house for ${HouseCost:N0}."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "household.rent_house",
                Label = "Rent Out a House ($250/year)",
                Description =
                    "Rent out one spare house. At least one owned " +
                    "house remains reserved for the household.",
                Mode = ActionExecutionMode.Immediate,

                IsAvailable = actionContext =>
                {
                    if (!CanActOnSelf(actionContext))
                        return false;

                    var household =
                        economy.GetHousehold(
                            actionContext.Actor);

                    return household is not null
                        && household.HousesOwned
                            > household.RentedHouses + 1;
                },

                Execute = actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var household =
                        economy.GetHousehold(actor);

                    if (household is null
                        || household.HousesOwned
                            <= household.RentedHouses + 1)
                    {
                        return new GameActionResult(false);
                    }

                    economy.SetRentedHouses(
                        actor,
                        household.RentedHouses + 1);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "household.house_rented",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["annualIncome"] = RentIncome.ToString(),
                                ["text"] =
                                    $"{family.GetDisplayName(actor)} " +
                                    "started renting out one of their houses."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "household.give_house_to_son",
                Label = "Give House to Son",
                Description =
                    "Give one spare house to the selected living son. " +
                    "A minor receives it when he reaches adulthood.",
                Mode = ActionExecutionMode.Immediate,

                IsAvailable = actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var target =
                        actionContext.Target;

                    if (!actor.Tags.Has("state.alive")
                        || !actor.Tags.Has("control.playable")
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || family.GetSex(target) != Sex.Male)
                    {
                        return false;
                    }

                    if (!family.GetChildren(actor)
                        .Any(child => child.Id == target.Id))
                    {
                        return false;
                    }

                    var household =
                        economy.GetHousehold(actor);

                    return household is not null
                        && household.HousesOwned > 1;
                },

                Execute = actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var son =
                        actionContext.Target;

                    var household =
                        economy.GetHousehold(actor);

                    if (household is null
                        || household.HousesOwned <= 1
                        || !son.Tags.Has("state.alive")
                        || family.GetSex(son) != Sex.Male
                        || !family.GetChildren(actor)
                            .Any(child => child.Id == son.Id))
                    {
                        return new GameActionResult(false);
                    }

                    economy.SetHousesOwned(
                        actor,
                        household.HousesOwned - 1);

                    if (son.Age >= 18)
                    {
                        economy.EnsureHousehold(son);

                        var sonHousehold =
                            economy.GetHousehold(son)
                            ?? throw new InvalidOperationException(
                                "Adult son has no household state.");

                        economy.SetHousesOwned(
                            son,
                            sonHousehold.HousesOwned + 1);

                        events.Publish(
                            new GameEvent
                            {
                                Type = "household.house_given",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [son.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} " +
                                        $"gave a house to their son, " +
                                        $"{family.GetDisplayName(son)}."
                                }
                            });
                    }
                    else
                    {
                        economy.ChangePendingHouses(
                            son,
                            1);

                        events.Publish(
                            new GameEvent
                            {
                                Type = "household.house_promised",
                                Year = actionContext.GameState.Year,
                                SubjectId = actor.Id,
                                RelatedPersonIds = [son.Id],
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} " +
                                        $"promised a house to their son, " +
                                        $"{family.GetDisplayName(son)}, " +
                                        "upon adulthood."
                                }
                            });
                    }

                    return new GameActionResult(true);
                }
            });
    }

    private static void RegisterNannyActions(
        IActionRegistry actions,
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        ICareerService career,
        IEducationService education,
        IStatsService stats,
        IHealthService health,
        IHouseholdService households,
        IGameEventBus events,
        IGameDataService data,
        IGameRandom random,
        IGameCalendar calendar)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "household.hire_nanny",
                Label = "Hire a Nanny ($250/year)",
                Description =
                    "Hire help for an oversized household. " +
                    "The annual nanny cost is charged during finances.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    if (!CanActOnSelf(actionContext))
                        return false;

                    var actor =
                        actionContext.Actor;

                    var finance =
                        economy.GetHousehold(actor);

                    var status =
                        households.GetStatus(actor);

                    return finance is not null
                        && status is not null
                        && !status.HasNannyReference
                        && finance.Wealth >= NannyCost
                        && status.UnderageChildren
                            > status.BaseChildCapacity;
                },

                Execute = actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var finance =
                        economy.GetHousehold(actor);

                    var status =
                        households.GetStatus(actor);

                    if (finance is null
                        || status is null
                        || status.HasNannyReference
                        || finance.Wealth < NannyCost
                        || status.UnderageChildren
                            <= status.BaseChildCapacity)
                    {
                        return new GameActionResult(false);
                    }

                    var nanny =
                        gameState.CreatePerson(
                            RandomWeightedFrom(
                                data,
                                random,
                                FemaleNamesPath),
                            RandomWeightedFrom(
                                data,
                                random,
                                SurnamesPath),
                            random.NextInt(25, 45));

                    nanny.MaidenName =
                        nanny.Surname;

                    nanny.BirthDate =
                        RandomDateInYear(
                            gameState.Year - nanny.Age,
                            random,
                            calendar);

                    family.InitializePerson(
                        nanny,
                        Sex.Female);

                    nanny.Tags.Add("state.alive");
                    nanny.Tags.Add("age.adult");
                    nanny.Tags.Add("role.nanny");
                    nanny.Tags.Add("sexuality.heterosexual");

                    stats.EnsureStats(nanny);
                    health.EnsureHealth(nanny);

                    education.SetEducationLevel(
                        nanny,
                        0);

                    career.InitializeCareer(
                        nanny,
                        random.NextInt(0, 3),
                        random.NextInt(1, 5));

                    economy.SetNanny(
                        actor,
                        nanny.Id);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "household.nanny_hired",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            RelatedPersonIds = [nanny.Id],
                            Data = new Dictionary<string, string>
                            {
                                ["text"] =
                                    $"The {actor.Surname} family hired " +
                                    $"a nanny, {family.GetDisplayName(nanny)}, " +
                                    "to help with the children."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "household.fire_nanny",
                Label = "Fire Nanny",
                Description =
                    "Dismiss the current nanny before this year's " +
                    "household finances are calculated.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,

                IsAvailable = actionContext =>
                {
                    if (!CanActOnSelf(actionContext))
                        return false;

                    return economy.GetHousehold(
                        actionContext.Actor)
                        ?.NannyId is not null;
                },

                Execute = actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var nanny =
                        households.GetNanny(actor);

                    if (nanny is null)
                    {
                        // Normally unreachable because the nanny remains
                        // in gameState even after a natural death.
                        return new GameActionResult(false);
                    }

                    nanny.Tags.Remove("state.alive");
                    nanny.Tags.Remove("control.playable");
                    nanny.Tags.Add("state.dead");

                    // Source fire action stores only the death year.
                    nanny.DeathDate =
                        new GameDate(
                            actionContext.GameState.Year);

                    economy.SetNanny(
                        actor,
                        null);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "household.nanny_fired",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            RelatedPersonIds = [nanny.Id],
                            Data = new Dictionary<string, string>
                            {
                                ["text"] =
                                    $"{family.GetDisplayName(actor)} " +
                                    $"fired the nanny, " +
                                    $"{family.GetDisplayName(nanny)}."
                            }
                        });

                    return new GameActionResult(true);
                }
            });
    }

    private static bool CanActOnSelf(
        GameActionContext context)
    {
        return context.Actor.Id == context.Target.Id
            && context.Actor.Tags.Has("state.alive")
            && context.Actor.Tags.Has("control.playable")
            && !context.Actor.Tags.Has("state.imprisoned");
    }

    private static string RandomWeightedFrom(
        IGameDataService data,
        IGameRandom random,
        string relativePath)
    {
        var entries =
            data.GetWeightedStringList(
                relativePath);

        var total =
            entries.Sum(
                entry => (double)entry.Weight);

        var roll =
            random.NextDouble() * total;

        foreach (var entry in entries)
        {
            if (roll < entry.Weight)
                return entry.Value;

            roll -= entry.Weight;
        }

        return entries[^1].Value;
    }

    private static GameDate RandomDateInYear(
        int year,
        IGameRandom random,
        IGameCalendar calendar)
    {
        var month =
            random.NextInt(1, 12);

        var day =
            random.NextInt(
                1,
                calendar.GetDaysInMonth(
                    year,
                    month));

        return new GameDate(
            year,
            month,
            day);
    }
}
