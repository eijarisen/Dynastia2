using System.Globalization;
using Dynastia.Contracts;
using Dynastia.Mechanics.Loans;
using Dynastia.Mechanics.Wellbeing;

namespace Dynastia.Core.Tests;

public sealed class TownLifeBatch4Tests
{
    [Fact]
    public void BankQualityTiersImprovePrincipalInterestAndDurationEnvelopes()
    {
        var rows = ReadCsv("data/TownLife/bank_offer_quality.csv");
        Assert.Equal(5, rows.Count);

        var means = rows
            .OrderBy(row => Int(row, "Tier"))
            .Select(row => new
            {
                Principal = Mean(row, "PrincipalMultiplierMin", "PrincipalMultiplierMax"),
                Interest = Mean(row, "InterestMultiplierMin", "InterestMultiplierMax"),
                Duration = Mean(row, "DurationMultiplierMin", "DurationMultiplierMax")
            })
            .ToArray();

        for (var index = 1; index < means.Length; index++)
        {
            Assert.True(means[index].Principal > means[index - 1].Principal);
            Assert.True(means[index].Interest < means[index - 1].Interest);
            Assert.True(means[index].Duration > means[index - 1].Duration);
        }
    }

    [Fact]
    public void InterestMultiplierChangesOfferRateWithoutChangingLegacyDefault()
    {
        var legacy = LoanTermsCalculator.Calculate(10000m, 10);
        var betterBank = LoanTermsCalculator.Calculate(10000m, 10, 0.80m);
        var worseBank = LoanTermsCalculator.Calculate(10000m, 10, 1.25m);

        Assert.Equal(1m, legacy.InterestMultiplier);
        Assert.Equal(legacy.TotalInterestRate * 0.80m, betterBank.TotalInterestRate);
        Assert.Equal(legacy.TotalInterestRate * 1.25m, worseBank.TotalInterestRate);
        Assert.Equal(legacy.Principal, betterBank.Principal);
        Assert.Equal(legacy.DurationYears, betterBank.DurationYears);
    }

    [Fact]
    public void MedicalQualityChangesTreatmentCostAndSuccessWithNinetyFivePercentCap()
    {
        Assert.Equal(3750m, MedicalTreatmentRules.AdjustCost(3000m, 1.25m));
        Assert.Equal(3000m, MedicalTreatmentRules.AdjustCost(3000m, 1.00m));
        Assert.Equal(2400m, MedicalTreatmentRules.AdjustCost(3000m, 0.80m));

        Assert.Equal(0.55, MedicalTreatmentRules.AdjustSuccessChance(0.50, 0.05), 6);
        Assert.Equal(0.95, MedicalTreatmentRules.AdjustSuccessChance(0.90, 0.20), 6);
    }

    [Fact]
    public void MedicalQualityDataMatchesTheFiveSpecifiedTiers()
    {
        var rows = ReadCsv("data/TownLife/medical_quality.csv")
            .OrderBy(row => Int(row, "Tier"))
            .ToArray();

        Assert.Equal(5, rows.Length);
        Assert.Equal(0.00, Double(rows[0], "TreatmentSuccessAdd"), 6);
        Assert.Equal(1.25m, Decimal(rows[0], "TreatmentCostMultiplier"));
        Assert.Equal(0.10, Double(rows[2], "TreatmentSuccessAdd"), 6);
        Assert.Equal(1.00m, Decimal(rows[2], "TreatmentCostMultiplier"));
        Assert.Equal(0.20, Double(rows[4], "TreatmentSuccessAdd"), 6);
        Assert.Equal(0.80m, Decimal(rows[4], "TreatmentCostMultiplier"));
    }

