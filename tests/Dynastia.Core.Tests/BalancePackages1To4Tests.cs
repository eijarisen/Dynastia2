using Dynastia.Contracts;
using Dynastia.Core.Entities;
using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.Reproduction;
using System.Reflection;

namespace Dynastia.Core.Tests;

public sealed class BalancePackages1To4Tests
{
    [Theory]
    [InlineData(0, 0, 1000, 1000, 0, 1000)]
    [InlineData(0, 60, 1000, 1000, 60, 940)]
    [InlineData(0, 1000, 1000, 1000, 1000, 0)]
    [InlineData(200, 800, 1000, 1000, 1000, 0)]
    [InlineData(-200, 1200, 1000, 1000, 1000, 0)]
    public void BasicNeedsFundingUsesResourcesAfterExistingDebt(
        decimal startingWealth,
        decimal ordinaryIncome,
        decimal ordinaryExpenses,
        decimal expectedRequired,
        decimal expectedFunded,
        decimal expectedShortfall)
    {
        var result = EconomyBalanceRules.CalculateBasicNeedsFunding(
            startingWealth,
            ordinaryIncome,
            ordinaryExpenses);

        Assert.Equal(expectedRequired, result.Required);
        Assert.Equal(expectedFunded, result.Funded);
        Assert.Equal(expectedShortfall, result.Shortfall);
    }

    [Fact]
    public void SharedProductiveEffortCombinesCapacityAndExistingRecoverWithoutMutation()
    {
        var person = new Person("Jan", "Test", 40);
        person.Tags.Add("modifier.salary.recover.25");
        person.Tags.Add("state.alive");
        var before = person.Tags.All.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var service = new FixedWorkCapacityService(0.50, canWork: true);

        var effort = AnnualProductiveEffortRules.Get(person, service);

        Assert.Equal(0.50, effort.WorkCapacityMultiplier, 6);
        Assert.Equal(25m, effort.RecoverReductionPercent);
        Assert.Equal(0.375, effort.OutputMultiplier, 6);
        Assert.True(effort.CanProduce);
        Assert.Equal(375m, effort.Apply(1000m));
        Assert.Equal(before, person.Tags.All.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Equal(1, service.CallCount);
    }

    [Theory]
    [InlineData("modifier.salary.recover.10", 0.90)]
    [InlineData("modifier.salary.recover.50", 0.50)]
    [InlineData("modifier.salary.recover.75", 0.50)]
    public void SharedProductiveEffortUsesAlreadySelectedRecoverReduction(
        string recoverTag,
        double expectedMultiplier)
    {
        var person = new Person("Jan", "Test", 40);
        person.Tags.Add(recoverTag);
        var service = new FixedWorkCapacityService(1.0, canWork: true);

        var effort = AnnualProductiveEffortRules.Get(person, service);

        Assert.Equal(expectedMultiplier, effort.OutputMultiplier, 6);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public void SharedProductiveEffortBlocksProductionAtZeroWorkCapacity()
    {
        var person = new Person("Jan", "Test", 40);
        var effort = AnnualProductiveEffortRules.Get(
            person,
            new FixedWorkCapacityService(0, canWork: false));

        Assert.False(effort.CanProduce);
        Assert.Equal(0m, effort.Apply(1000m));
    }

    [Fact]
    public void PovertyConsumersUseBasicNeedsSignalAndLateLoanSettlementIsTwoPass()
    {
        var sources = new[]
        {
            RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Households", "StandardHouseholdService.cs"),
            RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Health", "StandardStressService.cs"),
            RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Childhood", "ChildHappinessYearSystem.cs"),
            RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Relationships", "MarriageSatisfactionYearSystem.cs"),
            RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Thoughts", "HouseholdThoughtProvider.cs"),
            RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Justice", "CrimeYearSystem.cs"),
            RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Personality", "MoralsDeteriorationYearSystem.cs"),
            RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.RareEvents", "RareEventYearSystem.Helpers.cs")
        };
        foreach (var source in sources)
            Assert.Contains("HasUnfundedBasicNeeds", source);

        var loans = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Loans", "LoanPaymentYearSystem.cs");
        var economy = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Economy", "StandardEconomyService.cs");
        Assert.Contains("ApplyAnnualFinanceReceipt", economy);
        Assert.Contains("var duePayments =", loans);
        Assert.Contains("ApplyAnnualFinanceReceipt", loans);
        Assert.Contains("ChangeWealthAllowDebt", loans);
        Assert.Contains("RecordRealizedExpense", loans);
    }

    [Fact]
    public void CivicProfilesFreezeAtSharedTechnologyHorizonAndValidateIncumbents()
    {
        var civic = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Community", "CivicOfficeService.cs");

        Assert.Contains("GameCalendarConfiguration.TechnologyFreezeYear", civic);
        Assert.DoesNotContain("Math.Min(year, 2026)", civic);
        Assert.Contains("IsActiveSimulatedIncumbent", civic);
        Assert.Contains("state.imprisoned", civic);
        Assert.Contains("SimulationState.IsInactive", civic);
        Assert.Contains("SimulationState.IsExternallyResident", civic);
        Assert.Contains("_economy.GetHouseholdId(person)", civic);
        Assert.Contains("GetResidenceTown(person)", civic);
        Assert.Contains("GetOffice(person)?.AnnualSalary ?? 0m", civic);
    }

    [Fact]
    public void MaritalConceptionEligibilityIsSymmetricAndRequiresEstablishedSharedHousehold()
    {
        var father = new Person("Jan", "Test", 30);
        var mother = new Person("Anna", "Test", 29);
        father.Tags.Add("state.alive");
        mother.Tags.Add("state.alive");
        var family = new EligibilityFamilyService(father, mother, startYear: 1900);
        var sharedHousehold = Guid.NewGuid();
        var economy = CreateEconomy(
            (father.Id, sharedHousehold),
            (mother.Id, sharedHousehold));

        Assert.True(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1900));

        father.Tags.Add("state.imprisoned");
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        father.Tags.Remove("state.imprisoned");

        mother.Tags.Add("state.imprisoned");
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        mother.Tags.Remove("state.imprisoned");

        father.Tags.Add("vocation.religious.active");
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        father.Tags.Remove("vocation.religious.active");

        mother.Tags.Add("vocation.religious.active");
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        mother.Tags.Remove("vocation.religious.active");

        father.Tags.Add(SimulationState.ExternalResidenceTag);
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        father.Tags.Remove(SimulationState.ExternalResidenceTag);

        mother.Tags.Add(SimulationState.ExternalResidenceTag);
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        mother.Tags.Remove(SimulationState.ExternalResidenceTag);

        father.Tags.Add(SimulationState.PeripheralInactiveTag);
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        father.Tags.Remove(SimulationState.PeripheralInactiveTag);

        mother.Tags.Add(SimulationState.PeripheralInactiveTag);
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        mother.Tags.Remove(SimulationState.PeripheralInactiveTag);

        father.Tags.Remove("state.alive");
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
        father.Tags.Add("state.alive");

        var splitEconomy = CreateEconomy(
            (father.Id, Guid.NewGuid()),
            (mother.Id, Guid.NewGuid()));
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, splitEconomy, father, mother, 1901));

