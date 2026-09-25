using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousHouseholdDecisionService :
    IAutonomousHouseholdDecisionService
{
    private readonly IGameState _gameState;
    private readonly IHouseholdService _households;
    private readonly IActionRegistry _actions;
    private readonly IAutonomousHouseholdStrategy _strategy;
    private readonly AutonomousPlanStateCoordinator? _plans;

    public AutonomousHouseholdDecisionService(
        IGameState gameState,
        IHouseholdService households,
        IActionRegistry actions,
        IAutonomousHouseholdStrategy strategy,
        AutonomousPlanStateCoordinator? plans = null)
    {
        _gameState = gameState;
        _households = households;
        _actions = actions;
        _strategy = strategy;
        _plans = plans;
    }

    public int QueueActionsForAllHouseholds()
    {
        return QueueActions(
            includeLineage: true,
            replaceExisting: true);
    }

    public int QueueActionsForAutonomousHouseholds()
    {
        return QueueActions(
            includeLineage: false,
            replaceExisting: false);
    }

    private int QueueActions(
        bool includeLineage,
        bool replaceExisting)
    {
        var allHouseholds = _households.GetActiveHouseholds().ToList();
        if (!includeLineage && _plans is not null)
        {
            foreach (var household in allHouseholds.Where(household =>
                household.Class != HouseholdClass.Bloodline))
            {
                _plans.ReleasePlansForHousehold(household);
            }
        }

        var households = allHouseholds
            .Where(household => includeLineage
                || household.Class == HouseholdClass.Bloodline)
            .ToList();

        if (replaceExisting)
        {
            foreach (var household in households)
            {
                var head = FindPerson(household.HeadId);
                if (head is not null)
                    _actions.CancelQueuedActions(head);
            }
        }

        var queuedCount = 0;

        foreach (var household in households)
        {
            var head = FindPerson(household.HeadId);
            if (head is null || !head.Tags.Has("state.alive"))
                continue;

            // Normal autonomous simulation never overwrites an already
            // queued action. Debug simulation clears all queues above and
            // then runs this exact same strategy for every household.
            if (_actions.GetQueuedActions(head).Count > 0)
                continue;

            var snapshot = _strategy.BuildSnapshot(household);
            if (_plans is not null)
                snapshot = _plans.PrepareSnapshot(snapshot);

            var scored = _strategy
                .GetAvailableActions(snapshot)
                .Select(action => _strategy.ScoreAction(action, snapshot))
                .OfType<AutonomousActionCandidate>()
                .ToList();
            if (_plans is not null)
                scored = _plans.AttachPlanMetadata(snapshot, scored).ToList();

            var assessed = scored.ToList();
            var rejected = new List<AutonomousActionCandidate>();

            // A stale offer or a guard can reject the first choice. Try the
            // remaining valid plans so the household does not silently lose a year.
            while (scored.Count > 0)
            {
                var selected = _strategy.ChooseAction(scored);
                if (selected is null)
                    break;
                if (_strategy.QueueAction(selected, snapshot))
                {
                    if (_plans is not null)
                    {
                        var fairnessCandidates = assessed
                            .Where(candidate => !rejected.Contains(candidate))
                            .ToList();
                        _plans.RecordQueuedSelection(snapshot, selected, fairnessCandidates);
                        _plans.RecordUnconsumedRejections(snapshot, rejected);
                    }
                    queuedCount++;
                    break;
                }
                rejected.Add(selected);
                scored.Remove(selected);
            }

            if (scored.Count == 0 && _plans is not null)
                _plans.RecordUnconsumedRejections(snapshot, rejected);
        }

        return queuedCount;
    }

    private IPerson? FindPerson(Guid id) =>
        _gameState.People.FirstOrDefault(person => person.Id == id);
}
