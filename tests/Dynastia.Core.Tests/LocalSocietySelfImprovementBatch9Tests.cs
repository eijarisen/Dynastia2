using System.Globalization;
using System.Text.Json;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietySelfImprovementBatch9Tests
{
    [Fact]
    public void SelfImprovementSelectorIsRetiredFromMainActionsAndInstructions()
    {
        var actions = Read(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Actions.cs");
        var mainWindow = Read(
            "src", "Dynastia.App", "Views", "MainWindow.axaml.cs");
        var retiredSelector = Read(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.SelfImprovement.cs");
        var instructions = Read(
            "src", "Dynastia.App", "Views", "InstructionsWindow.axaml");

        Assert.DoesNotContain("SelfImprovementUiActionId", actions);
        Assert.DoesNotContain("ui.self_improvement", mainWindow);
        Assert.DoesNotContain("Paid Self Improvement", instructions);
        Assert.DoesNotContain("ui.self_improvement", retiredSelector);
        Assert.DoesNotContain("CreateSelfImprovementPresentationAction", retiredSelector);
    }

    [Fact]
    public void ReligiousStudyUsesChurchTierSuccessFormulaAndChurchRouting()
    {
        using var document = JsonDocument.Parse(
            Read("data", "LocalSociety", "religious_study_church_rules.json"));
        var rules = document.RootElement;

        Assert.Equal(3000m, rules.GetProperty("baseCost").GetDecimal());
        for (var tier = 1; tier <= 5; tier++)
        {
            var expected = 0.40 + 0.05 * tier;
            var actual = rules
                .GetProperty("tierChances")
                .GetProperty(tier.ToString(CultureInfo.InvariantCulture))
                .GetDouble();
            Assert.Equal(expected, actual, 10);
        }

        var personality = Read(
            "plugins", "Dynastia.Mechanics.Personality", "PersonalityPlugin.cs");
        var churchRules = Read(
            "plugins", "Dynastia.Mechanics.Personality", "ReligiousStudyChurchRules.cs");
        var townLife = Read(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");

        Assert.Contains("ResolveChurchTier(", personality);
        Assert.Contains("rules.GetSuccessChance(churchTier)", personality);
        Assert.DoesNotContain("random.NextDouble() >= 0.50", personality);
        Assert.Contains("0.40 + 0.05 * tier", churchRules);
        Assert.Contains("\"personality.religious_study\" => TownAffairsTab.Church", townLife);
        Assert.Contains("AddChurchAction(\"personality.religious_study\"", townLife);
    }

    [Fact]
    public void MedicalImprovementRulesUseSpecifiedFacilityMinimums()
    {
        var rows = ParseCsv(
            Read("data", "LocalSociety", "medical_stat_improvement_rules.csv"));

        Assert.Equal(6, rows.Count);
        Assert.Equal("1", Row(rows, "stats.improve_strength")["MinimumMedicalTier"]);
        Assert.Equal("2", Row(rows, "stats.improve_intellect")["MinimumMedicalTier"]);
        Assert.Equal("2", Row(rows, "stats.improve_immunity")["MinimumMedicalTier"]);
        Assert.Equal("2", Row(rows, "stats.improve_appeal")["MinimumMedicalTier"]);
        Assert.Equal("3", Row(rows, "stats.improve_longevity")["MinimumMedicalTier"]);
        Assert.Equal("3", Row(rows, "stats.improve_fertility")["MinimumMedicalTier"]);
        Assert.All(rows, row => Assert.Equal("20000", row["BaseCost"]));
    }

    [Fact]
    public void MedicalImprovementCostScalesWithLocalMedicalQuality()
    {
        var improvementRows = ParseCsv(
            Read("data", "LocalSociety", "medical_stat_improvement_rules.csv"));
        var baseCost = decimal.Parse(
            Row(improvementRows, "stats.improve_strength")["BaseCost"],
            CultureInfo.InvariantCulture);

        var medicalRows = ParseCsv(
            Read("data", "TownLife", "medical_quality.csv"));

        Assert.Equal(25000m, Adjust(baseCost, MedicalMultiplier(medicalRows, 1)));
        Assert.Equal(20000m, Adjust(baseCost, MedicalMultiplier(medicalRows, 3)));
        Assert.Equal(16000m, Adjust(baseCost, MedicalMultiplier(medicalRows, 5)));

        var plugin = Read(
            "plugins", "Dynastia.Mechanics.StatImprovements", "StatImprovementsPlugin.cs");
        Assert.Contains("medical.Tier < definition.MinimumMedicalTier", plugin);
        Assert.Contains("StatImprovementRules.CalculateCost", plugin);
        Assert.Contains("locations.GetLocation(target).HomeTown", plugin);
        Assert.DoesNotContain("VisitingPhysician", plugin);
    }

    [Fact]
    public void HealthTabContainsTreatmentTherapyAndMedicalImprovementSections()
    {
        var townLife = Read(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var hub = Read(
            "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var window = Read(
            "src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        Assert.Contains("wellbeing.heal_relative", townLife);
        Assert.Contains("wellbeing.therapy", townLife);
        Assert.Contains("stats.improve_strength", townLife);
        Assert.Contains("stats.improve_fertility", townLife);
        Assert.Contains("MedicalImprovementActions", hub);
        Assert.Contains("TreatmentHealthActions", hub);
        Assert.Contains("TherapyHealthActions", hub);
        Assert.DoesNotContain("Text=\"Current Health / Conditions\"", window);
        Assert.Contains("Text=\"Treatment / Heal\"", window);
        Assert.Contains("IsVisible=\"{Binding HasTherapyHealthActions}\"", window);
        Assert.Contains("Text=\"Therapy\"", window);
        Assert.Contains("IsVisible=\"{Binding HasMedicalImprovementActions}\"", window);
        Assert.Contains("Text=\"Medical Improvements\"", window);
        Assert.DoesNotContain("TreatmentCostMultiplier:0.##", hub);
    }

    [Fact]
    public void StatImprovementStillRequiresHistoricalVariantAsAdditionalRule()
    {
        var plugin = Read(
            "plugins", "Dynastia.Mechanics.StatImprovements", "StatImprovementsPlugin.cs");

        Assert.Contains("historical.GetVariant(", plugin);
        Assert.Contains("ActionCompatibilityParameters.IsRestoredQueuedAction", plugin);
        Assert.Contains("stats.TryIncreaseAcquiredStat", plugin);
        Assert.Contains("medical.Tier < definition.MinimumMedicalTier", plugin);
    }

    private static decimal Adjust(decimal baseCost, decimal multiplier) =>
        Math.Round(baseCost * multiplier, 0, MidpointRounding.AwayFromZero);

    private static decimal MedicalMultiplier(
        IReadOnlyList<Dictionary<string, string>> rows,
        int tier) =>
        decimal.Parse(
            rows.Single(row => row["Tier"] == tier.ToString(CultureInfo.InvariantCulture))["TreatmentCostMultiplier"],
            CultureInfo.InvariantCulture);

    private static Dictionary<string, string> Row(
        IReadOnlyList<Dictionary<string, string>> rows,
        string actionId) =>
        rows.Single(row => row["ActionId"] == actionId);

    private static IReadOnlyList<Dictionary<string, string>> ParseCsv(string text)
    {
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        var headers = lines[0]
            .TrimStart('\uFEFF')
            .Split(',')
            .Select(value => value.Trim())
            .ToArray();

        return lines
            .Skip(1)
            .Select(line =>
            {
                var values = line.Split(',').Select(value => value.Trim()).ToArray();
                return headers
                    .Select((header, index) => new { header, value = values[index] })
                    .ToDictionary(item => item.header, item => item.value, StringComparer.OrdinalIgnoreCase);
            })
            .ToArray();
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
}