        mother.Age = 46;
        Assert.False(ReproductionEligibilityRules.CanAttemptMaritalConception(
            family, economy, father, mother, 1901));
    }

    [Fact]
    public void ActiveAndPassiveMaritalConceptionUseTheSameSymmetricRule()
    {
        var rule = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Reproduction", "ReproductionEligibilityRules.cs");
        var plugin = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Reproduction", "ReproductionPlugin.cs");
        var system = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Reproduction", "ReproductionYearSystem.cs");
        var nonmarital = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Reproduction", "NonmaritalBirthYearSystem.cs");

        Assert.Contains("ReproductionEligibilityRules.CanAttemptMaritalConception", plugin);
        Assert.Contains("ReproductionEligibilityRules.CanAttemptMaritalConception", system);
        Assert.Contains("father.Tags.Has(\"state.imprisoned\")", rule);
        Assert.Contains("mother.Tags.Has(\"state.imprisoned\")", rule);
        Assert.Contains("vocation.religious.active", rule);
        Assert.Contains("SimulationState.IsInactive(father)", rule);
        Assert.Contains("SimulationState.IsInactive(mother)", rule);
        Assert.Contains("SimulationState.IsExternallyResident(father)", rule);
        Assert.Contains("SimulationState.IsExternallyResident(mother)", rule);
        Assert.Contains("activeMarriage.StartYear == year", rule);
        Assert.Contains("economy.GetHouseholdId(father)", rule);
        Assert.Contains("economy.GetHouseholdId(mother)", rule);
        Assert.Contains("SimulationState.IsInactive(person)", nonmarital);
        Assert.Contains("SimulationState.IsExternallyResident(person)", nonmarital);
    }

    [Fact]
    public void ArtisticCraftAndFarmOutputUseSharedProductiveEffort()
    {
        var art = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Heirlooms", "ArtisticWorkYearSystem.cs");
        var crafts = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Crafts", "StandardCraftService.cs");
        var farming = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Farming", "StandardFarmingService.cs");
        var heirloomManifest = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Heirlooms", "plugin.json");

        Assert.Contains("AnnualProductiveEffortRules.Get", art);
        Assert.Contains("AnnualProductiveEffortRules.Get", crafts);
        Assert.Contains("AnnualProductiveEffortRules.Get", farming);
        Assert.Contains("productiveEffort.OutputMultiplier", art);
        Assert.Contains("if (!productiveEffort.CanProduce)", art);
        Assert.Contains("state.imprisoned", art);
        Assert.Contains("SimulationState.IsInactive", art);
        Assert.Contains("SimulationState.IsExternallyResident", art);
        var effortIndex = art.IndexOf("AnnualProductiveEffortRules.Get", StringComparison.Ordinal);
        var lastProductionIndex = art.IndexOf("LastProductionYearByCraft[craft.Id]", StringComparison.Ordinal);
        Assert.True(effortIndex >= 0 && lastProductionIndex > effortIndex);
        Assert.Contains("\"dynastia.health\"", heirloomManifest);
    }


    private static IEconomyService CreateEconomy(
        params (Guid PersonId, Guid HouseholdId)[] memberships)
    {
        var service = DispatchProxy.Create<IEconomyService, HouseholdEconomyProxy>();
        var proxy = (HouseholdEconomyProxy)(object)service;
        foreach (var membership in memberships)
            proxy.Households[membership.PersonId] = membership.HouseholdId;
        return service;
    }

    public class HouseholdEconomyProxy : DispatchProxy
    {
        public Dictionary<Guid, Guid> Households { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IEconomyService.GetHouseholdId)
                && args is { Length: > 0 }
                && args[0] is IPerson person)
            {
                return Households.TryGetValue(person.Id, out var householdId)
                    ? householdId
                    : null;
            }

            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
                return null;

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    private sealed class EligibilityFamilyService : IFamilyService
    {
        private readonly IPerson _father;
        private readonly IPerson _mother;
        private readonly int _startYear;

        public EligibilityFamilyService(IPerson father, IPerson mother, int startYear)
        {
            _father = father;
            _mother = mother;
            _startYear = startYear;
        }

        public void InitializePerson(IPerson person, Sex sex, int? generation = null) { }
        public Sex GetSex(IPerson person) => person.Id == _father.Id ? Sex.Male : Sex.Female;
        public int? GetGeneration(IPerson person) => 1;
        public IPerson? GetFather(IPerson person) => null;
        public IPerson? GetMother(IPerson person) => null;
        public IPerson? GetSpouse(IPerson person) =>
            person.Id == _father.Id ? _mother : person.Id == _mother.Id ? _father : null;
        public IReadOnlyList<IPerson> GetChildren(IPerson person) => [];
        public void SetParents(IPerson child, IPerson? father, IPerson? mother) { }
        public void SetSpouses(IPerson first, IPerson second, int startYear) { }
        public void EndRelationship(IPerson first, IPerson second, int endYear, string endReason, bool clearFirst = true, bool clearSecond = true) { }
        public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(IPerson person) =>
            person.Id == _father.Id
                ? [new RelationshipHistoryInfo(_mother.Id, _startYear, null, null)]
                : person.Id == _mother.Id
                    ? [new RelationshipHistoryInfo(_father.Id, _startYear, null, null)]
                    : [];
        public void SetGeneratedFamilyBackground(IPerson person, GeneratedFamilyBackgroundInfo background) { }
        public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(IPerson person) => null;
        public string FormatSurname(string surname, Sex sex) => surname;
        public string GetDisplayName(IPerson person) => $"{person.Name} {person.Surname}";
        public bool IsBloodline(IPerson person) => true;
        public bool IsMaleLineage(IPerson person) => person.Id == _father.Id;
    }

    private sealed class FixedWorkCapacityService(double multiplier, bool canWork) : IWorkCapacityService
    {
        public int CallCount { get; private set; }

        public WorkCapacitySnapshot GetWorkCapacity(IPerson person)
        {
            CallCount++;
            return new WorkCapacitySnapshot(multiplier, canWork);
        }
    }
}
