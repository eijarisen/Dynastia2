using Dynastia.Contracts;
using Dynastia.Mechanics.Justice;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyCriminalOccupationBatch6Tests
{
    [Fact]
    public void MasteryUsesActiveHeistYearsAndDetectionFallsAtExactThresholds()
    {
        var catalog = CriminalOccupationCatalog.Load(
            new RepositoryDataService(RepositoryRoot()));

        var expected = new[]
        {
            (Years: 0, Level: 1, Name: "Novice", Detection: 0.45),
            (Years: 2, Level: 2, Name: "Apprentice", Detection: 0.34),
            (Years: 5, Level: 3, Name: "Adept", Detection: 0.24),
            (Years: 9, Level: 4, Name: "Expert", Detection: 0.15),
            (Years: 15, Level: 5, Name: "Master", Detection: 0.08)
        };

        foreach (var item in expected)
        {
            var resolved = catalog.ResolveMastery(item.Years);
            Assert.Equal(item.Level, resolved.Level);
            Assert.Equal(item.Name, resolved.DisplayName);
            Assert.Equal(item.Detection, resolved.BaseDetectionChance, 10);
        }

        Assert.Equal(1, catalog.ResolveMastery(1).Level);
        Assert.Equal(2, catalog.ResolveMastery(4).Level);
        Assert.Equal(3, catalog.ResolveMastery(8).Level);
        Assert.Equal(4, catalog.ResolveMastery(14).Level);
    }

    [Fact]
    public void ArchetypeUsesHighestStatsAndExactTripleFiveIsMastermind()
    {
        var catalog = CriminalOccupationCatalog.Load(
            new RepositoryDataService(RepositoryRoot()));

        Assert.Equal("appeal", catalog.ResolveArchetype(5, 3, 2).ArchetypeId);
        Assert.Equal("strength", catalog.ResolveArchetype(2, 5, 3).ArchetypeId);
        Assert.Equal("intellect", catalog.ResolveArchetype(2, 3, 5).ArchetypeId);
        Assert.Equal("appeal_strength", catalog.ResolveArchetype(4, 4, 2).ArchetypeId);
        Assert.Equal("appeal_intellect", catalog.ResolveArchetype(4, 2, 4).ArchetypeId);
        Assert.Equal("strength_intellect", catalog.ResolveArchetype(2, 4, 4).ArchetypeId);
        Assert.Equal("balanced", catalog.ResolveArchetype(4, 4, 4).ArchetypeId);

        var mastermind = catalog.ResolveArchetype(5, 5, 5);
        Assert.Equal("mastermind", mastermind.ArchetypeId);
        Assert.Equal("Criminal Mastermind", mastermind.DisplayName);
        Assert.Equal(1.25m, mastermind.IncomeMultiplier);
    }

    [Fact]
    public void StartIsEvilOnlyOneTimeAndFirstActionPerformsOnlyOneHeist()
    {
        var actions = Read(
            "plugins", "Dynastia.Mechanics.Justice", "JusticePlugin.CriminalOccupation.cs");
        var service = Read(
            "plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationService.cs");
        var state = Read(
            "plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationComponent.cs");

        Assert.Contains("Id = rules.StartActionId", actions);
        Assert.Contains("RequiredMorals", actions);
        Assert.Contains("crime.HasStartedLifeOfCrime(context.Target)", actions);
        Assert.Contains("crime.PerformFirstHeist(context.Target)", actions);
        Assert.Contains("HasStartedLifeOfCrime = true", service);
        Assert.Contains("component.LastHeistYear == _gameState.Year", service);
        Assert.Contains("component.LastHeistYear = _gameState.Year", service);
        Assert.Contains("HasStartedLifeOfCrime", state);
    }

    [Fact]
    public void CrimeIncomeUsesLongTailFormulaAndMastermindSentenceReduction()
    {
        var rules = CriminalOccupationCatalog.Load(
            new RepositoryDataService(RepositoryRoot())).Rules;
        var service = Read(
            "plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationService.cs");

        Assert.Equal(1000m, rules.Heist.BaseIncome);
        Assert.Equal(0, rules.Heist.IncomeRollMinimum);
        Assert.Equal(94, rules.Heist.IncomeRollMaximumInclusive);
        Assert.Equal(0.8m, rules.Heist.MastermindSentenceMultiplier);

        Assert.Contains("100m / denominator", service);
        Assert.Contains("Math.Clamp(1m + 0.05m * (average - 3m), 0.90m, 1.10m)", service);
        Assert.Contains("archetype.IncomeMultiplier", service);
        Assert.Contains("Math.Floor(proceeds / 2m)", service);
        Assert.Contains("originalSentence * sentenceMultiplier", service);
    }

    [Fact]
    public void CriminalOccupationIsExclusiveAndPrisonPausesIncomeAndMastery()
    {
        var crime = Read(
            "plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationService.cs");
        var randomCrime = Read(
            "plugins", "Dynastia.Mechanics.Justice", "CrimeYearSystem.cs");
        var career = Read(
            "plugins", "Dynastia.Mechanics.Career", "StandardCareerService.Opportunities.cs");
        var crafts = Read(
            "plugins", "Dynastia.Mechanics.Crafts", "StandardCraftService.cs");
        var farming = Read(
            "plugins", "Dynastia.Mechanics.Farming", "StandardFarmingService.cs");
        var retirement = Read(
            "plugins", "Dynastia.Mechanics.Career", "CareerRetirementYearSystem.cs");
        var civic = Read(
            "plugins", "Dynastia.Mechanics.Community", "CivicOfficeService.cs");

        Assert.Contains("_career.AssignCareer(person, null, 0", crime);
        Assert.Contains("_craftResolver()?.EndOccupation(person, \"life of crime\")", crime);
        Assert.Contains("_justice.IsImprisoned(person)", crime);
        Assert.Contains("component.ActiveHeistYears++", crime);
        Assert.Contains("occupation.criminal", randomCrime);
        Assert.Contains("EndLifeOfCrime(person, \"formal employment\")", career);
        Assert.Contains("EndLifeOfCrime(person, \"craft occupation\")", crafts);
        Assert.Contains("occupation.criminal", farming);
        Assert.Contains("occupation.criminal", retirement);
        Assert.Contains("EndLifeOfCrime(person, \"civic office\")", civic);
    }

    [Fact]
    public void JusticePluginRegistersPersistentServiceIncomeProviderAndAnnualHeistSystem()
    {
        var plugin = Read(
            "plugins", "Dynastia.Mechanics.Justice", "JusticePlugin.cs");
        var system = Read(
            "plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationYearSystem.cs");

        Assert.Contains("context.AddService<ICriminalOccupationService>", plugin);
        Assert.Contains("income.Register(criminalOccupation)", plugin);
        Assert.Contains("criminalOccupation.ReconcileAll(people)", plugin);
        Assert.Contains("new CriminalOccupationYearSystem(criminalOccupation)", plugin);
        Assert.Contains("Before => [\"justice.crime\"]", system);
        Assert.Contains("After => [\"actions.queued.life_events\"]", system);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dynastia.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class RepositoryDataService(string root) : IGameDataService
    {
        public string ReadText(string relativePath) =>
            File.ReadAllText(Path.Combine(
                root,
                "data",
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

        public IReadOnlyList<string> GetStringList(string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(string relativePath) =>
            throw new NotSupportedException();
    }
}
