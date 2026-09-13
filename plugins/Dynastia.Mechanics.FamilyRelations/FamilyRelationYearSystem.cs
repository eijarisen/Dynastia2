using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal sealed class FamilyRelationYearSystem : IYearSystem
{
    private readonly StandardFamilyRelationService _relations;
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IHouseholdService _households;
    private readonly IPersonalityService? _personality;
    private readonly IGameRandom _random;

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
        _family = family;
        _economy = economy;
        _households = households;
        _personality = personality;
        _random = random;
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

            if (relation.Type == FamilyRelationshipType.ParentChild)
            {
                ApplyParentChildChange(first, second);
                continue;
            }

            if (relation.Type == FamilyRelationshipType.Sibling)
                ApplySiblingChange(first, second);
        }
    }

    private void ApplyParentChildChange(IPerson first, IPerson second)
    {
        var child = IsParentOf(first, second) ? second
            : IsParentOf(second, first) ? first
            : null;
        var parent = child?.Id == first.Id ? second : first;

        if (child is null || child.Age >= 18)
            return;

        var childHead = _households.ResolveHouseholdHead(child);
        var parentHead = _households.ResolveHouseholdHead(parent);
        var sameHousehold = childHead is not null
            && parentHead is not null
            && _economy.GetHouseholdId(childHead) == _economy.GetHouseholdId(parentHead);

        double delta = sameHousehold ? 0.5 : 0;
        var status = childHead is null ? null : _households.GetStatus(childHead);
        if (status?.IsBroke == true)
            delta -= 0.75;
        if (status?.IsLargeFamilyStrained == true)
            delta -= 0.5;
        if (child.Tags.Has("state.parents_divorced") && !sameHousehold)
            delta -= 0.75;

        delta = Math.Clamp(delta, -2, 2);
        if (Math.Abs(delta) >= 0.01)
            _relations.ModifyRelation(first, second, delta, majorInteraction: false);
    }

    private void ApplySiblingChange(IPerson first, IPerson second)
    {
        if (Math.Min(first.Age, second.Age) < 5)
            return;

        double delta = (_random.NextDouble() - 0.5) * 0.8;
        if (_personality is not null)
        {
            var a = _personality.GetPersonality(first);
            var b = _personality.GetPersonality(second);
            if (a is not null && b is not null)
            {
                delta += string.Equals(a.Temperament, b.Temperament, StringComparison.OrdinalIgnoreCase)
                    ? 0.45 : -0.20;

                if (string.Equals(a.Morals, b.Morals, StringComparison.OrdinalIgnoreCase))
                    delta += 0.75;
                else if ((a.Morals.Equals("Good", StringComparison.OrdinalIgnoreCase) && b.Morals.Equals("Evil", StringComparison.OrdinalIgnoreCase))
                    || (a.Morals.Equals("Evil", StringComparison.OrdinalIgnoreCase) && b.Morals.Equals("Good", StringComparison.OrdinalIgnoreCase)))
                    delta -= 0.85;
            }
        }

        delta = Math.Clamp(delta, -2, 2);
        if (Math.Abs(delta) >= 0.01)
            _relations.ModifyRelation(first, second, delta, majorInteraction: false);
    }

    private bool IsParentOf(IPerson possibleParent, IPerson possibleChild) =>
        _family.GetFather(possibleChild)?.Id == possibleParent.Id
        || _family.GetMother(possibleChild)?.Id == possibleParent.Id;

    private IPerson? Find(Guid id) => _gameState.People.FirstOrDefault(p => p.Id == id);
}