    [Fact]
    public void BorrowingRequiresLocalBankButExistingLoanServicingDoesNot()
    {
        var root = RepositoryRoot();
        var plugin = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Loans",
            "LoansPlugin.cs"));
        var service = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Loans",
            "StandardLoanService.cs"));
        var payments = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Loans",
            "LoanPaymentYearSystem.cs"));

        var autonomy = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Households",
            "AdvancedAutonomousHouseholdStrategy.cs"));

        Assert.Contains("HasLocalBank", plugin);
        Assert.Contains("if (!bankQuality.IsAvailable)", service);
        Assert.Contains("new List<LoanOfferInfo>(3)", service);
        Assert.Contains("index < 3", service);
        Assert.Contains("loans.GetOffers(snapshot.Head, isGivingLoan: false, 10000m)", autonomy);
        Assert.Contains("interestMultiplier", autonomy);
        Assert.DoesNotContain("ITownFacilityQualityService", payments);
        Assert.DoesNotContain("GetBankQuality", payments);
    }

    [Fact]
    public void HealthcareUsesLocalMedicalQualityWithoutChangingIllnessIncidence()
    {
        var root = RepositoryRoot();
        var wellbeing = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Wellbeing",
            "WellbeingPlugin.TreatmentActions.cs"));
        var healthYear = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Health",
            "HealthYearSystem.cs"));
        var incidence = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Health",
            "HealthIncidenceRules.cs"));

        Assert.Contains("currentMedical.IsAvailable", wellbeing);
        Assert.Contains("MedicalTreatmentRules.AdjustCost", wellbeing);
        Assert.Contains("MedicalTreatmentRules.AdjustSuccessChance", wellbeing);
        Assert.Contains("currentMedical.TreatmentSuccessAdd", wellbeing);
        Assert.DoesNotContain("ITownFacilityQualityService", healthYear);
        Assert.DoesNotContain("ITownFacilityQualityService", incidence);
    }

    [Fact]
    public void TownLifeWindowTextReportsBankAndMedicalEffects()
    {
        var town = new TownInfo("Test Town", "County", 19, 52, 50_000)
        {
            Id = "batch4-town",
            RegionId = "test-region",
            PolityId = "test-polity",
            PolityName = "Test Polity",
            IsDestinationAvailable = true
        };
        var institutions = new TownInstitutionSnapshot(
            town,
            1900,
            [
                new TownInstitutionInfo("bank", "Bank", 4, "Commercial Bank"),
                new TownInstitutionInfo("medical", "Medical Facility", 4, "General Hospital")
            ]);
        var snapshot = new TownLifeSnapshot(
            town,
            "Test Region",
            new LocationOpportunitySnapshot(town, "Test Region", [], [], string.Empty),
            institutions,
            new TownProsperitySnapshot(100, "Stable", 0, []),
            new BankOfferQualityInfo(4, "Commercial Bank", 1.05m, 1.30m, 0.85m, 0.98m, 1.10m, 1.30m),
            new MedicalQualityInfo(4, "General Hospital", 0.15, 0.90m));

        Assert.Equal("Loan offers: favorable", snapshot.FinanceCapacityText);
        Assert.Equal("Healthcare: good", snapshot.HealthcareCapacityText);
    }

    [Fact]
    public void VisitingPhysicianRemainsAvailableWithoutLocalMedicalInstitution()
    {
        var root = RepositoryRoot();
        var wellbeing = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Wellbeing",
            "WellbeingPlugin.TreatmentActions.cs"));

        Assert.Contains("IsVisitingPhysicianVariant", wellbeing);
        Assert.Contains("currentVisitingPhysician", wellbeing);
        Assert.Contains("|| currentMedical.IsAvailable", wellbeing);
        Assert.Contains("? HealCost", wellbeing);
    }

    [Fact]
    public void AlcoholDependenceFromDrinkingUsesTemperamentSpecificRiskAndReducedHealthDamage()
    {
        var root = RepositoryRoot();
        var wellbeing = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Wellbeing",
            "WellbeingPlugin.cs"));
        var recovery = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Wellbeing",
            "WellbeingPlugin.RecoveryActions.cs"));
        var health = File.ReadAllText(Path.Combine(
            root,
            "data",
            "Common",
            "health_conditions.json"));

        Assert.Contains("AlcoholismChanceFromDrinkingCalm =\n        0.25", wellbeing);
        Assert.Contains("AlcoholismChanceFromDrinkingReactive =\n        0.33", wellbeing);
        Assert.Contains("personality.choleric", wellbeing);
        Assert.Contains("personality.melancholic", wellbeing);
        Assert.Contains("GetAlcoholismChanceFromDrinking(actor)", recovery);
        Assert.Contains("25% chance", recovery);
        Assert.Contains("33% for Choleric/Melancholic", recovery);

        using var document = System.Text.Json.JsonDocument.Parse(health);
        var alcoholism = document.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "alcoholism");
        Assert.Equal(-5, alcoholism.GetProperty("healthImpact").GetInt32());
    }

    [Fact]
    public void Batch4DataFilesAreInstalled()
    {
        var root = RepositoryRoot();
        Assert.True(File.Exists(Path.Combine(root, "data", "TownLife", "bank_offer_quality.csv")));
        Assert.True(File.Exists(Path.Combine(root, "data", "TownLife", "medical_quality.csv")));
    }

    private static decimal Mean(
        IReadOnlyDictionary<string, string> row,
        string minimum,
        string maximum) =>
        (Decimal(row, minimum) + Decimal(row, maximum)) / 2m;

    private static int Int(IReadOnlyDictionary<string, string> row, string field) =>
        int.Parse(row[field], CultureInfo.InvariantCulture);

    private static decimal Decimal(IReadOnlyDictionary<string, string> row, string field) =>
        decimal.Parse(row[field], CultureInfo.InvariantCulture);

    private static double Double(IReadOnlyDictionary<string, string> row, string field) =>
        double.Parse(row[field], CultureInfo.InvariantCulture);

    private static IReadOnlyList<Dictionary<string, string>> ReadCsv(string relativePath)
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), relativePath));
        var headers = lines[0].TrimStart('\uFEFF').Split(',');
        return lines.Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(','))
            .Select(values => headers
                .Select((header, index) => (header, value: values[index]))
                .ToDictionary(item => item.header, item => item.value, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

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
}
