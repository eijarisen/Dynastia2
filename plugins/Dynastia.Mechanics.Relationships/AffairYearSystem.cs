using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class AffairYearSystem :
    IYearSystem
{
    private const double AffairChance =
        0.005;

    private readonly IFamilyService _family;
    private readonly IGameRandom _random;
    private readonly RelationshipBreakupService _breakups;

    public AffairYearSystem(
        IFamilyService family,
        IGameRandom random,
        RelationshipBreakupService breakups)
    {
        _family = family;
        _random = random;
        _breakups = breakups;
    }

    public string Id =>
        "relationships.affairs";

    public YearPhase Phase =>
        YearPhase.LifeEvents;

    public IReadOnlyCollection<string> Before =>
        [
            "reproduction.births",
            "relationships.female_remarriage"
        ];

    public IReadOnlyCollection<string> After =>
        ["relationships.marriage"];

    public void Execute(
        IGameState gameState)
    {
        // Same family-array order as the source.
        var livingSnapshot =
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive"))
                .ToList();

        foreach (var person in
            livingSnapshot)
        {
            var spouse =
                _family.GetSpouse(
                    person);

            if (spouse is null)
                continue;

            // In Dynasty 4 the spouse local is captured before
            // automatic marriage; therefore a brand-new marriage
            // cannot immediately have an affair in that same pass.
            var currentMarriage =
                _family
                    .GetRelationshipHistory(
                        person)
                    .LastOrDefault(
                        relationship =>
                            relationship.SpouseId
                                == spouse.Id
                            && relationship.EndYear
                                is null);

            if (currentMarriage?.StartYear
                == gameState.Year)
            {
                continue;
            }

            var affairChance =
                PersonalityInfluence.AdjustProbability(
                    AffairChance,
                    person,
                    good: -0.20,
                    evil: 0.20);

            if (_random.NextDouble()
                >= affairChance)
            {
                continue;
            }

            _breakups.AffairDivorce(
                gameState,
                person,
                spouse);
        }
    }
}
