using Dynastia.Contracts;
using Dynastia.Core.Entities;
using Dynastia.Mechanics.Appearance;

namespace Dynastia.Core.Tests;

public sealed class AppearancePortraitTests
{
    [Fact]
    public void CandidateAppearance_IsDeterministic_AndPortraitsVary()
    {
        var service = new StandardAppearanceService(new TestFamilyService());
        var id = Guid.Parse("9d607247-d621-452e-8951-312421c13d91");

        var first = service.GenerateCandidateAppearance(id, Sex.Male);
        var second = service.GenerateCandidateAppearance(id, Sex.Male);

        Assert.Equal(first, second);

        var portraits = Enumerable.Range(0, 24)
            .Select(index =>
            {
                var candidateId = DeterministicGuid(index);
                var appearance = service.GenerateCandidateAppearance(
                    candidateId,
                    Sex.Female);
                return service.GetPortrait(
                    appearance,
                    Sex.Female,
                    28,
                    candidateId);
            })
            .Distinct()
            .ToList();

        Assert.True(portraits.Count > 2);
    }


    [Fact]
    public void BaselineHairDistribution_IsVariedWhileStillMostlyDark()
    {
        var service = new StandardAppearanceService(new TestFamilyService());
        const int sampleSize = 4000;
        var counts = Enum.GetValues<HairColor>()
            .ToDictionary(color => color, _ => 0);

        for (var index = 0; index < sampleSize; index++)
        {
            var appearance = service.GenerateCandidateAppearance(
                DeterministicGuid(index + 1000),
                Sex.Female);
            counts[appearance.HairColor]++;
        }

        var darkShare =
            (counts[HairColor.Black] + counts[HairColor.Brown])
            / (double)sampleSize;
        var blondShare = counts[HairColor.Blond] / (double)sampleSize;
        var redShare = counts[HairColor.Red] / (double)sampleSize;

        Assert.InRange(darkShare, 0.58, 0.70);
        Assert.InRange(blondShare, 0.27, 0.36);
        Assert.InRange(redShare, 0.025, 0.065);
    }

    [Fact]
    public void PortraitRules_KeepBeardsAndBaldnessOutOfChildPortraits()
    {
        var service = new StandardAppearanceService(new TestFamilyService());
        var id = Guid.Parse("b3d29439-30bd-4e06-a899-7fcfbb1e175f");
        var appearance = new AppearanceSnapshot(
            HairColor.Brown,
            HairColor.Brown,
            HairColor.Brown,
            HairTexture.Straight,
            40,
            BaldingTendency.Strong,
            BeardDensity.Strong);

        var child = service.GetPortrait(
            appearance,
            Sex.Male,
            10,
            id);
        var woman = service.GetPortrait(
            appearance with { BeardDensity = BeardDensity.Strong },
            Sex.Female,
            35,
            id);
        var elder = service.GetPortrait(
            appearance,
            Sex.Male,
            74,
            id);

        Assert.DoesNotContain("🧔", child);
        Assert.DoesNotContain("👨‍🦲", child);
        Assert.DoesNotContain("🧔", woman);
        Assert.Equal("👴🏻", elder);
    }

    [Fact]
    public void Portraits_DoNotAppendHairColorMarkerEmoji()
    {
        var service = new StandardAppearanceService(new TestFamilyService());
        var id = Guid.Parse("d5f729ba-f67a-4e07-a0c9-5c1b47ce87cc");

        foreach (var hairColor in Enum.GetValues<HairColor>())
        {
            var portrait = service.GetPortrait(
                new AppearanceSnapshot(
                    hairColor,
                    hairColor,
                    hairColor,
                    HairTexture.Straight,
                    65,
                    BaldingTendency.None,
                    BeardDensity.None),
                Sex.Female,
                28,
                id);

            Assert.DoesNotContain("⚫", portrait);
            Assert.DoesNotContain("🟤", portrait);
            Assert.DoesNotContain("🟡", portrait);
            Assert.DoesNotContain("🔴", portrait);
        }
    }

