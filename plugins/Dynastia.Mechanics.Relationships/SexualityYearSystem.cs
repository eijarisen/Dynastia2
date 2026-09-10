using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class SexualityYearSystem : IYearSystem
{
    private const double HomosexualityChance = 0.005;

    private readonly IFamilyService _family;
    private readonly IGameRandom _random;

    public SexualityYearSystem(
        IFamilyService family,
        IGameRandom random)
    {
        _family = family;
        _random = random;
    }

    public string Id =>
        "relationships.sexuality";

    public YearPhase Phase =>
        YearPhase.Status;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["aging.increment_age"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead"))
                continue;

            if (person.Age != 18)
                continue;

            if (_family.GetSex(person) != Sex.Male)
                continue;

            if (!_family.IsMaleLineage(person))
                continue;

            person.Tags.Remove(
                "sexuality.heterosexual");

            person.Tags.Remove(
                "sexuality.homosexual");

            person.Tags.Add(
                _random.NextDouble() < HomosexualityChance
                    ? "sexuality.homosexual"
                    : "sexuality.heterosexual");
        }
    }
}
