using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class CareerPlugin
{
    private static void InitializeFromEvents(
        IGameState gameState,
        ICareerService career,
        IFamilyService family,
        IEducationService education,
        RetirementRuleCatalog retirementRules,
        IGameRandom random,
        IGameEventBus events)
    {
        events.EventPublished +=
            (_, gameEvent) =>
            {
                if (gameEvent.Type.Equals(
                    "game.started",
                    StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var person in gameState.People)
                    {
                        var initialJobLevel =
                            gameEvent.SubjectId is Guid founderId
                            && person.Id == founderId
                                ? 1
                                : InitialCareerProfileRules.ResolveJobLevel(
                                    person.Age,
                                    education.GetEducationLevel(person),
                                    random.NextDouble());

                        career.InitializeCareer(
                            person,
                            initialJobLevel,
                            random.NextInt(1, 5));
                    }

                    return;
                }

                if (gameEvent.Data.TryGetValue(
                        "preserveGeneratedProfile",
                        out var preserveGeneratedProfile)
                    && preserveGeneratedProfile.Equals(
                        "true",
                        StringComparison.OrdinalIgnoreCase)
                    && (gameEvent.Type.Equals(
                            "relationship.married",
                            StringComparison.OrdinalIgnoreCase)
                        || gameEvent.Type.Equals(
                            "relationship.partnered",
                            StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                if (gameEvent.Type.Equals(
                        "relationship.married",
                        StringComparison.OrdinalIgnoreCase)
                    || gameEvent.Type.Equals(
                        "relationship.partnered",
                        StringComparison.OrdinalIgnoreCase)
                    || gameEvent.Type.Equals(
                        "relationship.remarried",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var spouse =
                        FindFirstRelatedPerson(
                            gameState,
                            gameEvent);

                    if (spouse is not null)
                    {
                        career.InitializeCareer(
                            spouse,
                            InitialCareerProfileRules.ResolveJobLevel(
                                spouse.Age,
                                education.GetEducationLevel(spouse),
                                random.NextDouble()),
                            random.NextInt(1, 5));

                        var retirementAge =
                            retirementRules
                                .GetRule(
                                    gameState.Year)
                                .GetRetirementAge(
                                    family.GetSex(spouse));

                        if (spouse.Age >= retirementAge)
                        {
                            career.Retire(spouse);
                        }
                    }

                    return;
                }

                if (gameEvent.Type.Equals(
                        "life.birth",
                        StringComparison.OrdinalIgnoreCase)
                    && gameEvent.SubjectId is Guid childId)
                {
                    var child =
                        gameState.People.FirstOrDefault(
                            person => person.Id == childId);

                    if (child is not null)
                    {
                        career.InitializeCareer(
                            child,
                            0,
                            random.NextInt(1, 5));
                    }
                }
            };
    }

}
