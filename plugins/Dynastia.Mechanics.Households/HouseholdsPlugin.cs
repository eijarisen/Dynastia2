using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed class HouseholdsPlugin : IGamePlugin
{
    private const decimal HousePurchasePrice = 10000m;
    private const decimal HouseSalePrice = 8000m;
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

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year-system registry is unavailable.");

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
                career,
                events);

        context.AddService<IHouseholdService>(
            households);

        systems.Register(
            new HouseholdReconcileYearSystem(
                households,
                "households.status_reconcile",
                YearPhase.Status,
                before:
                    Array.Empty<string>(),
                after:
                    Array.Empty<string>()));

        systems.Register(
            new HouseholdReconcileYearSystem(
                households,
                "households.inheritance_reconcile",
                YearPhase.Inheritance,
                before:
                    ["inheritance.estate_settlement"],
                after:
                    ["adoption.child_placement"]));

        systems.Register(
            new HouseholdReconcileYearSystem(
                households,
                "households.post_inheritance_reconcile",
                YearPhase.DerivedState,
                before:
                    Array.Empty<string>(),
                after:
                    Array.Empty<string>()));

        systems.Register(
            new NannyNeedReconcileYearSystem(
                households,
                economy,
                family,
                events,
                "households.nanny_need_prefinance",
                YearPhase.QueuedActionsEarly,
                before:
                    Array.Empty<string>(),
                after:
                    ["actions.queued.early"]));

        systems.Register(
            new NannyNeedReconcileYearSystem(
                households,
                economy,
                family,
                events,
                "households.nanny_need_postyear",
                YearPhase.DerivedState,
                before:
                    Array.Empty<string>(),
                after:
                    ["households.post_inheritance_reconcile"]));

        systems.Register(
            new AutonomousHouseholdDecisionSystem(
                gameState,
                households,
                actions,
                economy,
                random));

        events.EventPublished +=
            (_, gameEvent) =>
            {
                households.RecordFamilyNewsVisibility(
                    gameEvent);

                households.UpdatePeripheralRelationshipState(
                    gameEvent);

                if (gameEvent.Type.Equals(
                    "game.started",
                    StringComparison.OrdinalIgnoreCase))
                {
                    households.ReconcileHouseholds();
                }
            };

        healthModifiers.Register(
            new HouseholdHealthModifierProvider(
                households,
                family,
                stats,
                career));

        _ =
            new FamilyNannyTracker(
                gameState,
                family,
                economy,
                events);

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
                Id =
                    "household.buy_house",

                Label =
                    "Buy a House (10,000 zł)",

                Description =
                    "Purchase one house immediately. If this is the " +
                    "household's first owned house, it becomes the residence. " +
                    "Every additional house is rented automatically.",

                Mode =
                    ActionExecutionMode.Immediate,

                IsAvailable =
                    actionContext =>
                    {
                        if (!CanActOnSelf(
                            actionContext))
                        {
                            return false;
                        }

                        var household =
                            economy.GetHousehold(
                                actionContext.Actor);

                        return household is not null
                            && household.Wealth
                                >= HousePurchasePrice;
                    },

                Execute =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var household =
                            economy.GetHousehold(
                                actor);

                        if (household is null
                            || household.Wealth
                                < HousePurchasePrice)
                        {
                            return new GameActionResult(
                                false);
                        }

                        economy.ChangeWealth(
                            actor,
                            -HousePurchasePrice);

                        var house =
                            economy.AddHouse(
                                actor);

                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "household.house_bought",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    actor.Id,

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["amount"] =
                                            HousePurchasePrice.ToString(),

                                        ["town"] =
                                            house.Town.Town,

                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            $"bought a house in " +
                                            $"{house.Town.Town} for " +
                                            $"{HousePurchasePrice:N0} zł."
                                    }
                            });

                        return new GameActionResult(
                            true);
                    }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id =
                    "household.sell_house",

                Label =
                    "Sell a House (8,000 zł)",

                Description =
                    "Sell one rented investment house immediately for " +
                    "8,000 zł. The household's residence cannot be sold.",

                Mode =
                    ActionExecutionMode.Immediate,

                IsAvailable =
                    actionContext =>
                    {
                        if (!CanActOnSelf(
                            actionContext))
                        {
                            return false;
                        }

                        var household =
                            economy.GetHousehold(
                                actionContext.Actor);

                        return household is not null
                            && household.HousesOwned > 1;
                    },

                Execute =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var sold =
                            economy.TakeAdditionalHouse(
                                actor);

                        if (sold is null)
                        {
                            return new GameActionResult(
                                false);
                        }

                        economy.ChangeWealth(
                            actor,
                            HouseSalePrice);

                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "household.house_sold",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    actor.Id,

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["amount"] =
                                            HouseSalePrice.ToString(),

                                        ["town"] =
                                            sold.Town.Town,

                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            $"sold the house in " +
                                            $"{sold.Town.Town} for " +
                                            $"{HouseSalePrice:N0} zł."
                                    }
                            });

                        return new GameActionResult(
                            true);
                    }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id =
                    "household.give_house_to_son",

                Label =
                    "Give House to Son",

                Description =
                    "Give one rented investment house to the selected living " +
                    "son. The house keeps its town. A minor receives the " +
                    "exact same property when he reaches adulthood.",

                Mode =
                    ActionExecutionMode.Immediate,

                IsAvailable =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var target =
                            actionContext.Target;

                        if (!actor.Tags.Has(
                                "state.alive")
                            || !actor.Tags.Has(
                                "control.playable")
                            || !target.Tags.Has(
                                "state.alive")
                            || target.Id == actor.Id
                            || family.GetSex(
                                target) != Sex.Male)
                        {
                            return false;
                        }

                        if (!family
                            .GetChildren(
                                actor)
                            .Any(
                                child =>
                                    child.Id
                                    == target.Id))
                        {
                            return false;
                        }

                        var household =
                            economy.GetHousehold(
                                actor);

                        return household is not null
                            && household.HousesOwned > 1;
                    },

                Execute =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var son =
                            actionContext.Target;

                        if (!son.Tags.Has(
                                "state.alive")
                            || family.GetSex(
                                son) != Sex.Male
                            || !family
                                .GetChildren(
                                    actor)
                                .Any(
                                    child =>
                                        child.Id
                                        == son.Id))
                        {
                            return new GameActionResult(
                                false);
                        }

                        var gifted =
                            economy.TakeAdditionalHouse(
                                actor);

                        if (gifted is null)
                        {
                            return new GameActionResult(
                                false);
                        }

                        if (son.Age >= 18)
                        {
                            economy.EnsureHousehold(
                                son);

                            economy.AddExistingHouse(
                                son,
                                gifted);

                            events.Publish(
                                new GameEvent
                                {
                                    Type =
                                        "household.house_given",

                                    Year =
                                        actionContext.GameState.Year,

                                    SubjectId =
                                        actor.Id,

                                    RelatedPersonIds =
                                        [son.Id],

                                    Data =
                                        new Dictionary<string, string>
                                        {
                                            ["town"] =
                                                gifted.Town.Town,

                                            ["text"] =
                                                $"{family.GetDisplayName(actor)} " +
                                                $"gave the house in " +
                                                $"{gifted.Town.Town} to their son, " +
                                                $"{family.GetDisplayName(son)}."
                                        }
                                });
                        }
                        else
                        {
                            economy.AddPendingHouse(
                                son,
                                gifted);

                            events.Publish(
                                new GameEvent
                                {
                                    Type =
                                        "household.house_promised",

                                    Year =
                                        actionContext.GameState.Year,

                                    SubjectId =
                                        actor.Id,

                                    RelatedPersonIds =
                                        [son.Id],

                                    Data =
                                        new Dictionary<string, string>
                                        {
                                            ["town"] =
                                                gifted.Town.Town,

                                            ["text"] =
                                                $"{family.GetDisplayName(actor)} " +
                                                $"promised the house in " +
                                                $"{gifted.Town.Town} to their son, " +
                                                $"{family.GetDisplayName(son)}, " +
                                                "upon adulthood."
                                        }
                                });
                        }

                        return new GameActionResult(
                            true);
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
                    "The annual nanny cost is charged during finances. " +
                    "The service ends automatically once the household is no longer strained by young children.",
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
                Id =
                    "household.ask_daughter_nanny",

                Label =
                    "Ask to Become Nanny",

                Description =
                    "Ask the selected adult unmarried and unemployed daughter " +
                    "who still lives in this household to care for the younger " +
                    "children for free. Her help removes the large-family strain. " +
                    "It ends automatically if she dies, finds work, marries, or " +
                    "the household no longer has large-family strain.",

                Mode =
                    ActionExecutionMode.Queued,

                QueuePhase =
                    YearPhase.QueuedActionsEarly,

                IsAvailable =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var daughter =
                            actionContext.Target;

                        if (!actor.Tags.Has(
                                "state.alive")
                            || !actor.Tags.Has(
                                "control.playable")
                            || !daughter.Tags.Has(
                                "state.alive")
                            || daughter.Tags.Has(
                                "state.dead")
                            || daughter.Id == actor.Id
                            || daughter.Age < 18
                            || family.GetSex(
                                daughter) != Sex.Female
                            || family.GetSpouse(
                                daughter) is not null
                            || !family.GetChildren(
                                actor)
                                .Any(
                                    child =>
                                        child.Id
                                        == daughter.Id)
                            || households.ResolveHouseholdHead(
                                daughter)?.Id
                                != actor.Id)
                        {
                            return false;
                        }

                        var finance =
                            economy.GetHousehold(
                                actor);

                        var status =
                            households.GetStatus(
                                actor);

                        var daughterCareer =
                            career.GetCareer(
                                daughter);

                        return finance is not null
                            && status is not null
                            && !status.HasNannyReference
                            && status.UnderageChildren
                                > status.BaseChildCapacity
                            && !daughterCareer.IsRetired
                            && daughterCareer.JobLevel == 0;
                    },

                Execute =
                    actionContext =>
                    {
                        var actor =
                            actionContext.Actor;

                        var daughter =
                            actionContext.Target;

                        var finance =
                            economy.GetHousehold(
                                actor);

                        var status =
                            households.GetStatus(
                                actor);

                        var daughterCareer =
                            career.GetCareer(
                                daughter);

                        if (finance is null
                            || status is null
                            || status.HasNannyReference
                            || status.UnderageChildren
                                <= status.BaseChildCapacity
                            || daughterCareer.IsRetired
                            || daughterCareer.JobLevel != 0
                            || family.GetSpouse(
                                daughter) is not null
                            || households.ResolveHouseholdHead(
                                daughter)?.Id
                                != actor.Id)
                        {
                            return new GameActionResult(
                                false);
                        }

                        daughter.Tags.Add(
                            FamilyNannyTracker.FamilyNannyTag);

                        economy.SetNanny(
                            actor,
                            daughter.Id);

                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "household.family_nanny_started",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    actor.Id,

                                RelatedPersonIds =
                                    [daughter.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["text"] =
                                            $"{family.GetDisplayName(daughter)} " +
                                            "agreed to care for the younger children " +
                                            "as the family's nanny without pay."
                                    }
                            });

                        return new GameActionResult(
                            true);
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

                    var nanny =
                        households.GetNanny(
                            actionContext.Actor);

                    return nanny is not null
                        && !nanny.Tags.Has(
                            FamilyNannyTracker.FamilyNannyTag);
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

                    var familyNanny =
                        nanny.Tags.Has(
                            FamilyNannyTracker.FamilyNannyTag);

                    if (familyNanny)
                    {
                        nanny.Tags.Remove(
                            FamilyNannyTracker.FamilyNannyTag);
                    }
                    else
                    {
                        nanny.Tags.Remove("state.alive");
                        nanny.Tags.Remove("control.playable");
                        nanny.Tags.Add("state.dead");

                        // Source fire action stores only the death year.
                        nanny.DeathDate =
                            new GameDate(
                                actionContext.GameState.Year);
                    }

                    economy.SetNanny(
                        actor,
                        null);

                    events.Publish(
                        new GameEvent
                        {
                            Type =
                                familyNanny
                                    ? "household.family_nanny_ended"
                                    : "household.nanny_fired",

                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            RelatedPersonIds = [nanny.Id],
                            Data = new Dictionary<string, string>
                            {
                                ["reason"] =
                                    familyNanny
                                        ? "the family ended the arrangement"
                                        : string.Empty,

                                ["text"] =
                                    familyNanny
                                        ? $"{family.GetDisplayName(nanny)} " +
                                          "stopped helping as the family nanny."
                                        : $"{family.GetDisplayName(actor)} " +
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