    [Fact]
    public void Portraits_UseQualifiedHairVariants_AndPreserveLastAppearanceForDeadPeople()
    {
        var service = new StandardAppearanceService(new TestFamilyService());
        var id = Guid.Parse("2f1b51bb-266c-4c92-942d-56354d50591c");

        var red = service.GetPortrait(
            new AppearanceSnapshot(
                HairColor.Red,
                HairColor.Red,
                HairColor.Red,
                HairTexture.Straight,
                65,
                BaldingTendency.None,
                BeardDensity.Strong),
            Sex.Male,
            32,
            id);

        var blond = service.GetPortrait(
            new AppearanceSnapshot(
                HairColor.Blond,
                HairColor.Blond,
                HairColor.Blond,
                HairTexture.Straight,
                65,
                BaldingTendency.None,
                BeardDensity.Strong),
            Sex.Female,
            32,
            id);

        var dead = service.GetPortrait(
            new AppearanceSnapshot(
                HairColor.Red,
                HairColor.Red,
                HairColor.Red,
                HairTexture.Straight,
                65,
                BaldingTendency.None,
                BeardDensity.None),
            Sex.Female,
            32,
            id,
            isDead: true);

        Assert.Equal("👨🏻‍🦰", red);
        Assert.Equal("👱🏻‍♀️", blond);
        Assert.Equal("👩🏻‍🦰", dead);
        Assert.DoesNotContain("💀", dead);
    }

    [Fact]
    public void ChildHairGenes_AreInheritedFromParents()
    {
        var family = new TestFamilyService();
        var service = new StandardAppearanceService(family);
        var father = CreatePerson("Father", Sex.Male, family);
        var mother = CreatePerson("Mother", Sex.Female, family);
        var child = CreatePerson("Child", Sex.Male, family);
        family.SetParents(child, father, mother);

        service.SetAppearance(
            father,
            new AppearanceSnapshot(
                HairColor.Black,
                HairColor.Brown,
                HairColor.Black,
                HairTexture.Straight,
                48,
                BaldingTendency.Mild,
                BeardDensity.Normal));
        service.SetAppearance(
            mother,
            new AppearanceSnapshot(
                HairColor.Blond,
                HairColor.Red,
                HairColor.Blond,
                HairTexture.Curly,
                55,
                BaldingTendency.None,
                BeardDensity.None));

        var inherited = service.EnsureAppearance(child);

        Assert.Contains(
            inherited.HairGeneA,
            new[] { HairColor.Black, HairColor.Brown });
        Assert.Contains(
            inherited.HairGeneB,
            new[] { HairColor.Blond, HairColor.Red });
    }


    [Fact]
    public void InheritedHairExpression_UsesOneGeneFromEachParentAndDominance()
    {
        var family = new TestFamilyService();
        var service = new StandardAppearanceService(family);
        var father = CreatePerson("Father", Sex.Male, family);
        var mother = CreatePerson("Mother", Sex.Female, family);
        var child = CreatePerson("Child", Sex.Female, family);
        family.SetParents(child, father, mother);

        service.SetAppearance(
            father,
            new AppearanceSnapshot(
                HairColor.Black,
                HairColor.Black,
                HairColor.Black,
                HairTexture.Straight,
                50,
                BaldingTendency.None,
                BeardDensity.Normal));
        service.SetAppearance(
            mother,
            new AppearanceSnapshot(
                HairColor.Blond,
                HairColor.Blond,
                HairColor.Blond,
                HairTexture.Straight,
                50,
                BaldingTendency.None,
                BeardDensity.None));

        var inherited = service.EnsureAppearance(child);

        Assert.Equal(HairColor.Black, inherited.HairGeneA);
        Assert.Equal(HairColor.Blond, inherited.HairGeneB);
        Assert.Equal(HairColor.Black, inherited.HairColor);
    }

