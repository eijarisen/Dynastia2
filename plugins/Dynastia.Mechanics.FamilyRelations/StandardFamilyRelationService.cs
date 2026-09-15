using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

public sealed class StandardFamilyRelationService : IFamilyRelationService
{
    private const double ParentChildStart = 70;
    private const double LegacyExSpouseStart = 45;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IHouseholdService _households;
    private readonly IPersonalityService? _personality;
    private readonly IMarriageSatisfactionService _marriage;
    private readonly IGameRandom _random;

    public StandardFamilyRelationService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        IHouseholdService households,
        IPersonalityService? personality,
        IMarriageSatisfactionService marriage,
        IGameRandom random)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _households = households;
        _personality = personality;
        _marriage = marriage;
        _random = random;
    }

    public FamilyRelationshipSnapshot? GetRelation(IPerson first, IPerson second)
    {
        if (first.Id == second.Id)
            return null;

        var data = Find(first, second);
        return data is null ? null : ToSnapshot(data);
    }

    public FamilyRelationshipSnapshot EnsureRelation(
        IPerson first,
        IPerson second,
        FamilyRelationshipType type,
        double startingScore,
        bool majorInteraction = false)
    {
        if (first.Id == second.Id)
            throw new InvalidOperationException("A person cannot have a family relation with themselves.");

        var existing = Find(first, second);
        if (existing is null)
        {
            var (owner, a, b) = ResolveCanonical(first, second);
            var component = GetComponent(owner);
            existing = new FamilyRelationshipData
            {
                PersonAId = a.Id,
                PersonBId = b.Id,
                Type = (int)type,
                Score = Math.Clamp(startingScore, 0, 100),
                CreatedYear = _gameState.Year,
                LastMajorInteractionYear = majorInteraction ? _gameState.Year : 0
            };
            component.Relationships.Add(existing);
            owner.Components.Set(component);
        }

        return ToSnapshot(existing);
    }

    public FamilyRelationshipSnapshot ModifyRelation(
        IPerson first,
        IPerson second,
        double amount,
        bool majorInteraction = true)
    {
        var data = Find(first, second)
            ?? throw new InvalidOperationException("The requested family relationship is not tracked.");

        data.Score = Math.Clamp(data.Score + amount, 0, 100);
        if (majorInteraction)
            data.LastMajorInteractionYear = _gameState.Year;

        var owner = ResolveCanonical(first, second).Owner;
        owner.Components.Set(GetComponent(owner));
        return ToSnapshot(data);
    }

    public string GetDisplayState(double score) =>
        FamilyRelationScoreRules.GetDisplayState(score);

    public IReadOnlyList<RelatedFamilyHouseholdInfo> GetRelatedHouseholds(IPerson activeHouseholdHead)
    {
        var activeHouseholdId = _economy.GetHouseholdId(activeHouseholdHead);
        var relatives = GetCloseRelatives(activeHouseholdHead)
            .Where(relative => relative.Person.Tags.Has("state.alive"))
            .GroupBy(relative => relative.Person.Id)
            .Select(group => group.First())
            .Select(relative =>
            {
                var head = _households.ResolveHouseholdHead(relative.Person);
                var householdId = head is null ? null : _economy.GetHouseholdId(head);
                var info = head is null
                    ? null
                    : _households.GetActiveHouseholds().FirstOrDefault(h => h.HouseholdId == householdId);
                var relation = GetRelation(activeHouseholdHead, relative.Person)
                    ?? EnsureRelation(activeHouseholdHead, relative.Person, relative.Type, DefaultScore(relative.Type));
                return new
                {
                    relative.Person,
                    relative.Kinship,
                    relative.Type,
                    Head = head,
                    HouseholdId = householdId,
                    HouseholdClass = info?.Class,
                    Relation = relation
                };
            })
            .Where(item => item.HouseholdId is null || item.HouseholdId != activeHouseholdId)
            .ToList();

        var grouped = relatives
            .GroupBy(item => item.HouseholdId?.ToString() ?? $"peripheral:{item.Person.Id}")
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(item => KinshipPriority(item.Kinship))
                    .ThenByDescending(item => item.Relation.Score)
                    .ToList();
                var first = ordered[0];
                var links = ordered.Select(item => new FamilyRelationLinkInfo(
                    item.Person.Id,
                    item.Type,
                    item.Kinship,
                    item.Relation.Score,
                    item.Relation.State)).ToList();
                return new RelatedFamilyHouseholdInfo(
                    first.HouseholdId,
                    first.Head?.Id,
                    first.HouseholdClass,
                    links);
            })
            .OrderBy(info => KinshipPriority(info.PrimaryRelation.Kinship))
            .ThenByDescending(info => info.PrimaryRelation.Score)
            .ToList();

        return grouped;
    }

    public double EvaluateRequestWillingness(
        IPerson requester,
        IPerson relative,
        double abilityFactor = 1.0)
    {
        var score = GetRelation(requester, relative)?.Score ?? 50;
        return FamilyRelationScoreRules.GetRequestWillingness(
            score,
            abilityFactor);
    }

    internal IReadOnlyList<FamilyRelationshipSnapshot> GetAllRelationships()
    {
        return _gameState.People
            .SelectMany(person =>
                person.Components.Get<FamilyRelationsComponent>()?.Relationships
                ?? Enumerable.Empty<FamilyRelationshipData>())
            .Select(ToSnapshot)
            .ToList();
    }

    public void ReconcileAll()
    {
        foreach (var person in _gameState.People)
        {
            var father = _family.GetFather(person);
            var mother = _family.GetMother(person);

            if (father is not null)
                EnsureRelation(father, person, FamilyRelationshipType.ParentChild, ParentChildStart + CompatibilityStartBonus(father, person));
            if (mother is not null)
                EnsureRelation(mother, person, FamilyRelationshipType.ParentChild, ParentChildStart + CompatibilityStartBonus(mother, person));

            foreach (var history in _family.GetRelationshipHistory(person).Where(h => h.EndYear is not null))
            {
                var former = FindPerson(history.SpouseId);
                if (former is not null)
                    EnsureRelation(person, former, FamilyRelationshipType.ExSpouse, LegacyExSpouseStart);
            }
        }

        foreach (var younger in _gameState.People.Where(p => p.Age >= 5))
        {
            foreach (var sibling in GetSiblings(younger).Where(s => s.Id != younger.Id && s.Age >= 5))
            {
                if (GetRelation(younger, sibling) is not null)
                    continue;

                var start = 58 + _random.NextInt(-8, 8) + CompatibilityStartBonus(younger, sibling);
                EnsureRelation(younger, sibling, FamilyRelationshipType.Sibling, start);
            }
        }
    }

    public void ConvertDivorceToExSpouse(IPerson first, IPerson second, double extraDamage)
    {
        var satisfaction = _marriage.GetSatisfactionBetween(first, second)?.Value ?? 50;
        var relation = EnsureRelation(
            first,
            second,
            FamilyRelationshipType.ExSpouse,
            Math.Clamp(satisfaction - extraDamage, 0, 100),
            majorInteraction: true);

        var data = Find(first, second)!;
        data.Type = (int)FamilyRelationshipType.ExSpouse;
        data.Score = Math.Clamp(satisfaction - extraDamage, 0, 100);
        data.LastMajorInteractionYear = _gameState.Year;
        ResolveCanonical(first, second).Owner.Components.Set(GetComponent(ResolveCanonical(first, second).Owner));
    }

    public IReadOnlyList<IPerson> GetSiblings(IPerson person)
    {
        var father = _family.GetFather(person);
        var mother = _family.GetMother(person);
        if (father is null && mother is null)
            return [];

        return _gameState.People
            .Where(candidate => candidate.Id != person.Id)
            .Where(candidate =>
                (father is not null && _family.GetFather(candidate)?.Id == father.Id)
                || (mother is not null && _family.GetMother(candidate)?.Id == mother.Id))
            .ToList();
    }

    private IEnumerable<(IPerson Person, FamilyRelationshipType Type, string Kinship)> GetCloseRelatives(IPerson person)
    {
        var father = _family.GetFather(person);
        if (father is not null)
            yield return (father, FamilyRelationshipType.ParentChild, "Father");
        var mother = _family.GetMother(person);
        if (mother is not null)
            yield return (mother, FamilyRelationshipType.ParentChild, "Mother");

        foreach (var sibling in GetSiblings(person))
            yield return (sibling, FamilyRelationshipType.Sibling, _family.GetSex(sibling) == Sex.Male ? "Brother" : "Sister");

        foreach (var child in _family.GetChildren(person).Where(child => child.Age >= 18))
            yield return (child, FamilyRelationshipType.ParentChild, _family.GetSex(child) == Sex.Male ? "Son" : "Daughter");

        foreach (var grandparent in GetGrandparents(person))
        {
            yield return (
                grandparent,
                FamilyRelationshipType.GrandparentGrandchild,
                _family.GetSex(grandparent) == Sex.Male ? "Grandfather" : "Grandmother");
        }

        foreach (var grandchild in GetGrandchildren(person))
        {
            yield return (
                grandchild,
                FamilyRelationshipType.GrandparentGrandchild,
                _family.GetSex(grandchild) == Sex.Male ? "Grandson" : "Granddaughter");
        }

        foreach (var parentSibling in GetParentSiblings(person))
        {
            yield return (
                parentSibling,
                FamilyRelationshipType.UncleAuntNieceNephew,
                _family.GetSex(parentSibling) == Sex.Male ? "Uncle" : "Aunt");

            foreach (var cousin in _family.GetChildren(parentSibling))
            {
                if (cousin.Id == person.Id)
                    continue;

                yield return (
                    cousin,
                    FamilyRelationshipType.FirstCousin,
                    "First cousin");
            }
        }

        var currentSpouseId = _family.GetSpouse(person)?.Id;
        foreach (var history in _family.GetRelationshipHistory(person).Where(h => h.EndYear is not null))
        {
            var former = FindPerson(history.SpouseId);
            if (former is not null && former.Id != currentSpouseId)
                yield return (former, FamilyRelationshipType.ExSpouse, _family.GetSex(former) == Sex.Male ? "Ex-husband" : "Ex-wife");
        }
    }


    private IReadOnlyList<IPerson> GetGrandparents(IPerson person)
    {
        var result = new Dictionary<Guid, IPerson>();
        foreach (var parent in new[] { _family.GetFather(person), _family.GetMother(person) })
        {
            if (parent is null)
                continue;

            var grandfather = _family.GetFather(parent);
            var grandmother = _family.GetMother(parent);
            if (grandfather is not null) result[grandfather.Id] = grandfather;
            if (grandmother is not null) result[grandmother.Id] = grandmother;
        }

        return result.Values.ToList();
    }

    private IReadOnlyList<IPerson> GetGrandchildren(IPerson person)
    {
        var result = new Dictionary<Guid, IPerson>();
        foreach (var child in _family.GetChildren(person))
        foreach (var grandchild in _family.GetChildren(child))
            result[grandchild.Id] = grandchild;

        return result.Values.ToList();
    }

    private IReadOnlyList<IPerson> GetParentSiblings(IPerson person)
    {
        var result = new Dictionary<Guid, IPerson>();
        foreach (var parent in new[] { _family.GetFather(person), _family.GetMother(person) })
        {
            if (parent is null)
                continue;

            foreach (var sibling in GetSiblings(parent))
                result[sibling.Id] = sibling;
        }

        return result.Values.ToList();
    }

    private double CompatibilityStartBonus(IPerson first, IPerson second)
    {
        if (_personality is null)
            return 0;

        var a = _personality.GetPersonality(first);
        var b = _personality.GetPersonality(second);
        if (a is null || b is null)
            return 0;

        double result = string.Equals(a.Temperament, b.Temperament, StringComparison.OrdinalIgnoreCase) ? 2 : -1;
        if (string.Equals(a.Morals, b.Morals, StringComparison.OrdinalIgnoreCase))
            result += 4;
        else if ((a.Morals.Equals("Good", StringComparison.OrdinalIgnoreCase) && b.Morals.Equals("Evil", StringComparison.OrdinalIgnoreCase))
            || (a.Morals.Equals("Evil", StringComparison.OrdinalIgnoreCase) && b.Morals.Equals("Good", StringComparison.OrdinalIgnoreCase)))
            result -= 5;
        return result;
    }

    private static double DefaultScore(FamilyRelationshipType type) => type switch
    {
        FamilyRelationshipType.ParentChild => ParentChildStart,
        FamilyRelationshipType.Sibling => 55,
        FamilyRelationshipType.GrandparentGrandchild => 60,
        FamilyRelationshipType.UncleAuntNieceNephew => 52,
        FamilyRelationshipType.FirstCousin => 48,
        _ => LegacyExSpouseStart
    };

    private static int KinshipPriority(string kinship) => kinship switch
    {
        "Father" or "Mother" => 0,
        "Son" or "Daughter" => 1,
        "Brother" or "Sister" => 2,
        "Grandfather" or "Grandmother" or "Grandson" or "Granddaughter" => 3,
        "Uncle" or "Aunt" => 4,
        "First cousin" => 5,
        _ => 6
    };

    private FamilyRelationshipData? Find(IPerson first, IPerson second)
    {
        var (owner, a, b) = ResolveCanonical(first, second);
        return owner.Components.Get<FamilyRelationsComponent>()?.Relationships
            .FirstOrDefault(r => r.PersonAId == a.Id && r.PersonBId == b.Id);
    }

    private FamilyRelationsComponent GetComponent(IPerson owner) =>
        owner.Components.Get<FamilyRelationsComponent>() ?? new FamilyRelationsComponent();

    private static (IPerson Owner, IPerson A, IPerson B) ResolveCanonical(IPerson first, IPerson second)
    {
        var firstKey = first.Id.ToString("N");
        var secondKey = second.Id.ToString("N");
        return string.CompareOrdinal(firstKey, secondKey) <= 0
            ? (first, first, second)
            : (second, second, first);
    }

    private FamilyRelationshipSnapshot ToSnapshot(FamilyRelationshipData data) =>
        new(
            data.PersonAId,
            data.PersonBId,
            (FamilyRelationshipType)data.Type,
            data.Score,
            GetDisplayState(data.Score),
            data.CreatedYear,
            data.LastMajorInteractionYear);

    private IPerson? FindPerson(Guid id) => _gameState.People.FirstOrDefault(p => p.Id == id);
}
