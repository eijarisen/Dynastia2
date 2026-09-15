using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal sealed class FamilyRelationYearSystem : IYearSystem
{
    private readonly StandardFamilyRelationService _relations;
    private readonly IGameState _gameState;

    public FamilyRelationYearSystem(
        StandardFamilyRelationService relations,
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        IHouseholdService households,
        IPersonalityService? personality,
        IGameRandom random)
    {
        _relations = relations;
        _gameState = gameState;
    }

    public string Id => "family_relations.annual";
    public YearPhase Phase => YearPhase.FamilyRelations;
    public IReadOnlyCollection<string> Before => [];
    public IReadOnlyCollection<string> After => [];

    public void Execute(IGameState gameState)
    {
        _relations.ReconcileAll();

        foreach (var relation in _relations.GetAllRelationships())
        {
            var first = Find(relation.PersonAId);
            var second = Find(relation.PersonBId);
            if (first is null || second is null
                || !first.Tags.Has("state.alive")
                || !second.Tags.Has("state.alive"))
            {
                continue;
            }

            // Familiarity is permanent once acquired. Sympathy slowly relaxes
            // toward Neutral; kinship tolerance makes strong family bonds fade
            // more slowly on the positive side.
            _relations.DriftSympathyTowardNeutral(first, second);
        }
    }

    private IPerson? Find(Guid id) => _gameState.People.FirstOrDefault(p => p.Id == id);
}
