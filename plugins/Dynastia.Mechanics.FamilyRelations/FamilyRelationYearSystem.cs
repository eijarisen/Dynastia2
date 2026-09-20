using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal sealed class FamilyRelationYearSystem : IYearSystem
{
    private readonly StandardFamilyRelationService _relations;
    private readonly IGameState _gameState;
    private readonly IEconomyService _economy;
    private readonly ILocationService _locations;

    public FamilyRelationYearSystem(
        StandardFamilyRelationService relations,
        IGameState gameState,
        IEconomyService economy,
        ILocationService locations)
    {
        _relations = relations;
        _gameState = gameState;
        _economy = economy;
        _locations = locations;
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

            var firstHousehold = _economy.GetHouseholdId(first);
            var secondHousehold = _economy.GetHouseholdId(second);
            var sameHousehold = firstHousehold is not null
                && firstHousehold == secondHousehold;

            var firstTown = _locations.GetLocation(first).HomeTown;
            var secondTown = _locations.GetLocation(second).HomeTown;
            var distance = firstTown.Id.Equals(
                    secondTown.Id,
                    StringComparison.OrdinalIgnoreCase)
                ? 0.0
                : FamilyRelationDistanceRules.DistanceKm(
                    firstTown.Latitude,
                    firstTown.Longitude,
                    secondTown.Latitude,
                    secondTown.Longitude);

            var drift = FamilyRelationDistanceRules.GetAnnualDrift(
                distance,
                sameHousehold);

            // Distance erodes familiarity only gradually, while Sympathy
            // always relaxes toward Neutral rather than toward hostility.
            // Active family interactions add Familiarity/Sympathy and can
            // therefore counter this passive drift.
            _relations.DriftTowardNeutralForDistance(
                first,
                second,
                drift.FamiliarityLoss,
                drift.SympathyDrift);
        }
    }

    private IPerson? Find(Guid id) =>
        _gameState.People.FirstOrDefault(person => person.Id == id);
}
