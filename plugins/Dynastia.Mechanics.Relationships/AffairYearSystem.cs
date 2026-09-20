using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class AffairYearSystem :
    IYearSystem
{
    private const double AffairChance =
        0.005;

    private readonly IFamilyService _family;
    private readonly IGameRandom _random;
    private readonly IMarriageSatisfactionService _satisfaction;
    private readonly IGameEventBus _events;

    public AffairYearSystem(
        IFamilyService family,
        IGameRandom random,
        IMarriageSatisfactionService satisfaction,
        IGameEventBus events)
    {
        _family = family;
        _random = random;
        _satisfaction = satisfaction;
        _events = events;
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

            // Affairs are a major marriage shock, but they now feed the same
            // cumulative Satisfaction system as other marital pressures rather
            // than forcing a guaranteed immediate divorce. Repeated affairs or
            // an already-damaged marriage can still lead to divorce in the
            // normal low-satisfaction evaluation later in this turn.
            _satisfaction.ChangeSatisfactionExact(
                person,
                -MarriageBalanceRules.AffairPenalty);

            _events.Publish(
                new GameEvent
                {
                    Type = "relationship.affair",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    RelatedPersonIds = [spouse.Id],
                    Data = new Dictionary<string, string>
                    {
                        ["satisfactionLoss"] =
                            MarriageBalanceRules.AffairPenalty.ToString("0.##"),
                        ["text"] =
                            $"{_family.GetDisplayName(person)} was caught having an affair. " +
                            $"The betrayal badly strained the marriage to {_family.GetDisplayName(spouse)}."
                    }
                });
        }
    }
}
