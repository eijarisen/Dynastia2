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

    private static bool IsResidentSupportedMember(
        IPerson actor,
        IPerson target,
        IEconomyService economy) =>
        target.Id != actor.Id
        && HouseholdKinshipRules.IsResidentHouseholdMember(
            actor,
            target,
            economy,
            requireAdult: true);

    private static bool IsRecoverOrQuitTarget(
        IPerson actor,
        IPerson target,
        IEconomyService economy) =>
        IsResidentSupportedMember(
            actor,
            target,
            economy);

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
