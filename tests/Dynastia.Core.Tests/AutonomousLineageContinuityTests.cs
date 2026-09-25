using Dynastia.Contracts;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed partial class AutonomousStrategyCharacterizationTests
{
    [Fact]
    public void TwoDaughtersSecureBloodlineButStillLeaveMaleLineWithoutAnHeir()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var first = LineageChild(f, wife, 7, Sex.Female);
        var second = LineageChild(f, wife, 4, Sex.Female);

        var snapshot = BuildLineageSnapshot(f, [wife, first, second]);

        Assert.Equal(2, snapshot.LivingChildCount);
        Assert.Equal(2, snapshot.ViableBloodlineDescendantCount);
        Assert.Empty(snapshot.LivingMaleLineDescendants);
        Assert.True(snapshot.HasSecuredBloodline);
        Assert.False(snapshot.HasSecuredMaleLine);
        Assert.True(snapshot.NeedsMaleLineContinuity);
        Assert.True(snapshot.CanActivelyTryForChild);
        var action = Assert.IsType<AutonomousActionCandidate>(f.Strategy.ScoreAction(
            Candidate("reproduction.try_for_baby", f.Head), snapshot));
        Assert.Equal(AutonomousPriorityBands.MaleLineContinuity, action.PriorityBand);
    }

    [Fact]
    public void ASecondViableMaleDescendantSuppliesTheSingleHeirBuffer()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var first = LineageChild(f, wife, 7, Sex.Male);
        var oneHeir = BuildLineageSnapshot(f, [wife, first]);
        Assert.Equal(1, oneHeir.ViableMaleLineDescendantCount);
        Assert.True(oneHeir.CanActivelyTryForChild);

        var second = LineageChild(f, wife, 4, Sex.Male);
        var buffered = BuildLineageSnapshot(f, [wife, first, second]);
        Assert.True(buffered.HasSecuredMaleLine);
        Assert.False(buffered.NeedsFamilyContinuity);
        Assert.False(buffered.CanActivelyTryForChild);
        Assert.Null(f.Strategy.ScoreAction(Candidate("reproduction.try_for_baby", f.Head), buffered));
    }

    [Fact]
    public void GrandsonsLivingElsewhereCountEvenWhenTheirFatherHasDied()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var deadSon = f.World.Person(27, alive: false);
        f.World.Family.SetParents(deadSon, f.Head, wife);
        var firstGrandson = f.World.Person(6);
        var secondGrandson = f.World.Person(3);
        f.World.Family.SetParents(firstGrandson, deadSon, null);
        f.World.Family.SetParents(secondGrandson, deadSon, null);

        var snapshot = BuildLineageSnapshot(f, [wife]);

        Assert.Empty(snapshot.LivingChildren);
        Assert.Equal(new[] { firstGrandson.Id, secondGrandson.Id },
            snapshot.LivingMaleLineDescendants.Select(person => person.Id));
        Assert.Equal(2, snapshot.ViableMaleLineDescendantCount);
        Assert.True(snapshot.HasSecuredMaleLine);
        Assert.False(snapshot.CanActivelyTryForChild);
        Assert.Equal(0, snapshot.ReproductiveUrgency);
    }

    [Fact]
    public void AdoptedHouseholdChildrenAndDaughtersSonsDoNotBecomeMaleLineHeirs()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var daughter = LineageChild(f, wife, 21, Sex.Female);
        var grandson = f.World.Person(2);
        grandson.Tags.Remove("lineage.male");
        f.World.Family.SetParents(grandson, null, daughter);
        var adopted = f.World.Person(5);
        adopted.Tags.Remove("lineage.male");
        adopted.Tags.Remove("family.bloodline");

        var snapshot = BuildLineageSnapshot(f, [wife, daughter, adopted]);

        Assert.Empty(snapshot.LivingMaleLineDescendants);
        Assert.Contains(snapshot.LivingBloodlineDescendants, person => person.Id == grandson.Id);
        Assert.DoesNotContain(snapshot.LivingBloodlineDescendants, person => person.Id == adopted.Id);
        Assert.True(snapshot.NeedsMaleLineContinuity);
        Assert.True(snapshot.CanActivelyTryForChild);
    }

    [Theory]
    [InlineData("infertile")]
    [InlineData("elderly")]
    [InlineData("seriously-ill")]
    public void AFragileLivingMaleDoesNotFalselySecureTheContinuationBuffer(string risk)
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var first = LineageChild(f, wife, 7, Sex.Male);
        var fragile = LineageChild(f, wife, risk == "elderly" ? 65 : 20, Sex.Male);
        var fertility = risk == "infertile" ? new Dictionary<Guid, int> { [fragile.Id] = 0 } : null;
        var health = risk == "seriously-ill" ? new Dictionary<Guid, double> { [fragile.Id] = 35 } : null;

        var snapshot = BuildLineageSnapshot(f, [wife, first, fragile], fertility: fertility, health: health);

        Assert.Equal(2, snapshot.LivingMaleLineDescendants.Count);
        Assert.Equal(1, snapshot.ViableMaleLineDescendantCount);
        Assert.True(snapshot.NeedsMaleLineContinuity);
    }

    [Fact]
    public void MinorHeirsAreAssessedProspectivelyBeforeAdultMarriageRulesApply()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var first = LineageChild(f, wife, 4, Sex.Male);
        var second = LineageChild(f, wife, 8, Sex.Male);
        first.Tags.Add("sexuality.homosexual");

        var snapshot = BuildLineageSnapshot(f, [wife, first, second]);

        Assert.Equal(2, snapshot.ViableMaleLineDescendantCount);
        Assert.True(snapshot.HasSecuredMaleLine);
    }

    [Theory]
    [InlineData(AutonomousFinancialState.Poor, false, 6)]
    [InlineData(AutonomousFinancialState.Critical, false, 6)]
    [InlineData(AutonomousFinancialState.Secure, true, 6)]
    [InlineData(AutonomousFinancialState.Secure, false, 2)]
    public void LineageUrgencyNeverOverridesPovertyStrainOrDependentCapacity(
        AutonomousFinancialState financialState, bool strained, int capacity)
    {
        Assert.False(AutonomousStrategyRules.CanActivelyTryForChild(
            viableDescendants: 0, financialState, strained,
            dependentChildren: 2, effectiveCapacity: capacity, hasReproductivePath: true));
    }

    [Fact]
    public void SupportedStepchildrenUseCapacityEvenThoughTheyAreNotTheHeadsDescendants()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var stepchild = f.World.Person(9);
        stepchild.Tags.Remove("family.bloodline");
        stepchild.Tags.Remove("lineage.male");

        var snapshot = BuildLineageSnapshot(f, [wife, stepchild], capacity: 1);

        Assert.Equal(0, snapshot.LivingChildCount);
        Assert.Equal(1, snapshot.DependentChildCount);
        Assert.True(snapshot.NeedsMaleLineContinuity);
        Assert.False(snapshot.CanActivelyTryForChild);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AnotherBirthCannotOvercrowdAHomeDespiteAmpleChildcareCapacity(int residentCapacity)
    {
        using var f = new Fixture();
        var wife = LineageWife(f);

        var snapshot = BuildLineageSnapshot(f, [wife], residentCapacity: residentCapacity);

        Assert.True(snapshot.NeedsMaleLineContinuity);
        Assert.False(snapshot.CanActivelyTryForChild);
    }

    [Fact]
    public void SeriousIllnessSuppressesAnotherBirthEvenWhenNoTreatmentIsAvailable()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var snapshot = BuildLineageSnapshot(f, [wife],
            health: new Dictionary<Guid, double> { [f.Head.Id] = 45 });

        Assert.True(snapshot.NeedsMaleLineContinuity);
        Assert.Null(f.Strategy.ScoreAction(Candidate("reproduction.try_for_baby", f.Head), snapshot));
    }

    [Fact]
    public void AChildMustFitTheProjectedBudgetAfterTheirLivingCostsAreAdded()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);

        var snapshot = BuildLineageSnapshot(f, [wife], projectedIncome: 2300m);

        Assert.Equal(AutonomousFinancialState.Secure, snapshot.FinancialState);
        Assert.True(snapshot.NeedsMaleLineContinuity);
        Assert.True(snapshot.HasRealisticReproductivePath);
        Assert.False(snapshot.CanActivelyTryForChild);
        Assert.True(BuildLineageSnapshot(f, [wife], projectedIncome: 2600m).CanActivelyTryForChild);
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

    [Fact]
    public void SeparatedSpousesHaveNoConceptionPathUntilTheyShareAHousehold()
    {
        using var f = new Fixture();
        var wife = LineageWife(f);
        var snapshot = BuildLineageSnapshot(f, [wife], separatedPartner: wife.Id);
        Assert.False(snapshot.HasRealisticReproductivePath);
        Assert.False(snapshot.CanActivelyTryForChild);
    }

    [Fact]
    public void InfertileUnmarriedManDoesNotSearchForAReproductiveSpouse()
    {
        using var f = new Fixture();
        var snapshot = BuildLineageSnapshot(f, [],
            fertility: new Dictionary<Guid, int> { [f.Head.Id] = 0 });
        Assert.False(snapshot.HasRealisticReproductivePath);
        Assert.False(new AutonomousReproductiveEligibility(f.World.Family)
            .CanSearchForReproductiveSpouse(f.Head, fertility: 0));
    }

    [Fact]
    public void MaleLineMarriageIsPrioritizedBeforeBloodlineMarriageAndDevelopment()
    {
        using var f = new Fixture();
        var son = f.World.Person(22);
        var daughter = f.World.Person(24, sex: Sex.Female);
        var scorer = new AutonomousFamilyContinuityScorer(f.Context, f.World.Family,
            LineageStats(), new AutonomousReproductiveEligibility(f.World.Family));

        var male = Assert.IsType<AutonomousActionCandidate>(scorer.Score(
            Candidate("relationship.marry_off_son", son), f.Snapshot));
        var blood = Assert.IsType<AutonomousActionCandidate>(scorer.Score(
            Candidate("relationship.marry_off_daughter", daughter), f.Snapshot));

        Assert.Equal(AutonomousPriorityBands.MaleLineContinuity, male.PriorityBand);
        Assert.Equal(AutonomousPriorityBands.BloodlineContinuity, blood.PriorityBand);
        Assert.Same(male, f.Strategy.ChooseAction([blood, male,
            Candidate("stats.improve_strength", f.Head, score: 150)]));
        Assert.Null(scorer.Score(Candidate("relationship.marry_off_son", son),
            f.Snapshot with { FinancialState = AutonomousFinancialState.Poor }));
    }

    [Fact]
    public void MarriageRepairProtectsAnUnsecuredMaleLineEvenWithSeveralLivingDaughters()
    {
        using var f = new Fixture();
        var snapshot = f.Snapshot with
        {
            LivingChildCount = 4,
            HasRealisticReproductivePath = true,
            NeedsMaleLineContinuity = true,
            MarriageSatisfaction = 24
        };
        var repair = Assert.IsType<AutonomousActionCandidate>(f.Strategy.ScoreAction(
            Candidate("relationship.repair_marriage", f.Head), snapshot));
        Assert.Equal(AutonomousPriorityBands.MaleLineContinuity, repair.PriorityBand);
        Assert.Null(f.Strategy.ScoreAction(Candidate("reproduction.try_for_baby", f.Head),
            snapshot with { CanActivelyTryForChild = true }));
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
}
