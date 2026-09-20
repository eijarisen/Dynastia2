using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class HouseholdsPlugin
{
    private const string AdoptPolishSurnameActionId =
        "family.adopt_polish_surname";

    private const string PolishSurnameAdoptedTag =
        "dynasty.polish_surname_adopted";

    private static void RegisterAssimilationActions(
        IActionRegistry actions,
        IGameState gameState,
        IFamilyService family,
        INationalityService nationalities,
        IHistoricalNameService historicalNames,
        IGameRandom random,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = AdoptPolishSurnameActionId,
                Label = "Adopt a Polish Surname",
                Description =
                    "Adopt a new Polish surname for the dynasty. This is optional; leave the action unused to retain the original family name.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = context =>
                    context.ActorHasControl
                    && context.Actor.Id == context.Target.Id
                    && family.IsMaleLineage(context.Actor)
                    && context.Actor.Tags.Has(AssimilationYearSystem.PolishNamingTag)
                    && !context.Actor.Tags.Has(PolishSurnameAdoptedTag)
                    && nationalities.GetNationality(context.Actor).Equals(
                        "polish",
                        StringComparison.OrdinalIgnoreCase),
                Execute = context =>
                {
                    if (!context.ActorHasControl
                        || context.Actor.Id != context.Target.Id
                        || !family.IsMaleLineage(context.Actor)
                        || !context.Actor.Tags.Has(AssimilationYearSystem.PolishNamingTag)
                        || context.Actor.Tags.Has(PolishSurnameAdoptedTag)
                        || !nationalities.GetNationality(context.Actor).Equals(
                            "polish",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return new GameActionResult(false);
                    }

                    var oldSurname = gameState.DynastySurname;
                    var newSurname = oldSurname;
                    for (var attempt = 0; attempt < 8
                         && newSurname.Equals(oldSurname, StringComparison.OrdinalIgnoreCase);
                         attempt++)
                    {
                        newSurname = historicalNames.GetRandomSurname(
                            Sex.Male,
                            "polish",
                            random);
                    }

                    if (newSurname.Equals(oldSurname, StringComparison.OrdinalIgnoreCase))
                        return new GameActionResult(false, "No different Polish surname could be generated.");

                    gameState.DynastySurname = newSurname;

                    foreach (var person in gameState.People)
                    {
                        var currentSpouse = family.GetSpouse(person);
                        var belongsToDynasty = family.IsBloodline(person)
                            || (currentSpouse is not null && family.IsBloodline(currentSpouse));

                        if (!belongsToDynasty
                            || !person.Surname.Equals(oldSurname, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        person.Surname = newSurname;
                        if (person.MaidenName is not null
                            && person.MaidenName.Equals(oldSurname, StringComparison.OrdinalIgnoreCase))
                        {
                            person.MaidenName = newSurname;
                        }
                    }

                    foreach (var person in gameState.People.Where(family.IsBloodline))
                    {
                        person.Tags.Add(AssimilationYearSystem.PolishNamingTag);
                        person.Tags.Add(PolishSurnameAdoptedTag);
                    }

                    events.Publish(
                        new GameEvent
                        {
                            Type = "family.polish_surname_adopted",
                            Year = context.GameState.Year,
                            SubjectId = context.Actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["oldSurname"] = oldSurname,
                                ["newSurname"] = newSurname,
                                ["text"] =
                                    $"The {oldSurname} dynasty adopted the Polish surname {newSurname}."
                            }
                        });

                    return new GameActionResult(true);
                }
            });
    }
}
