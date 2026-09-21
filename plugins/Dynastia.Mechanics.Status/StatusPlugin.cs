using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Status;

public sealed class StatusPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = Required<IGameState>(context, "game state");
        var family = Required<IFamilyService>(context, "family service");
        var economy = Required<IEconomyService>(context, "economy service");
        var education = Required<IEducationService>(context, "education service");
        var career = Required<ICareerService>(context, "career service");
        var crafts = Required<ICraftService>(context, "craft service");
        var farming = Required<IFarmingService>(context, "farming service");
        var locations = Required<ILocationService>(context, "location service");
        var loans = Required<ILoanService>(context, "loan service");
        var heirlooms = Required<IHeirloomService>(context, "heirloom service");
        var data = Required<IGameDataService>(context, "game data service");
        var events = Required<IGameEventBus>(context, "game event bus");
        var reconciliation = Required<IStateReconciliationLifecycle>(context, "state reconciliation lifecycle");

        var rules = StatusRules.Load(data);
        var eventCatalog = StatusEventCatalog.Load(data);
        var status = new StandardStatusService(
            gameState,
            family,
            economy,
            education,
            career,
            crafts,
            farming,
            locations,
            loans,
            heirlooms,
            rules);

        context.AddService<IStatusService>(status);

        reconciliation.Register(
            "status.components_and_locality",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction,
                ReconciliationLifecycleStage.AfterQueuedAction,
                ReconciliationLifecycleStage.AfterPersonCreated
            ],
            _ => status.ReconcileAll(),
            order: 85);

        events.EventPublished += (_, gameEvent) =>
            HandleEvent(gameState, status, eventCatalog, gameEvent);

        context.Log("Status mechanics registered.");
    }

    private static void HandleEvent(
        IGameState gameState,
        IStatusService status,
        StatusEventCatalog catalog,
        GameEvent gameEvent)
    {
        if (gameEvent.Type.Equals("life.adult", StringComparison.OrdinalIgnoreCase))
        {
            var adult = Find(gameState, gameEvent.SubjectId);
            if (adult is not null)
                status.SeedAdultInheritance(adult);
            return;
        }

        if (gameEvent.Type.Equals("justice.crime", StringComparison.OrdinalIgnoreCase)
            && gameEvent.Data.TryGetValue("category", out var category)
            && catalog.TryGetCrime(category, out var crimeDelta))
        {
            Apply(gameState, status, gameEvent.SubjectId, crimeDelta, "public crime");
            return;
        }

        if (gameEvent.Type.Equals("historical.household_impact", StringComparison.OrdinalIgnoreCase)
            && gameEvent.Data.TryGetValue("eventId", out var historicalId)
            && catalog.TryGetHistorical(historicalId, out var historicalDelta))
        {
            var ids = (gameEvent.SubjectId is Guid subjectId
                    ? new[] { subjectId }
                    : Array.Empty<Guid>())
                .Concat(gameEvent.RelatedPersonIds)
                .Distinct();
            foreach (var id in ids)
            {
                var person = Find(gameState, id);
                if (person is not null && person.Age >= 18 && person.Tags.Has("state.alive"))
                    status.ApplyPersistentDelta(person, historicalDelta.Renown, historicalDelta.Reputation, $"historical:{historicalId}");
            }
            return;
        }

        if (catalog.TryGetEvent(gameEvent.Type, out var eventDelta))
        {
            if (gameEvent.Type.Equals("relationship.divorce", StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals("relationship.low_satisfaction_divorce", StringComparison.OrdinalIgnoreCase))
            {
                Apply(gameState, status, gameEvent.SubjectId, eventDelta, gameEvent.Type);
                foreach (var id in gameEvent.RelatedPersonIds)
                    Apply(gameState, status, id, eventDelta, gameEvent.Type);
            }
            else
            {
                // The current affair event models the outside partner abstractly;
                // RelatedPersonIds contains the betrayed spouse and must not be penalized.
                Apply(gameState, status, gameEvent.SubjectId, eventDelta, gameEvent.Type);
            }
        }

        var hasRenownDelta = TryReadDelta(
            gameEvent.Data,
            "statusRenownDelta",
            out var renown);
        var hasReputationDelta = TryReadDelta(
            gameEvent.Data,
            "statusReputationDelta",
            out var reputation);
        if (hasRenownDelta || hasReputationDelta)
        {
            var person = Find(gameState, gameEvent.SubjectId);
            if (person is not null)
                status.ApplyPersistentDelta(person, renown, reputation, gameEvent.Type);
        }
    }

    private static void Apply(
        IGameState gameState,
        IStatusService status,
        Guid? personId,
        (double Renown, double Reputation) delta,
        string reason)
    {
        var person = Find(gameState, personId);
        if (person is not null)
            status.ApplyPersistentDelta(person, delta.Renown, delta.Reputation, reason);
    }

    private static IPerson? Find(IGameState state, Guid? id) =>
        id.HasValue
            ? state.People.FirstOrDefault(person => person.Id == id.Value)
            : null;

    private static bool TryReadDelta(
        IReadOnlyDictionary<string, string> data,
        string key,
        out double value)
    {
        value = 0;
        return data.TryGetValue(key, out var text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static T Required<T>(IGamePluginContext context, string label)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{label} is unavailable.");
}
