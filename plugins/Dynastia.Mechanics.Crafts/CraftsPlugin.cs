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
        var data = Require<IGameDataService>(context, "Game data service");
        var random = Require<IGameRandom>(context, "Game random service");
        var events = Require<IGameEventBus>(context, "Game event bus");
        var actions = Require<IActionRegistry>(context, "Action registry");
        var systems = Require<IYearSystemRegistry>(context, "Year-system registry");
        var income = Require<IIncomeProviderRegistry>(context, "Income provider registry");

        var catalog = CraftCatalog.Load(data);
        ValidateCareerReferences(catalog, career);

        var service = new StandardCraftService(
            gameState,
            family,
            economy,
            career,
            random,
            events,
            catalog);

        context.AddService<ICraftService>(service);
        income.Register(service);

        RegisterActions(actions, service, family, economy, career, stats, random, events, catalog);
        RegisterGeneratedAdultInitialization(gameState, service, career, events);
        RegisterRelocationCleanup(gameState, service, events);

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
                Label = $"Start {definition.Name} Occupation",
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
                    && definition.StartYear <= context.GameState.Year
                    && crafts.KnowsCraft(context.Actor, definition.Id)
                    && !crafts.IsSelfEmployed(context.Actor)
                    && !career.GetCareer(context.Actor).IsRetired,
                Execute = context =>
                    new GameActionResult(crafts.StartOccupation(context.Actor, definition.Id))
            });

            actions.Register(new GameActionDefinition
            {
                Id = $"craft.teach.{definition.Id}",
                Label = $"Teach Craft: {definition.Name}",
                Description =
                    $"Teach {definition.Name} to a child aged 10-17. Success depends on the child's Intellect.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,
                IsAvailable = context =>
                {
                    if (!context.Actor.Tags.Has("state.alive")
                        || !context.Actor.Tags.Has("control.playable")
                        || !context.Target.Tags.Has("state.alive")
                        || context.Target.Age is < 10 or > 17
                        || definition.StartYear > context.GameState.Year
                        || crafts.GetKnownCrafts(context.Target).Count >= CraftRules.MaximumCrafts
                        || crafts.KnowsCraft(context.Target, definition.Id))
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

                    var intellect = stats.GetStats(context.Target)
                        .First(stat => stat.Id.Equals("intellect", StringComparison.OrdinalIgnoreCase))
                        .Value;
                    var chance = CraftRules.GetTeachingSuccessChance(intellect);
                    var success = random.NextDouble() < chance
                        && crafts.LearnCraft(context.Target, definition.Id);

                    events.Publish(new GameEvent
                    {
                        Type = success ? "craft.learned" : "craft.teaching_failed",
                        Year = context.GameState.Year,
                        SubjectId = context.Target.Id,
                        RelatedPersonIds = [teacher.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["craftId"] = definition.Id,
                            ["craftName"] = definition.Name,
                            ["teacherId"] = teacher.Id.ToString(),
                            ["learningMode"] = "taught",
                            ["chance"] = chance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                            ["suppressChronicle"] = success ? "false" : "true",
                            ["text"] = success
                                ? $"At age {context.Target.Age}, {family.GetDisplayName(context.Target)} learned {definition.Name} from {family.GetDisplayName(teacher)}."
                                : $"{family.GetDisplayName(teacher)} tried to teach {family.GetDisplayName(context.Target)} {definition.Name}, but the lesson did not take."
                        }
                    });

                    return new GameActionResult(true);
                }
            });
        }
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
        ICraftService crafts,
        ICareerService career,
        IGameEventBus events)
    {
        events.EventPublished += (_, gameEvent) =>
        {
            if (gameEvent.Type.Equals("game.started", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var person in gameState.People.Where(person => person.Age >= 18))
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

    private static void RegisterRelocationCleanup(
        IGameState gameState,
        ICraftService crafts,
        IGameEventBus events)
    {
        events.EventPublished += (_, gameEvent) =>
        {
            if (!gameEvent.Type.Equals(
                    "household.moved",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var ids = new HashSet<Guid>(gameEvent.RelatedPersonIds);
            if (gameEvent.SubjectId is Guid subjectId)
                ids.Add(subjectId);

            foreach (var person in gameState.People.Where(person => ids.Contains(person.Id)))
            {
                if (crafts.IsSelfEmployed(person))
                    crafts.EndOccupation(person, "relocation");
            }
        };
    }

    private static void InitializeGeneratedAdult(
        IPerson person,
        int year,
        ICraftService crafts,
        ICareerService career)
    {
        if (crafts.GetKnownCrafts(person).Count > 0)
            return;

        var formal = career.GetCareer(person);
        crafts.SetCrafts(
            person,
            crafts.GenerateCandidateCraftIds(
                person.Id.ToString("N"),
                formal.JobLevel > 0 ? formal.CareerId : null,
                year));
    }

    private static void ValidateCareerReferences(
        CraftCatalog catalog,
        ICareerService career)
    {
        foreach (var craft in catalog.All)
        {
            if (career.GetLevelOneSalary(craft.PrimaryCareerId) <= 0m)
            {
                throw new InvalidDataException(
                    $"Craft '{craft.Id}' references unknown primary career '{craft.PrimaryCareerId}'.");
            }

            foreach (var relatedCareerId in craft.RelatedCareerIds)
            {
                if (career.GetLevelOneSalary(relatedCareerId) <= 0m)
                {
                    throw new InvalidDataException(
                        $"Craft '{craft.Id}' references unknown related career '{relatedCareerId}'.");
                }
            }
        }
    }

    private static T Require<T>(IGamePluginContext context, string name)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{name} is unavailable.");
}
