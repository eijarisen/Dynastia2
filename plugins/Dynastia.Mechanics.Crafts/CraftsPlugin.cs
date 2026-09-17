using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

public sealed class CraftsPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = Require<IGameState>(context, "Game state");
        var family = Require<IFamilyService>(context, "Family service");
        var economy = Require<IEconomyService>(context, "Economy service");
        var career = Require<ICareerService>(context, "Career service");
        var stats = Require<IStatsService>(context, "Stats service");
        var personality = Require<IPersonalityService>(context, "Personality service");
        var localOpportunities = Require<ILocalCareerOpportunityService>(context, "Local opportunity service");
        var contextWeights = Require<IContextWeightService>(context, "Context-weight service");
        var data = Require<IGameDataService>(context, "Game data service");
        var random = Require<IGameRandom>(context, "Game random service");
        var events = Require<IGameEventBus>(context, "Game event bus");
        var actions = Require<IActionRegistry>(context, "Action registry");
        var systems = Require<IYearSystemRegistry>(context, "Year-system registry");
        var income = Require<IIncomeProviderRegistry>(context, "Income provider registry");

        var catalog = CraftCatalog.Load(data);
        CraftVocationDataValidation.Validate(data);
        ValidateCareerReferences(catalog, career);

        var service = new StandardCraftService(
            gameState,
            family,
            economy,
            career,
            stats,
            personality,
            localOpportunities,
            random,
            events,
            contextWeights,
            catalog);

        context.AddService<ICraftService>(service);
        income.Register(service);

        RegisterActions(actions, service, family, economy, career, stats, random, events, catalog);
        RegisterGeneratedAdultInitialization(gameState, service, career, events);

        systems.Register(new CraftExperienceYearSystem(service));
        systems.Register(new PassiveCraftLearningYearSystem(
            service,
            family,
            economy,
            random,
            events));

        context.Log("Craft mechanics registered.");
    }

    private static void RegisterActions(
        IActionRegistry actions,
        StandardCraftService crafts,
        IFamilyService family,
        IEconomyService economy,
        ICareerService career,
        IStatsService stats,
        IGameRandom random,
        IGameEventBus events,
        CraftCatalog catalog)
    {
        foreach (var craft in catalog.All)
        {
            var definition = craft;

            actions.Register(new GameActionDefinition
            {
                Id = $"craft.start.{definition.Id}",
                Label = "Work in a Profession",
                Description =
                    $"Leave any formal career and earn a living through {definition.Name}. Income varies month to month.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,
                IsAvailable = context =>
                    context.Actor.Id == context.Target.Id
                    && context.Actor.Tags.Has("state.alive")
                    && context.Actor.Tags.Has("control.playable")
                    && context.Actor.Age >= 18
                    && !context.Actor.Tags.Has("state.imprisoned")
                    && crafts.KnowsCraft(context.Actor, definition.Id)
                    && !string.Equals(
                        crafts.GetActiveCraft(context.Actor)?.Id,
                        definition.Id,
                        StringComparison.OrdinalIgnoreCase),
                Execute = context =>
                    new GameActionResult(crafts.StartOccupation(context.Actor, definition.Id))
            });

            actions.Register(new GameActionDefinition
            {
                Id = $"craft.teach.{definition.Id}",
                Label = $"Teach Craft: {definition.Name}",
                Description =
                    $"Teach {definition.Name} to a child who is old enough to learn it. Success depends on the craft's relevant aptitude.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,
                IsAvailable = context =>
                {
                    if (!context.Actor.Tags.Has("state.alive")
                        || !context.Actor.Tags.Has("control.playable")
                        || !context.Target.Tags.Has("state.alive")
                        || context.Target.Age >= 18
                        || crafts.GetKnownCrafts(context.Target).Count >= CraftRules.MaximumCrafts
                        || crafts.KnowsCraft(context.Target, definition.Id)
                        || !crafts.CanLearnCraft(context.Target, definition.Id))
                    {
                        return false;
                    }

                    var actorHousehold = economy.GetHouseholdId(context.Actor);
                    if (actorHousehold is null
                        || economy.GetHouseholdId(context.Target) != actorHousehold)
                    {
                        return false;
                    }

                    return FindTeacher(context.Target, definition.Id, crafts, family, economy) is not null;
                },
                Execute = context =>
                {
                    var teacher = FindTeacher(
                        context.Target,
                        definition.Id,
                        crafts,
                        family,
                        economy);
                    if (teacher is null)
                        return new GameActionResult(false);

                    var targetStats = stats.GetStats(context.Target)
                        .Where(stat => stat.Id is "strength" or "intellect")
                        .ToDictionary(stat => stat.Id, stat => stat.Value, StringComparer.OrdinalIgnoreCase);
                    var chance = CraftRules.GetTeachingSuccessChance(definition, targetStats);
                    var success = random.NextDouble() < chance
                        && crafts.LearnCraft(context.Target, definition.Id);
                    var displayName = crafts.Catalog.First(craft =>
                        craft.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)).Name;

                    events.Publish(new GameEvent
                    {
                        Type = success ? "craft.learned" : "craft.teaching_failed",
                        Year = context.GameState.Year,
                        SubjectId = context.Target.Id,
                        RelatedPersonIds = [teacher.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["craftId"] = definition.Id,
                            ["craftName"] = displayName,
                            ["teacherId"] = teacher.Id.ToString(),
                            ["learningMode"] = "taught",
                            ["chance"] = chance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                            ["suppressChronicle"] = success ? "false" : "true",
                            ["text"] = success
                                ? $"At age {context.Target.Age}, {family.GetDisplayName(context.Target)} learned {displayName} from {family.GetDisplayName(teacher)}."
                                : $"{family.GetDisplayName(teacher)} tried to teach {family.GetDisplayName(context.Target)} {displayName}, but the lesson did not take."
                        }
                    });

                    return new GameActionResult(true);
                }
            });
        }

        actions.Register(new GameActionDefinition
        {
            Id = "craft.stop_occupation",
            Label = "Stop Working in a Profession",
            Description =
                "End Craft self-employment. The Craft and all Mastery progress are preserved.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            IsAvailable = context =>
                context.Actor.Id == context.Target.Id
                && context.Actor.Tags.Has("state.alive")
                && context.Actor.Tags.Has("control.playable")
                && !context.Actor.Tags.Has("state.imprisoned")
                && crafts.IsSelfEmployed(context.Actor),
            Execute = context =>
                new GameActionResult(crafts.EndOccupation(context.Actor, "stopped"))
        });
    }

    private static IPerson? FindTeacher(
        IPerson child,
        string craftId,
        ICraftService crafts,
        IFamilyService family,
        IEconomyService economy)
    {
        var householdId = economy.GetHouseholdId(child);
        if (householdId is null)
            return null;

        return new[] { family.GetFather(child), family.GetMother(child) }
            .Where(parent => parent is not null
                && parent.Tags.Has("state.alive")
                && economy.GetHouseholdId(parent) == householdId
                && crafts.KnowsCraft(parent, craftId))
            .Cast<IPerson>()
            .FirstOrDefault();
    }

    private static void RegisterGeneratedAdultInitialization(
        IGameState gameState,
        StandardCraftService crafts,
        ICareerService career,
        IGameEventBus events)
    {
        events.EventPublished += (_, gameEvent) =>
        {
            if (gameEvent.Type.Equals("game.started", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var person in gameState.People.Where(person => person.Age >= 18 && person.Tags.Has("state.alive")))
                    InitializeGeneratedAdult(person, gameState.Year, crafts, career);
                return;
            }

            if (gameEvent.Type.Equals("relationship.married", StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals("relationship.partnered", StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals("relationship.remarried", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var relatedId in gameEvent.RelatedPersonIds)
                {
                    var person = gameState.People.FirstOrDefault(candidate => candidate.Id == relatedId);
                    if (person is not null
                        && person.Age >= 18
                        && crafts.GetKnownCrafts(person).Count == 0)
                    {
                        InitializeGeneratedAdult(person, gameState.Year, crafts, career);
                    }
                }
            }
        };
    }

    private static void InitializeGeneratedAdult(
        IPerson person,
        int year,
        StandardCraftService crafts,
        ICareerService career)
    {
        if (crafts.GetKnownCrafts(person).Count > 0)
            return;

        var formal = career.GetCareer(person);
        crafts.SetCrafts(
            person,
            crafts.GenerateCraftIdsForPerson(
                person,
                formal.JobLevel > 0 ? formal.CareerId : null,
                year));
    }

    private static void ValidateCareerReferences(
        CraftCatalog catalog,
        ICareerService career)
    {
        foreach (var craft in catalog.All)
        {
            foreach (var careerId in craft.PrimaryCareerIds.Concat(craft.SecondaryCareerIds))
            {
                if (career.GetLevelOneSalary(careerId) <= 0m)
                {
                    throw new InvalidDataException(
                        $"Craft '{craft.Id}' references unknown career '{careerId}'.");
                }
            }
        }
    }

    private static T Require<T>(IGamePluginContext context, string name)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{name} is unavailable.");
}
