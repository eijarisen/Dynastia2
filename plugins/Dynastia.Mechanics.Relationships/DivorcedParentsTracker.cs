using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal sealed class DivorcedParentsTracker
{
    public const string Tag =
        "state.parents_divorced";

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    public DivorcedParentsTracker(
        IGameState gameState,
        IFamilyService family,
        IGameEventBus events)
    {
        _gameState =
            gameState;

        _family =
            family;

        events.EventPublished +=
            OnEventPublished;
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (!IsDivorceEvent(
            gameEvent.Type))
        {
            return;
        }

        var first =
            FindPerson(
                gameEvent.SubjectId);

        var second =
            gameEvent.RelatedPersonIds.Count > 0
                ? FindPerson(
                    gameEvent.RelatedPersonIds[0])
                : null;

        if (first is null
            || second is null)
        {
            return;
        }

        foreach (var child in
            _family.GetChildren(
                first))
        {
            var sharedBiologicalChild =
                DivorceCustodyRules.IsSharedBiologicalChild(
                    _family.GetFather(child)?.Id,
                    _family.GetMother(child)?.Id,
                    first.Id,
                    second.Id);

            if (!sharedBiologicalChild
                || child.Age >= 18
                || child.Tags.Has(
                    "state.dead"))
            {
                continue;
            }

            child.Tags.Add(
                Tag);
        }
    }

    private static bool IsDivorceEvent(
        string type)
    {
        return type.Equals(
                "relationship.divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.low_satisfaction_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.prison_divorce",
                StringComparison.OrdinalIgnoreCase);
    }

    private IPerson? FindPerson(
        Guid? id)
    {
        if (id is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == id.Value);
    }
}
