using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class CareerPlugin
{
    private static bool CanActorSupport(
        IPerson actor,
        bool actorHasControl)
    {
        return actor.Tags.Has("state.alive")
            && actorHasControl
            && !actor.Tags.Has("state.imprisoned");
    }

    private static bool IsResidentAdultChild(
        IPerson actor,
        IPerson target,
        IFamilyService family,
        IEconomyService economy)
    {
        if (target.Id == actor.Id
            || !target.Tags.Has("state.alive")
            || target.Age < 18
            || !family.GetChildren(actor)
                .Any(child => child.Id == target.Id))
        {
            return false;
        }

        var actorHouseholdId = economy.GetHouseholdId(actor);
        return actorHouseholdId is not null
            && economy.GetHouseholdId(target) == actorHouseholdId;
    }

    private static bool IsRecoverOrQuitTarget(
        IPerson actor,
        IPerson target,
        IFamilyService family,
        IEconomyService economy)
    {
        if (family.GetSpouse(actor)?.Id == target.Id)
            return true;

        return IsResidentAdultChild(
            actor,
            target,
            family,
            economy);
    }

    private static IPerson? FindFirstRelatedPerson(
        IGameState gameState,
        GameEvent gameEvent)
    {
        if (gameEvent.RelatedPersonIds.Count == 0)
            return null;

        var id = gameEvent.RelatedPersonIds[0];

        return gameState.People.FirstOrDefault(
            person => person.Id == id);
    }
}
