using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class HouseholdsPlugin
{
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
        IHistoricalNameService historicalNames,
        IGameRandom random,
        IGameCalendar calendar,
        IHistoricalActionVariantService historical)
    {
        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    WithHistoricalPresentation(
                        new GameActionDefinition
            {
                Id = "household.hire_nanny",
                Label = "Hire a Nanny (250 zł/year)",
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

                    var nannyNameSample =
                        random.NextDouble();

                    var nannySurname =
                        RandomWeightedFrom(
                            data,
                            random,
                            SurnamesPath);

                    var nannyAge =
                        random.NextInt(25, 45);

                    var nannyBirthYear =
                        gameState.Year - nannyAge;

                    var nanny =
                        gameState.CreatePerson(
                            historicalNames.GetRandomFirstName(
                                Sex.Female,
                                nannyBirthYear,
                                new FixedSampleGameRandom(
                                    nannyNameSample)),
                            nannySurname,
                            nannyAge);

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

                    var wording =
                        RequireHistoricalVariant(
                            historical,
                            "household.hire_nanny",
                            actionContext.GameState.Year);

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
                                    $"The {actor.Surname} family {wording.Narrative}: " +
                                    $"{family.GetDisplayName(nanny)}."
                            }
                        });

                    return new GameActionResult(true);
                }
                        },
                        historical,
                        gameState.Year)
                ]);

        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    WithHistoricalPresentation(
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

                        var wording =
                            RequireHistoricalVariant(
                                historical,
                                "household.ask_daughter_nanny",
                                actionContext.GameState.Year);

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
                                            $"{family.GetDisplayName(actor)} " +
                                            $"{wording.Narrative}. " +
                                            $"{family.GetDisplayName(daughter)} agreed and " +
                                            "began caring for the younger children without pay."
                                    }
                            });

                        return new GameActionResult(
                            true);
                    }
                        },
                        historical,
                        gameState.Year)
                ]);

        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    WithHistoricalPresentation(
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

                    var wording =
                        RequireHistoricalVariant(
                            historical,
                            "household.fire_nanny",
                            actionContext.GameState.Year);

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
                                        ? $"{family.GetDisplayName(nanny)}'s " +
                                          $"{career.GetStatusLabel(FamilyNannyTracker.FamilyNannyTag)} role ended."
                                        : $"{family.GetDisplayName(actor)} " +
                                          $"{wording.Narrative}: " +
                                          $"{family.GetDisplayName(nanny)}."
                            }
                        });

                    return new GameActionResult(true);
                }
                        },
                        historical,
                        gameState.Year)
                ]);
    }

    private static GameActionDefinition WithHistoricalPresentation(
        GameActionDefinition action,
        IHistoricalActionVariantService historical,
        int year)
    {
        var variant = RequireHistoricalVariant(
            historical,
            action.Id,
            year);

        return new GameActionDefinition
        {
            Id = action.Id,
            Label = variant.Label,
            Description = variant.Description,
            Mode = action.Mode,
            QueuePhase = action.QueuePhase,
            BypassGuards = action.BypassGuards,
            IsAvailable = action.IsAvailable,
            Execute = action.Execute
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

}
