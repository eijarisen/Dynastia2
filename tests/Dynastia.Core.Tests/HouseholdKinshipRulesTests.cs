using Dynastia.Contracts;
using Dynastia.Core.Entities;

namespace Dynastia.Core.Tests;

public sealed class HouseholdKinshipRulesTests
{
    [Fact]
    public void SupportedHouseholdKinshipMapsExtendedFamilyIntoActionRoles()
    {
        var family = new TestFamilyService();

        var grandmother = family.Create("Grandmother", Sex.Female);
        var mother = family.Create("Mother", Sex.Female);
        var uncle = family.Create("Uncle", Sex.Male);
        var actor = family.Create("Actor", Sex.Male);
        var brother = family.Create("Brother", Sex.Male);
        var sister = family.Create("Sister", Sex.Female);
        var cousin = family.Create("Cousin", Sex.Female);
        var child = family.Create("Child", Sex.Female);
        var grandchild = family.Create("Grandchild", Sex.Male);
        var nephew = family.Create("Nephew", Sex.Male);

        family.SetParents(mother, null, grandmother);
        family.SetParents(uncle, null, grandmother);
        family.SetParents(actor, null, mother);
        family.SetParents(brother, null, mother);
        family.SetParents(sister, null, mother);
        family.SetParents(cousin, uncle, null);
        family.SetParents(child, actor, null);
        family.SetParents(grandchild, null, child);
        family.SetParents(nephew, brother, null);

        Assert.Equal(
            HouseholdKinshipRole.ParentLike,
            HouseholdKinshipRules.Resolve(actor, mother, family));
        Assert.Equal(
            HouseholdKinshipRole.ParentLike,
            HouseholdKinshipRules.Resolve(actor, grandmother, family));
        Assert.Equal(
            HouseholdKinshipRole.ParentLike,
            HouseholdKinshipRules.Resolve(actor, uncle, family));
        Assert.Equal(
            HouseholdKinshipRole.SiblingLike,
            HouseholdKinshipRules.Resolve(actor, brother, family));
        Assert.Equal(
            HouseholdKinshipRole.SiblingLike,
            HouseholdKinshipRules.Resolve(actor, sister, family));
        Assert.Equal(
            HouseholdKinshipRole.SiblingLike,
            HouseholdKinshipRules.Resolve(actor, cousin, family));
        Assert.Equal(
            HouseholdKinshipRole.ChildLike,
            HouseholdKinshipRules.Resolve(actor, child, family));
        Assert.Equal(
            HouseholdKinshipRole.ChildLike,
            HouseholdKinshipRules.Resolve(actor, grandchild, family));
        Assert.Equal(
            HouseholdKinshipRole.ChildLike,
            HouseholdKinshipRules.Resolve(actor, nephew, family));
    }

    private sealed class TestFamilyService : IFamilyService
    {
        private readonly Dictionary<Guid, Sex> _sex = [];
        private readonly Dictionary<Guid, IPerson?> _father = [];
        private readonly Dictionary<Guid, IPerson?> _mother = [];
        private readonly Dictionary<Guid, IPerson?> _spouse = [];
        private readonly Dictionary<Guid, List<IPerson>> _children = [];

        public IPerson Create(string name, Sex sex)
        {
            var person = new Person(name, "Test", 30);
            _sex[person.Id] = sex;
            _children[person.Id] = [];
            return person;
        }

        public void InitializePerson(IPerson person, Sex sex, int? generation = null)
        {
            _sex[person.Id] = sex;
            _children.TryAdd(person.Id, []);
        }

        public Sex GetSex(IPerson person) => _sex[person.Id];
        public int? GetGeneration(IPerson person) => null;
        public IPerson? GetFather(IPerson person) => _father.GetValueOrDefault(person.Id);
        public IPerson? GetMother(IPerson person) => _mother.GetValueOrDefault(person.Id);
        public IPerson? GetSpouse(IPerson person) => _spouse.GetValueOrDefault(person.Id);
        public IReadOnlyList<IPerson> GetChildren(IPerson person) =>
            _children.TryGetValue(person.Id, out var children) ? children : [];

        public void SetParents(IPerson child, IPerson? father, IPerson? mother)
        {
            _father[child.Id] = father;
            _mother[child.Id] = mother;
            _children.TryAdd(child.Id, []);
            if (father is not null)
            {
                _children.TryAdd(father.Id, []);
                _children[father.Id].Add(child);
            }
            if (mother is not null)
            {
                _children.TryAdd(mother.Id, []);
                _children[mother.Id].Add(child);
            }
        }

        public void SetSpouses(IPerson first, IPerson second, int startYear)
        {
            _spouse[first.Id] = second;
            _spouse[second.Id] = first;
        }

        public void EndRelationship(IPerson first, IPerson second, int endYear, string endReason, bool clearFirst = true, bool clearSecond = true)
        {
            if (clearFirst) _spouse[first.Id] = null;
            if (clearSecond) _spouse[second.Id] = null;
        }

        public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(IPerson person) => [];
        public void SetGeneratedFamilyBackground(IPerson person, GeneratedFamilyBackgroundInfo background) { }
        public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(IPerson person) => null;
        public string FormatSurname(string surname, Sex sex) => surname;
        public string GetDisplayName(IPerson person) => person.Name;
        public bool IsBloodline(IPerson person) => true;
        public bool IsMaleLineage(IPerson person) => _sex[person.Id] == Sex.Male;
    }
}
