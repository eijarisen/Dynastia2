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

        context.GetService<IStateReconciliationLifecycle>()?
            .Register(
                "crafts.components",
                Enum.GetValues<ReconciliationLifecycleStage>(),
                _ => service.ReconcileAll(),
                order: 65);

        RegisterActions(actions, service, gameState, family, economy, career, stats, random, events, catalog);
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
        IGameState gameState,
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
                    $"Leave any formal career and earn a living through {definition.Name}. Income varies sharply from year to year.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,
                IsAvailable = context =>
                    CanDirectCraftOccupation(
                        context,
                        family,
                        economy)
                    && context.Target.Tags.Has("state.alive")
                    && context.Target.Age >= 18
                    && !context.Target.Tags.Has("state.imprisoned")
                    && crafts.KnowsCraft(context.Target, definition.Id)
                    && !string.Equals(
                        crafts.GetActiveCraft(context.Target)?.Id,
                        definition.Id,
                        StringComparison.OrdinalIgnoreCase),
                Execute = context =>
                    new GameActionResult(crafts.StartOccupation(context.Target, definition.Id))
            });

            actions.Register(new GameActionDefinition
            {
                Id = $"craft.teach.{definition.Id}",
                Label = "Teach Craft",
                Description =
                    $"Teach {definition.Name} to a young relative in the household who is old enough to learn it. Success depends on the craft's relevant aptitude.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.LifeEvents,
                IsAvailable = context =>
                {
                    if (!context.Actor.Tags.Has("state.alive")
                        || !context.ActorHasControl
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

                    return FindTeacher(
                        gameState,
                        context.Target,
                        definition.Id,
                        crafts,
                        family,
                        economy) is not null;
                },
                Execute = context =>
                {
                    var teacher = FindTeacher(
                        gameState,
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
                            ["familyNews"] = success ? "true" : "false",
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
            Label = "Quit Profession",
            Description =
                "End Craft self-employment. The Craft and all Mastery progress are preserved.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            IsAvailable = context =>
                CanDirectCraftOccupation(
                    context,
                    family,
                    economy)
                && context.Target.Tags.Has("state.alive")
                && !context.Target.Tags.Has("state.imprisoned")
                && crafts.IsSelfEmployed(context.Target),
            Execute = context =>
                new GameActionResult(crafts.EndOccupation(context.Target, "stopped"))
        });
    }

    private static bool CanDirectCraftOccupation(
        GameActionContext context,
        IFamilyService family,
        IEconomyService economy)
    {
        if (!context.ActorHasControl
            || !context.Actor.Tags.Has("state.alive"))
        {
            return false;
        }

        if (context.Actor.Id == context.Target.Id)
            return true;

        return HouseholdKinshipRules.IsSupportedResidentRelative(
            context.Actor,
            context.Target,
            family,
            economy,
            requireAdult: true);
    }

    private static IPerson? FindTeacher(
        IGameState gameState,
        IPerson child,
        string craftId,
        ICraftService crafts,
        IFamilyService family,
        IEconomyService economy)
    {
        var householdId = economy.GetHouseholdId(child);
        if (householdId is null)
            return null;

        return gameState.People
            .Where(candidate =>
                candidate.Id != child.Id
                && candidate.Age >= 18
                && candidate.Tags.Has("state.alive")
                && !candidate.Tags.Has("state.imprisoned")
                && economy.GetHouseholdId(candidate) == householdId
                && HouseholdKinshipRules.IsSupportedRelative(
                    candidate,
                    child,
                    family)
                && crafts.KnowsCraft(candidate, craftId))
            .OrderByDescending(candidate =>
                crafts.GetProgress(candidate, craftId)?.MasteryLevel ?? 0)
            .ThenByDescending(candidate => candidate.Age)
            .ThenBy(candidate => candidate.Id)
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
                        && person.Age >= 18)
                    {
                        // Relationship events are published synchronously while
                        // generated adults are still being constructed. Reconcile
                        // Craft-owned state before any Craft read; the general
                        // AfterPersonCreated lifecycle pass runs only after the
                        // creating year system returns.
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
        crafts.ReconcilePerson(person);

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
                    throw CatalogValidation.Error(
                        "Crafts/craft_career_links.csv",
                        "a CareerId defined by the career catalog",
                        item: craft.Id,
                        field: "CareerId",
                        value: careerId);
                }
            }
        }
    }

    private static T Require<T>(IGamePluginContext context, string name)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{name} is unavailable.");
}
