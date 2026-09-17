using Dynastia.Contracts;

namespace Dynastia.Mechanics.Childhood;

public sealed class ChildhoodPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>() ?? throw new InvalidOperationException("Game state is unavailable.");
        var family = context.GetService<IFamilyService>() ?? throw new InvalidOperationException("Family service is unavailable.");
        var health = context.GetService<IHealthService>() ?? throw new InvalidOperationException("Health service is unavailable.");
        var economy = context.GetService<IEconomyService>() ?? throw new InvalidOperationException("Economy service is unavailable.");
        var personality = context.GetService<IPersonalityService>() ?? throw new InvalidOperationException("Personality service is unavailable.");
        var random = context.GetService<IGameRandom>() ?? throw new InvalidOperationException("Random service is unavailable.");
        var events = context.GetService<IGameEventBus>() ?? throw new InvalidOperationException("Event bus is unavailable.");
        var actions = context.GetService<IActionRegistry>() ?? throw new InvalidOperationException("Action registry is unavailable.");
        var systems = context.GetService<IYearSystemRegistry>() ?? throw new InvalidOperationException("Year registry is unavailable.");

        var happiness = new StandardChildHappinessService();
        context.AddService<IChildHappinessService>(happiness);

        context.GetService<IStateReconciliationLifecycle>()?
            .Register(
                "childhood.happiness",
                Enum.GetValues<ReconciliationLifecycleStage>(),
                _ =>
                {
                    foreach (var person in gameState.People)
                        happiness.EnsureHappiness(person);
                },
                order: 75);

        events.EventPublished += (_, e) => ApplyEvent(e, gameState, family, happiness, random);

        actions.Register(CreateRaiseChildAction(family, happiness, personality, random, events));
        systems.Register(new ChildHappinessYearSystem(happiness, health, economy, personality, random));

        context.Log("Child happiness mechanics registered.");
    }

    private static GameActionDefinition CreateRaiseChildAction(
        IFamilyService family,
        IChildHappinessService happiness,
        IPersonalityService personality,
        IGameRandom random,
        IGameEventBus events)
    {
        return new GameActionDefinition
        {
            Id = "childhood.raise_child",
            Label = "Raise Child",
            Description = "Spend the year giving the selected child extra guidance and attention. Improves Happiness and may gently improve Morals.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            IsAvailable = c => c.ActorHasControl
                && c.Target.Tags.Has("state.alive")
                && c.Target.Age < 18
                && family.GetChildren(c.Actor).Any(x => x.Id == c.Target.Id),
            Execute = c =>
            {
                if (!c.Target.Tags.Has("state.alive") || c.Target.Age >= 18
                    || !family.GetChildren(c.Actor).Any(x => x.Id == c.Target.Id))
                    return new GameActionResult(false);

                happiness.ChangeHappiness(c.Target, 1);

                if (c.Target.Age >= 5)
                {
                    var chance = PersonalityInfluence.AdjustProbability(
                        0.06,
                        c.Target,
                        melancholic: 0.15,
                        phlegmatic: -0.25,
                        sanguine: 0.05,
                        choleric: 0.15);
                    if (random.NextDouble() < chance)
                        personality.ShiftMorals(c.Target, 1);
                }

                events.Publish(new GameEvent
                {
                    Type = "childhood.raised",
                    Year = c.GameState.Year,
                    SubjectId = c.Target.Id,
                    RelatedPersonIds = [c.Actor.Id],
                    Data = new Dictionary<string, string>
                    {
                        ["text"] = $"{family.GetDisplayName(c.Actor)} spent extra time raising {family.GetDisplayName(c.Target)}, improving the child's happiness."
                    }
                });
                return new GameActionResult(true);
            }
        };
    }

    private static void ApplyEvent(
        GameEvent e,
        IGameState gameState,
        IFamilyService family,
        IChildHappinessService happiness,
        IGameRandom random)
    {
        if (e.Type.Equals("career.work_harder", StringComparison.OrdinalIgnoreCase)
            || e.Type.Equals("wellbeing.recover", StringComparison.OrdinalIgnoreCase)
            || e.Type.Equals("career.ask_recover_success", StringComparison.OrdinalIgnoreCase))
        {
            var isRequestedRecover = e.Type.Equals(
                "career.ask_recover_success",
                StringComparison.OrdinalIgnoreCase);

            var parent = isRequestedRecover
                ? e.RelatedPersonIds
                    .Select(id => Find(gameState, id))
                    .FirstOrDefault(person => person is not null)
                : Find(gameState, e.SubjectId);

            if (parent is null)
                return;

            var isRecover = e.Type.Equals(
                "wellbeing.recover",
                StringComparison.OrdinalIgnoreCase)
                || isRequestedRecover;

            foreach (var child in family.GetChildren(parent)
                .Where(child => child.Age < 18 && child.Tags.Has("state.alive")))
            {
                var chance = isRecover
                    ? PersonalityInfluence.AdjustProbability(
                        0.25,
                        child,
                        melancholic: 0.10,
                        phlegmatic: -0.10,
                        sanguine: 0.20,
                        choleric: 0.10)
                    : PersonalityInfluence.AdjustProbability(
                        0.25,
                        child,
                        melancholic: 0.20,
                        phlegmatic: -0.20,
                        sanguine: -0.05,
                        choleric: 0.20);

                if (random.NextDouble() < chance)
                {
                    happiness.ChangeHappiness(
                        child,
                        isRecover ? 1 : -1);
                }
            }

            return;
        }

        if (e.Type.Equals("education.help_learning_success", StringComparison.OrdinalIgnoreCase)
            || e.Type.Equals("education.help_learning_failure", StringComparison.OrdinalIgnoreCase))
        {
            var child = Find(gameState, e.SubjectId);
            if (child is not null && child.Age < 18)
                happiness.ChangeHappiness(child, 1);
            return;
        }

        if (e.Type.Equals("wellbeing.heal", StringComparison.OrdinalIgnoreCase))
        {
            var target = e.RelatedPersonIds.Count > 0 ? Find(gameState, e.RelatedPersonIds[0]) : null;
            if (target is not null && target.Age < 18)
                happiness.ChangeHappiness(target, 1);
            return;
        }

        if (!e.Type.Contains("divorce", StringComparison.OrdinalIgnoreCase)
            && !e.Type.Equals("relationship.affair", StringComparison.OrdinalIgnoreCase))
            return;

        var first = Find(gameState, e.SubjectId);
        var second = e.RelatedPersonIds.Select(id => Find(gameState, id)).FirstOrDefault(p => p is not null);
        if (first is null || second is null)
            return;

        var secondId = second.Id;
        foreach (var child in family.GetChildren(first).Where(c => c.Age < 18 &&
                     (family.GetFather(c)?.Id == secondId || family.GetMother(c)?.Id == secondId)))
        {
            happiness.ChangeHappiness(child, -1);
        }
    }

    private static IPerson? Find(IGameState state, Guid? id) =>
        id is Guid value ? state.People.FirstOrDefault(p => p.Id == value) : null;
}
