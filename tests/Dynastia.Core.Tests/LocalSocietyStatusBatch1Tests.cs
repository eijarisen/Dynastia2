using Dynastia.Contracts;
using Dynastia.Mechanics.Status;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyStatusBatch1Tests
{
    [Fact]
    public void StatusProfileBandsAndFarmWorkMatchSuppliedRules()
    {
        var rules = StatusRules.Load(new RepositoryDataService(RepositoryFiles.Root));

        Assert.Equal(15, rules.BaseRenown, 10);
        Assert.Equal(0, rules.BaseReputation, 10);

        var poor = rules.WealthDelta(0m);
        Assert.Equal(-10, poor.Renown, 10);
        Assert.Equal(-5, poor.Reputation, 10);

        var wealthy = rules.WealthDelta(2_000_000m);
        Assert.Equal(12, wealthy.Renown, 10);
        Assert.Equal(2, wealthy.Reputation, 10);

        Assert.Equal(-3, rules.ActiveFarmWorker.Renown, 10);
        Assert.Equal(0, rules.ActiveFarmWorker.Reputation, 10);
        Assert.Equal(10, rules.Career[5].Renown, 10);
        Assert.Equal(9, rules.CraftMastery[5].Renown, 10);
    }

    [Fact]
    public void StatusInheritanceLocalityAndHouseholdRulesAreImplemented()
    {
        var source = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Status", "StandardStatusService.cs");

        Assert.Contains("Math.Min(\n                _rules.InheritedRenownCap", source);
        Assert.Contains("_rules.InheritedReputationFraction", source);
        Assert.Contains("person.Age >= 18", source);
        Assert.Contains("statuses.Average(status => status.Renown)", source);
        Assert.Contains("_rules.MoveMultiplier", source);
        Assert.Contains("_rules.YearsToFullRecognition", source);
        Assert.Contains("_farming.IsWorkingFarmWorker(person, person)", source);
    }

    [Fact]
    public void StatusNetWorthIncludesOutstandingPrivateLoanReceivables()
    {
        var source = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Status", "StandardStatusService.cs");

        Assert.Contains("_loans.GetLoansGiven(person).Sum(item => item.RemainingAmount)", source);
        Assert.Contains("+ receivables - debt", source);
    }

    [Fact]
    public void PublicCrimeAffairDivorceAndHistoricalEventsFeedPersistentStatus()
    {
        var source = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Status", "StatusPlugin.cs");
        Assert.Contains("justice.crime", source);
        Assert.DoesNotContain("justice.crime_uncaught", source);
        Assert.Contains("relationship.affair", RepositoryFiles.ReadText("data", "LocalSociety", "status_event_effects.csv"));
        Assert.Contains("relationship.divorce", RepositoryFiles.ReadText("data", "LocalSociety", "status_event_effects.csv"));
        Assert.Contains("historical.household_impact", source);
        Assert.Contains("historicalId", source);
        Assert.Contains("statusRenownDelta", source);
        Assert.Contains("statusReputationDelta", source);
    }

    [Fact]
    public void CandidateAndCareerUseTheSameStatusServiceWithCappedBonuses()
    {
        var candidates = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Relationships", "StandardPartnerSearchService.cs");
        var opportunities = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Career", "StandardCareerService.Opportunities.cs");
        var promotion = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Career", "CareerAdvancementYearSystem.cs");
        var app = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "MainWindow.axaml");

        Assert.Contains("GetCandidateStatus", candidates);
        Assert.Contains("StatusCandidateProfile", candidates);
        Assert.Contains("GetCareerApplicationBonus", opportunities);
        Assert.Contains("GetCareerPromotionBonus", promotion);
        Assert.Contains("SelectedFamily.Renown", app);
        Assert.Contains("SelectedFamily.Reputation", app);

        var rules = StatusRules.Load(new RepositoryDataService(RepositoryFiles.Root));
        Assert.Equal(0.025, rules.ApplicationMaximum, 10);
        Assert.Equal(0.045, rules.PromotionMaximum, 10);
    }

    private sealed class RepositoryDataService(string root) : IGameDataService
    {
        public string ReadText(string relativePath) =>
            File.ReadAllText(Path.Combine(root, "data", relativePath.Replace('/', Path.DirectorySeparatorChar)));

        public IReadOnlyList<string> GetStringList(string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(string relativePath) =>
            throw new NotSupportedException();
    }
}
