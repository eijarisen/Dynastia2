using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed partial class RelationshipsPlugin
{
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

        GeneratedFamilyBackgroundGenerator.Assign(
            husband,
            husband.Surname,
            family,
            data,
            random);

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
                "This raises Marriage Satisfaction by 20 points after this year's " +
                "marriage pressures are applied and before automatic divorce is decided.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.MarriageRepair,

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
