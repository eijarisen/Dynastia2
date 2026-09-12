using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class RelationshipsPlugin : IGamePlugin
{
    private const string MaleNamesPath =
        "Names/polish_male.csv";

    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private static readonly double[]
        ArrangedMarriageChanceByAppeal =
            [0, 0.25, 0.35, 0.50, 0.60, 0.80];

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

        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable.");

        var health =
            context.GetService<IHealthService>()
            ?? throw new InvalidOperationException(
                "Health service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var education =
            context.GetService<IEducationService>()
            ?? throw new InvalidOperationException(
                "Education service is unavailable.");

        var career =
            context.GetService<ICareerService>()
            ?? throw new InvalidOperationException(
                "Career service is unavailable.");

        var households =
            context.GetService<IHouseholdService>()
            ?? throw new InvalidOperationException(
                "Household service is unavailable.");

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Random service is unavailable.");

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

        var breakups =
            new RelationshipBreakupService(
                family,
                health,
                economy,
                data,
                random,
                events);

        var marriageSatisfaction =
            new StandardMarriageSatisfactionService(
                gameState,
                family,
                stats,
                events);

        context.AddService<IMarriageSatisfactionService>(
            marriageSatisfaction);

        _ =
            new DivorcedParentsTracker(
                gameState,
                family,
                health,
                events);

        systems.Register(
            new DivorcedParentsStateYearSystem(
                family,
                health));

        actions.Register(
            CreateFindSpouseAction(
                family));

        actions.Register(
            CreateMarryOffDaughterAction(
                family,
                households,
                stats,
                health,
                education,
                career,
                data,
                random,
                calendar,
                events));

        actions.Register(
            CreateDivorceAction(
                family,
                breakups));

        actions.Register(
            CreateRepairMarriageAction(
                family,
                marriageSatisfaction,
                events));

        systems.Register(
            new SexualityYearSystem(
                family,
                random));

        systems.Register(
            new MarriageYearSystem(
                family,
                stats,
                career,
                data,
                random,
                calendar,
                events));

        systems.Register(
            new AffairYearSystem(
                family,
                random,
                breakups));

        systems.Register(
            new FemaleRemarriageYearSystem(
                family,
                stats,
                health,
                education,
                career,
                data,
                random,
                calendar,
                events));

        systems.Register(
            new MarriageSatisfactionYearSystem(
                marriageSatisfaction,
                family,
                stats,
                health,
                career,
                households,
                random,
                breakups));

        context.Log(
            "Relationship mechanics registered.");
    }

    private static GameActionDefinition
        CreateFindSpouseAction(
            IFamilyService family)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.find_spouse",

            Label =
                "Find a Spouse",

            Description =
                "Attempt to find a suitable spouse next year. " +
                "Success depends on Appeal.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                    actionContext.Actor.Id
                        == actionContext.Target.Id
                    && actionContext.Actor.Tags.Has(
                        "state.alive")
                    && actionContext.Actor.Tags.Has(
                        "control.playable")
                    && actionContext.Actor.Age >= 18
                    && family.GetSpouse(
                        actionContext.Actor) is null,

            Execute =
                actionContext =>
                {
                    actionContext.Actor.Tags.Add(
                        "modifier.find_spouse");

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static GameActionDefinition
        CreateMarryOffDaughterAction(
            IFamilyService family,
            IHouseholdService households,
            IStatsService stats,
            IHealthService health,
            IEducationService education,
            ICareerService career,
            IGameDataService data,
            IGameRandom random,
            IGameCalendar calendar,
            IGameEventBus events)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.marry_off_daughter",

            Label =
                "Marry Off",

            Description =
                "Try to find a husband for the selected adult " +
                "unmarried daughter who still belongs to this " +
                "household. Success depends on her Appeal.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                    IsEligibleDaughter(
                        actionContext.Actor,
                        actionContext.Target,
                        family,
                        households),

            Execute =
                actionContext =>
                {
                    var father =
                        actionContext.Actor;

                    var daughter =
                        actionContext.Target;

                    if (!IsEligibleDaughter(
                        father,
                        daughter,
                        family,
                        households))
                    {
                        return new GameActionResult(
                            false,
                            "The selected daughter is no longer eligible.");
                    }

                    var appeal =
                        stats.GetStats(
                            daughter)
                        .First(
                            stat =>
                                stat.Id.Equals(
                                    "appeal",
                                    StringComparison.OrdinalIgnoreCase))
                        .Value;

                    var chance =
                        ArrangedMarriageChanceByAppeal[
                            Math.Clamp(
                                appeal,
                                1,
                                5)];

                    if (random.NextDouble()
                        >= chance)
                    {
                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "relationship.marry_off_failed",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    father.Id,

                                RelatedPersonIds =
                                    [daughter.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["appeal"] =
                                            appeal.ToString(),

                                        ["chance"] =
                                            chance.ToString(
                                                "0.00"),

                                        ["text"] =
                                            $"{family.GetDisplayName(father)} " +
                                            $"tried to find a husband for " +
                                            $"{family.GetDisplayName(daughter)}, " +
                                            "but no suitable match was found."
                                    }
                            });

                        return new GameActionResult(
                            true);
                    }

                    CreateArrangedHusband(
                        actionContext.GameState,
                        father,
                        daughter,
                        family,
                        stats,
                        health,
                        education,
                        career,
                        data,
                        random,
                        calendar,
                        events,
                        chance);

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static bool IsEligibleDaughter(
        IPerson father,
        IPerson daughter,
        IFamilyService family,
        IHouseholdService households)
    {
        if (!father.Tags.Has(
                "state.alive")
            || !father.Tags.Has(
                "control.playable")
            || !daughter.Tags.Has(
                "state.alive")
            || daughter.Tags.Has(
                "state.dead")
            || daughter.Id == father.Id
            || daughter.Age < 18
            || family.GetSex(
                daughter) != Sex.Female
            || family.GetSpouse(
                daughter) is not null)
        {
            return false;
        }

        var isDaughter =
            family.GetChildren(
                father)
            .Any(
                child =>
                    child.Id
                    == daughter.Id);

        if (!isDaughter)
            return false;

        return households
            .ResolveHouseholdHead(
                daughter)?
            .Id
            == father.Id;
    }

    private static void CreateArrangedHusband(
        IGameState gameState,
        IPerson father,
        IPerson daughter,
        IFamilyService family,
        IStatsService stats,
        IHealthService health,
        IEducationService education,
        ICareerService career,
        IGameDataService data,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events,
        double chance)
    {
        var husband =
            gameState.CreatePerson(
                RandomWeightedFrom(
                    data,
                    random,
                    MaleNamesPath),
                RandomWeightedFrom(
                    data,
                    random,
                    SurnamesPath),
                RelationshipPersonalityRules.ChoosePartnerAge(
                    daughter,
                    Sex.Male,
                    random));

        husband.BirthDate =
            RandomDateInYear(
                gameState.Year
                - husband.Age,
                random,
                calendar);

        family.InitializePerson(
            husband,
            Sex.Male,
            generation:
                null);

        husband.Tags.Add(
            "state.alive");

        husband.Tags.Add(
            "age.adult");

        husband.Tags.Add(
            "relationship.single");

        husband.Tags.Add(
            "sexuality.heterosexual");

        stats.EnsureStats(
            husband);

        var exceptionalMatch =
            RelationshipPersonalityRules.ApplyExceptionalPartnerStats(
                daughter,
                husband,
                stats,
                random);

        health.EnsureHealth(
            husband);

        education.SetEducationLevel(
            husband,
            0);

        var daughterEventName =
            family.GetDisplayName(
                daughter);

        var husbandEventName =
            family.GetDisplayName(
                husband);

        daughter.MaidenName ??=
            daughter.Surname;

        family.SetSpouses(
            daughter,
            husband,
            gameState.Year);

        daughter.Surname =
            husband.Surname;

        events.Publish(
            new GameEvent
            {
                Type =
                    "relationship.married",

                Year =
                    gameState.Year,

                SubjectId =
                    daughter.Id,

                RelatedPersonIds =
                    [
                        husband.Id,
                        father.Id
                    ],

                Data =
                    new Dictionary<string, string>
                    {
                        ["spouseId"] =
                            husband.Id.ToString(),

                        ["arrangedByFatherId"] =
                            father.Id.ToString(),

                        ["chance"] =
                            chance.ToString(
                                "0.00"),

                        ["text"] =
                            $"{family.GetDisplayName(father)} " +
                            $"found a husband for {daughterEventName}. " +
                            $"She married {husbandEventName}."
                    }
            });

        RelationshipPersonalityRules.ApplyExceptionalPartnerCareer(
            exceptionalMatch,
            husband,
            career,
            random);
    }

    private static string RandomWeightedFrom(
        IGameDataService data,
        IGameRandom random,
        string relativePath)
    {
        var entries =
            data.GetWeightedStringList(
                relativePath);

        var totalWeight =
            entries.Sum(
                entry =>
                    (double)entry.Weight);

        var roll =
            random.NextDouble()
            * totalWeight;

        foreach (var entry in entries)
        {
            if (roll < entry.Weight)
                return entry.Value;

            roll -=
                entry.Weight;
        }

        return entries[^1].Value;
    }

    private static GameDate RandomDateInYear(
        int year,
        IGameRandom random,
        IGameCalendar calendar)
    {
        var month =
            random.NextInt(
                1,
                12);

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

    private static GameActionDefinition
        CreateRepairMarriageAction(
            IFamilyService family,
            IMarriageSatisfactionService satisfaction,
            IGameEventBus events)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.repair_marriage",

            Label =
                "Repair Marriage",

            Description =
                "Spend the year working on the marriage. " +
                "This raises Marriage Satisfaction by 20 points.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

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
                        || family.GetSex(
                            actor) != Sex.Male)
                    {
                        return false;
                    }

                    var wife =
                        family.GetSpouse(
                            actor);

                    if (wife is null
                        || !wife.Tags.Has(
                            "state.alive")
                        || (target.Id != actor.Id
                            && target.Id != wife.Id))
                    {
                        return false;
                    }

                    var current =
                        satisfaction.GetSatisfaction(
                            actor);

                    return current is not null
                        && current.Value < 100;
                },

            Execute =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var wife =
                        family.GetSpouse(
                            actor);

                    if (wife is null
                        || !wife.Tags.Has(
                            "state.alive"))
                    {
                        return new GameActionResult(
                            false,
                            "The marriage no longer exists.");
                    }

                    satisfaction.ChangeSatisfaction(
                        actor,
                        20);

                    var updated =
                        satisfaction.GetSatisfaction(
                            actor);

                    events.Publish(
                        new GameEvent
                        {
                            Type =
                                "relationship.repair_marriage",

                            Year =
                                actionContext.GameState.Year,

                            SubjectId =
                                actor.Id,

                            RelatedPersonIds =
                                [wife.Id],

                            Data =
                                new Dictionary<string, string>
                                {
                                    ["satisfaction"] =
                                        updated?.Value
                                            .ToString(
                                                "0")
                                        ?? string.Empty,

                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} " +
                                        $"and {family.GetDisplayName(wife)} " +
                                        "worked on their marriage."
                                }
                        });

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static GameActionDefinition
        CreateDivorceAction(
            IFamilyService family,
            RelationshipBreakupService breakups)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.divorce_spouse",

            Label =
                "Divorce the Spouse",

            Description =
                "End the current marriage. Household wealth is halved " +
                "and the health of the family is harmed.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var spouse =
                        family.GetSpouse(
                            actor);

                    return actor.Tags.Has(
                            "state.alive")
                        && actor.Tags.Has(
                            "control.playable")
                        && spouse is not null
                        && spouse.Tags.Has(
                            "state.alive")
                        && (actionContext.Target.Id == actor.Id
                            || actionContext.Target.Id == spouse.Id);
                },

            Execute =
                actionContext =>
                    breakups.PlayerDivorce(
                        actionContext.GameState,
                        actionContext.Actor)
        };
    }
}
