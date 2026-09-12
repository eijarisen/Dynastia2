using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class PrisonStatusYearSystem :
    IYearSystem
{
    private readonly StandardJusticeService _justice;
    private readonly IFamilyService _family;
    private readonly IGameEventBus _events;

    public PrisonStatusYearSystem(
        StandardJusticeService justice,
        IFamilyService family,
        IGameEventBus events)
    {
        _justice = justice;
        _family = family;
        _events = events;
    }

    public string Id =>
        "justice.prison_status";

    public YearPhase Phase =>
        YearPhase.Status;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["career.retirement"];

    public void Execute(
        IGameState gameState)
    {
        foreach (var person in
            gameState.People)
        {
            if (person.Tags.Has(
                    "state.dead")
                || SimulationState.IsInactive(person)
                || !_justice.IsImprisoned(
                    person))
            {
                continue;
            }

            if (!_justice.AdvanceSentence(
                person))
            {
                continue;
            }

            _events.Publish(
                new GameEvent
                {
                    Type =
                        "justice.released",

                    Year =
                        gameState.Year,

                    SubjectId =
                        person.Id,

                    Data =
                        new Dictionary<string, string>
                        {
                            ["text"] =
                                $"{_family.GetDisplayName(person)} " +
                                "has been released from prison."
                        }
                });
        }
    }
}
