using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousHouseholdDecisionService :
    IAutonomousHouseholdDecisionService
{
    private readonly IGameState _gameState;
    private readonly IHouseholdService _households;
    private readonly IActionRegistry _actions;
    private readonly IAutonomousHouseholdStrategy _strategy;

    public AutonomousHouseholdDecisionService(
        IGameState gameState,
        IHouseholdService households,
        IActionRegistry actions,
        IAutonomousHouseholdStrategy strategy)
    {
        _gameState = gameState;
        _households = households;
        _actions = actions;
        _strategy = strategy;
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
        var households =
            _households
                .GetActiveHouseholds()
                .Where(
                    household =>
                        includeLineage
                        || household.Class
                            == HouseholdClass.Bloodline)
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
            var scored = _strategy
                .GetAvailableActions(snapshot)
                .Select(action => _strategy.ScoreAction(action, snapshot))
                .OfType<AutonomousActionCandidate>()
                .ToList();

            var selected = _strategy.ChooseAction(scored);
            if (selected is null)
                continue;

            if (_strategy.QueueAction(selected, snapshot))
                queuedCount++;
        }

        return queuedCount;
    }

    private IPerson? FindPerson(Guid id) =>
        _gameState.People.FirstOrDefault(person => person.Id == id);
}