    [Fact]
    public void TwoRedHairParents_ProduceRedHairExpression()
    {
        var family = new TestFamilyService();
        var service = new StandardAppearanceService(family);
        var father = CreatePerson("Father", Sex.Male, family);
        var mother = CreatePerson("Mother", Sex.Female, family);
        var child = CreatePerson("Child", Sex.Male, family);
        family.SetParents(child, father, mother);

        var red = new AppearanceSnapshot(
            HairColor.Red,
            HairColor.Red,
            HairColor.Red,
            HairTexture.Straight,
            50,
            BaldingTendency.None,
            BeardDensity.None);
        service.SetAppearance(father, red);
        service.SetAppearance(mother, red);

        Assert.Equal(HairColor.Red, service.EnsureAppearance(child).HairColor);
    }

    [Fact]
    public void PersonPortraitReadDoesNotCreateMissingAppearanceState()
    {
        var family = new TestFamilyService();
        var service = new StandardAppearanceService(family);
        var person = CreatePerson("Jan", Sex.Male, family);

        Assert.False(person.Components.Has<AppearanceComponent>());
        Assert.Throws<InvalidOperationException>(() => service.GetPortrait(person));
        Assert.False(person.Components.Has<AppearanceComponent>());

        service.EnsureAppearance(person);
        Assert.False(string.IsNullOrWhiteSpace(service.GetPortrait(person)));
    }

    [Fact]
    public void EnsureAppearance_DoesNotRerollExistingAppearance()
    {
        var family = new TestFamilyService();
        var service = new StandardAppearanceService(family);
        var person = CreatePerson("Jan", Sex.Male, family);

        var first = service.EnsureAppearance(person);
        var second = service.EnsureAppearance(person);

        Assert.Equal(first, second);
    }

    private static Person CreatePerson(
        string name,
        Sex sex,
        TestFamilyService family)
    {
        var person = new Person(name, "Kowalski", 20);
        person.Tags.Add("state.alive");
        family.InitializePerson(person, sex);
        return person;
    }

    private static Guid DeterministicGuid(int index)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(index + 1).CopyTo(bytes, 0);
        BitConverter.GetBytes((index + 1) * 7919).CopyTo(bytes, 8);
        return new Guid(bytes);
    }

    private sealed class TestFamilyService : IFamilyService
    {
        private readonly Dictionary<Guid, Sex> _sex = [];
        private readonly Dictionary<Guid, IPerson?> _fathers = [];
        private readonly Dictionary<Guid, IPerson?> _mothers = [];

        public void InitializePerson(IPerson person, Sex sex, int? generation = null)
        {
            _sex[person.Id] = sex;
            person.Tags.Add(sex == Sex.Male ? "sex.male" : "sex.female");
        }

        public Sex GetSex(IPerson person) => _sex[person.Id];

        public int? GetGeneration(IPerson person) => null;

        public IPerson? GetFather(IPerson person) =>
            _fathers.TryGetValue(person.Id, out var father)
                ? father
                : null;

        public IPerson? GetMother(IPerson person) =>
            _mothers.TryGetValue(person.Id, out var mother)
                ? mother
                : null;

        public IPerson? GetSpouse(IPerson person) => null;

        public IReadOnlyList<IPerson> GetChildren(IPerson person) => [];

        public void SetParents(IPerson child, IPerson? father, IPerson? mother)
        {
            _fathers[child.Id] = father;
            _mothers[child.Id] = mother;
        }

        public void SetSpouses(IPerson first, IPerson second, int startYear) =>
            throw new NotSupportedException();

        public void EndRelationship(
            IPerson first,
            IPerson second,
            int endYear,
            string endReason,
            bool clearFirst = true,
            bool clearSecond = true) =>
            throw new NotSupportedException();

        public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(
            IPerson person) => [];

        public void SetGeneratedFamilyBackground(
            IPerson person,
            GeneratedFamilyBackgroundInfo background) =>
            throw new NotSupportedException();

        public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(
            IPerson person) => null;

        public string FormatSurname(string surname, Sex sex) => surname;

        public string GetDisplayName(IPerson person) =>
            $"{person.Name} {person.Surname}";

        public bool IsBloodline(IPerson person) =>
            person.Tags.Has("family.bloodline");

        public bool IsMaleLineage(IPerson person) =>
            person.Tags.Has("lineage.male");
    }
}
