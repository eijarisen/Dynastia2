using Dynastia.Contracts;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed partial class AutonomousStrategyCharacterizationTests
{
    [Fact]
    public void TwoLivingDaughtersReachTheDeliberateExpansionSoftStop()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var first = LineageChild(f, wife, 7, Sex.Female);
        var second = LineageChild(f, wife, 4, Sex.Female);

        var snapshot = BuildLineageSnapshot(f, [wife, first, second]);

        Assert.Equal(2, snapshot.ExistingChildCount);
        Assert.Equal(2, snapshot.ViableBloodlineDescendantCount);
        Assert.True(snapshot.NeedsMaleLineContinuity); // diagnostic only
        Assert.False(snapshot.NeedsFamilyExpansion);
        Assert.False(snapshot.CanActivelyTryForChild);
        Assert.Null(f.Strategy.ScoreAction(
            Candidate("reproduction.try_for_baby", f.Head), snapshot));
    }

    [Theory]
    [InlineData(Sex.Male)]
    [InlineData(Sex.Female)]
    public void OneHealthyChildLeavesRoomForOneSustainableSecondChildRegardlessOfSex(Sex childSex)
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var child = LineageChild(f, wife, 7, childSex);

        var snapshot = BuildLineageSnapshot(f, [wife, child]);

        Assert.Equal(1, snapshot.ExistingChildCount);
        Assert.True(snapshot.NeedsFamilyExpansion);
        Assert.True(snapshot.CanActivelyTryForChild);
        var action = Assert.IsType<AutonomousActionCandidate>(f.Strategy.ScoreAction(
            Candidate("reproduction.try_for_baby", f.Head), snapshot));
        Assert.Equal(AutonomousPriorityBands.SustainableFamilyContinuity, action.PriorityBand);
        Assert.Equal(0, action.LineagePriority);
    }

    [Fact]
    public void SickChildRemainsACommitmentAndBlocksDiscretionarySecondBirth()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var child = LineageChild(f, wife, 7, Sex.Female);

        var snapshot = BuildLineageSnapshot(
            f,
            [wife, child],
            health: new Dictionary<Guid, double> { [child.Id] = 45 });

        Assert.Equal(1, snapshot.ExistingChildCount);
        Assert.True(snapshot.HasMaterialUnmetDependentNeed);
        Assert.False(snapshot.CanActivelyTryForChild);
        Assert.Null(f.Strategy.ScoreAction(
            Candidate("reproduction.try_for_baby", f.Head), snapshot));
    }

    [Fact]
    public void InfertilityDoesNotRemoveLivingChildrenFromTheSoftStop()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var first = LineageChild(f, wife, 20, Sex.Male);
        var second = LineageChild(f, wife, 18, Sex.Female);

        var snapshot = BuildLineageSnapshot(
            f,
            [wife, first, second],
            fertility: new Dictionary<Guid, int> { [first.Id] = 0, [second.Id] = 0 });

        Assert.Equal(2, snapshot.ExistingChildCount);
        Assert.False(snapshot.NeedsFamilyExpansion);
        Assert.False(snapshot.CanActivelyTryForChild);
        Assert.True(snapshot.NeedsMaleLineContinuity); // may remain true, but cannot trigger replacement births
    }

    [Fact]
    public void SpousesExistingChildrenCountTowardTheHouseholdsFamilyCommitment()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var otherFather = f.World.Person(40);
        var stepchild = f.World.Person(10, sex: Sex.Female);
        stepchild.Tags.Remove("family.bloodline");
        stepchild.Tags.Remove("lineage.male");
        f.World.Family.SetParents(stepchild, otherFather, wife);

        var snapshot = BuildLineageSnapshot(f, [wife, stepchild]);

        Assert.Contains(snapshot.ExistingChildren, child => child.Id == stepchild.Id);
        Assert.Equal(1, snapshot.ExistingChildCount);
    }

    [Fact]
    public void RecognizedAdoptedChildrenCountTowardTheHouseholdsFamilyCommitment()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var adopted = f.World.Person(8, sex: Sex.Female);
        adopted.Tags.Remove("family.bloodline");
        adopted.Tags.Remove("lineage.male");
        f.Context.AddService<IAdoptionService>(new AdoptionStub(adopted.Id, f.Head.Id));

        var snapshot = BuildLineageSnapshot(f, [wife, adopted]);

        Assert.Contains(snapshot.ExistingChildren, child => child.Id == adopted.Id);
        Assert.Equal(1, snapshot.ExistingChildCount);
    }

    [Fact]
    public void EstablishedDescendantFamilyPreventsParentsFromStartingOverAfterDirectChildDeath()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var deadSon = f.World.Person(27, alive: false);
        f.World.Family.SetParents(deadSon, f.Head, wife);
        var daughterInLaw = f.World.Person(26, sex: Sex.Female);
        f.World.Family.SetSpouses(deadSon, daughterInLaw, 1900);
        deadSon.Tags.Add("state.dead");
        var grandson = f.World.Person(3);
        f.World.Family.SetParents(grandson, deadSon, daughterInLaw);

        var snapshot = BuildLineageSnapshot(f, [wife]);

        Assert.Empty(snapshot.ExistingChildren);
        Assert.True(snapshot.HasEstablishedDescendantFamily);
        Assert.False(snapshot.NeedsFamilyExpansion);
        Assert.False(snapshot.CanActivelyTryForChild);
        Assert.Contains(snapshot.LivingMaleLineDescendants, person => person.Id == grandson.Id);
    }

    [Fact]
    public void ResidentAdultChildFormationPrecedesAParentsDiscretionarySecondBirth()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var adult = LineageChild(f, wife, 22, Sex.Female);

        var snapshot = BuildLineageSnapshot(f, [wife, adult]);

        Assert.Equal(1, snapshot.ExistingChildCount);
        Assert.True(snapshot.NeedsFamilyExpansion);
        Assert.True(snapshot.HasAdultFamilyFormationNeed);
        Assert.False(snapshot.CanActivelyTryForChild);
        Assert.Null(f.Strategy.ScoreAction(
            Candidate("reproduction.try_for_baby", f.Head), snapshot));
        var marriage = Assert.IsType<AutonomousActionCandidate>(f.Strategy.ScoreAction(
            Candidate("relationship.marry_off_daughter", adult), snapshot));
        Assert.Equal(AutonomousPriorityBands.SustainableFamilyContinuity, marriage.PriorityBand);
    }

    [Fact]
    public void StableChronicIllnessDoesNotFreezeAnAdultChildMarriagePlan()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var adult = LineageChild(f, wife, 22, Sex.Female);
        var snapshot = BuildLineageSnapshot(f, [wife, adult]) with
        {
            HasSeriousMedicalDanger = true,
            HasImmediateMedicalDanger = false,
            HasMaterialUnmetDependentNeed = false
        };

        Assert.NotNull(f.Strategy.ScoreAction(
            Candidate("relationship.marry_off_daughter", adult), snapshot));
    }

    [Fact]
    public void MaleLineIsOnlyAFinalTieBreakBetweenEquivalentAdultFamilyPlans()
    {
        using var f = new Fixture();
        var son = f.World.Person(22);
        var daughter = f.World.Person(22, sex: Sex.Female);
        var snapshot = f.Snapshot with
        {
            ExistingChildren = [son, daughter],
            HasAdultFamilyFormationNeed = true,
            Members =
            [
                Member(f.Head),
                Member(son) with { IsBloodline = true, IsMaleLineage = true },
                Member(daughter) with { IsBloodline = true }
            ]
        };

        var male = Assert.IsType<AutonomousActionCandidate>(f.Strategy.ScoreAction(
            Candidate("relationship.marry_off_son", son), snapshot));
        var female = Assert.IsType<AutonomousActionCandidate>(f.Strategy.ScoreAction(
            Candidate("relationship.marry_off_daughter", daughter), snapshot));

        Assert.Equal(male.PriorityBand, female.PriorityBand);
        Assert.Equal(male.Score, female.Score);
        Assert.Equal(1, male.LineagePriority);
        Assert.Equal(0, female.LineagePriority);
        Assert.Same(male, f.Strategy.ChooseAction([female, male]));
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Theory]
    [InlineData(false, "state.imprisoned")]
    [InlineData(true, "state.imprisoned")]
    [InlineData(false, "vocation.religious.active")]
    [InlineData(true, "vocation.religious.active")]
    [InlineData(false, SimulationState.ExternalResidenceTag)]
    [InlineData(true, SimulationState.PeripheralInactiveTag)]
    public void EitherParentsUnavailableStateBlocksDeliberateConception(bool wifeUnavailable, string tag)
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        (wifeUnavailable ? wife : f.Head).Tags.Add(tag);

        var snapshot = BuildLineageSnapshot(f, [wife]);

        Assert.False(snapshot.HasRealisticReproductivePath);
        Assert.False(snapshot.CanActivelyTryForChild);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroFertilityInEitherPartnerBlocksDeliberateConception(bool wifeInfertile)
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var snapshot = BuildLineageSnapshot(f, [wife],
            fertility: new Dictionary<Guid, int> { [(wifeInfertile ? wife : f.Head).Id] = 0 });

        Assert.False(snapshot.HasRealisticReproductivePath);
        Assert.False(snapshot.CanActivelyTryForChild);
    }

    private static IPerson LineageWife(Fixture f)
    {
        var wife = f.World.Person(30, sex: Sex.Female);
        wife.Tags.Remove("family.bloodline");
        f.World.Family.SetSpouses(f.Head, wife, 1890);
        return wife;
    }

    private static IPerson LineageChild(Fixture f, IPerson wife, int age, Sex sex)
    {
        var child = f.World.Person(age, sex: sex);
        f.World.Family.SetParents(child, f.Head, wife);
        return child;
    }

    private static IStatsService LineageStats(IReadOnlyDictionary<Guid, int>? fertility = null) =>
        Probe<IStatsService>((method, args) =>
        {
            Assert.Equal(nameof(IStatsService.GetStats), method.Name);
            var person = Assert.IsAssignableFrom<IPerson>(args[0]);
            var value = fertility?.GetValueOrDefault(person.Id, 3) ?? 3;
            return new StatValue[] { new("fertility", "Fertility", value, ""),
                new("appeal", "Appeal", 3, ""), new("strength", "Strength", 3, ""),
                new("intellect", "Intellect", 3, "") };
        });

    private static AutonomousHouseholdSnapshot BuildLineageSnapshot(
        Fixture f,
        IReadOnlyList<IPerson> otherMembers,
        int capacity = 6,
        IReadOnlyDictionary<Guid, int>? fertility = null,
        IReadOnlyDictionary<Guid, double>? health = null,
        Guid? separatedPartner = null,
        int residentCapacity = 8,
        decimal projectedIncome = 6000m)
    {
        var status = new HouseholdStatusSnapshot(f.Head.Id, otherMembers.Count + 1,
            capacity, capacity, null, null, false, false, false, false, false, [],
            ResidentCount: otherMembers.Count + 1, OvercrowdingThreshold: residentCapacity,
            IsOvercrowded: otherMembers.Count + 1 > residentCapacity);
        var households = Probe<IHouseholdService>((method, _) => method.Name switch
        {
            nameof(IHouseholdService.GetStatus) => status,
            _ => throw new InvalidOperationException(method.Name)
        });
        var economy = Probe<IEconomyService>((method, args) => method.Name switch
        {
            nameof(IEconomyService.GetHousehold) => f.Snapshot.Finance,
            nameof(IEconomyService.GetAnnualForecast) => new HouseholdAnnualForecast(projectedIncome, 2000m,
                [], [new FinanceBreakdownItem("living costs", 1200m)]),
            nameof(IEconomyService.GetHouseholdId) => ((IPerson)args[0]!).Id == separatedPartner
                ? Guid.Empty : f.Snapshot.Household.HouseholdId,
            nameof(IEconomyService.GetResidenceTown) => f.World.Town,
            nameof(IEconomyService.GetLivingCostPerPerson) => 500m,
            _ => throw new InvalidOperationException(method.Name)
        });
        var healthService = Probe<IHealthService>((method, args) =>
        {
            Assert.Equal(nameof(IHealthService.GetHealth), method.Name);
            var person = Assert.IsAssignableFrom<IPerson>(args[0]);
            return new HealthSnapshot(health?.GetValueOrDefault(person.Id, 100) ?? 100, 100, []);
        });
        var career = Probe<ICareerService>((method, _) => method.Name switch
        {
            nameof(ICareerService.GetCareer) => EmployedCareer(),
            _ => throw new InvalidOperationException(method.Name)
        });
        var household = f.Snapshot.Household with
        {
            MemberIds = new[] { f.Head.Id }.Concat(otherMembers.Select(person => person.Id)).ToArray()
        };
        return CreateStrategy(f, households, healthService, career, LineageStats(fertility), economy)
            .BuildSnapshot(household);
    }

    private sealed class AdoptionStub : IAdoptionService
    {
        private readonly Guid _childId;
        private readonly Guid _guardianId;

        public AdoptionStub(Guid childId, Guid guardianId)
        {
            _childId = childId;
            _guardianId = guardianId;
        }

        public AdoptionPlacementInfo GetPlacement(IPerson person) =>
            person.Id == _childId
                ? new AdoptionPlacementInfo(AdoptionPlacementKind.AdoptiveHousehold,
                    _guardianId, _guardianId, false, null, "Adopted")
                : new AdoptionPlacementInfo(AdoptionPlacementKind.BiologicalHousehold,
                    null, null, false, null, string.Empty);

        public IReadOnlyList<IPerson> GetHostedChildren(IPerson householdHead) => [];

        public bool HasOrphanTrait(IPerson person) => false;
    }
}
