using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal sealed class MarriageDivorceYearSystem : IYearSystem
{
    private readonly StandardMarriageSatisfactionService _satisfaction;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IGameRandom _random;
    private readonly RelationshipBreakupService _breakups;
    private readonly IRelationshipEraService _relationshipEras;

    public MarriageDivorceYearSystem(
        StandardMarriageSatisfactionService satisfaction,
        IFamilyService family,
        IStatsService stats,
        IGameRandom random,
        RelationshipBreakupService breakups,
        IRelationshipEraService relationshipEras)
    {
        _satisfaction = satisfaction;
        _family = family;
        _stats = stats;
        _random = random;
        _breakups = breakups;
        _relationshipEras = relationshipEras;
    }

    public string Id => "relationships.automatic_divorce";
    public YearPhase Phase => YearPhase.MarriageDivorce;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        var husbands = gameState.People
            .Where(person =>
                person.Tags.Has("state.alive")
                && _family.GetSex(person) == Sex.Male)
            .ToList();

        foreach (var husband in husbands)
        {
            var wife = _family.GetSpouse(husband);
            if (wife is null
                || !wife.Tags.Has("state.alive")
                || _family.GetSex(wife) != Sex.Female)
            {
                continue;
            }

            var satisfaction = _satisfaction.GetSatisfaction(husband);
            if (satisfaction is null)
                continue;

            var baseChance =
                MarriageBalanceRules.GetAutomaticDivorceChance(
                    satisfaction.Value);

            if (baseChance <= 0)
                continue;

            var historicalChance =
                _relationshipEras
                    .GetRule(gameState.Year)
                    .ApplyAutomaticDivorceChance(
                        baseChance);

            var divorceChance =
                RelationshipPersonalityRules.AdjustAutonomousDivorceChance(
                    historicalChance,
                    husband,
                    wife,
                    _stats);

            if (_random.NextDouble() >= divorceChance)
                continue;

            _breakups.LowSatisfactionDivorce(
                gameState,
                husband,
                wife,
                satisfaction.Value);
        }
    }

}
