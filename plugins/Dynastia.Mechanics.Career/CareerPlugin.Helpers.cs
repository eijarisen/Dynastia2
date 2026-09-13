using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class CareerPlugin
{
    private static bool CanActorSupport(
        IPerson actor)
    {
        return actor.Tags.Has("state.alive")
            && actor.Tags.Has("control.playable")
            && !actor.Tags.Has("state.imprisoned");
    }

    private static bool IsRecoverOrQuitTarget(
        IPerson actor,
        IPerson target,
        ICareerService career,
        IFamilyService family)
    {
        if (family.GetSpouse(actor)?.Id == target.Id)
            return true;

        return family.GetChildren(actor)
            .Any(child => child.Id == target.Id)
            && family.GetSex(target) == Sex.Female
            && target.Age >= 18;
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
