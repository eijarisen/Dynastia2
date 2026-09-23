using System.Globalization;
using System.Text.Json;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyReligiousCallingsBatch12Tests
{
    [Fact]
    public void CallingRulesUseOneAgeEighteenRollAndFlatAgeNineteenDeparture()
    {
        using var document = JsonDocument.Parse(
            RepositoryFiles.ReadText("data", "LocalSociety", "religious_calling_rules.json"));
        var root = document.RootElement;

        Assert.Equal(18, root.GetProperty("callingAge").GetInt32());
        Assert.Equal(0.005, root.GetProperty("callingChance").GetDouble(), 10);
        Assert.True(root.GetProperty("requiresUnmarried").GetBoolean());
        Assert.False(root.GetProperty("moralsAffectChance").GetBoolean());
        Assert.False(root.GetProperty("temperamentAffectsChance").GetBoolean());
        Assert.False(root.GetProperty("sexualityAffectsChance").GetBoolean());

        var departure = root.GetProperty("selfDeparture");
        Assert.Equal(19, departure.GetProperty("minimumAge").GetInt32());
        Assert.Equal(0.005, departure.GetProperty("annualChance").GetDouble(), 10);
        Assert.True(departure.GetProperty("onDeparture")
            .GetProperty("neverRollCallingAgain").GetBoolean());

        var system = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.ReligiousVocation", "ReligiousVocationYearSystem.cs");
        Assert.Contains("person.Age != _rules.CallingAge", system);
        Assert.Contains("person.Tags.Add(_rules.CheckedTag);", system);
        Assert.Contains("_family.GetSpouse(person) is not null", system);
        Assert.Contains("_random.NextDouble() >= _rules.CallingChance", system);
        Assert.Contains("person.Age < _rules.DepartureMinimumAge", system);
        Assert.Contains("person.Tags.Add(_rules.FormerTag);", system);
    }

    [Fact]
    public void CallingOnlyCareersUseSpecifiedSexSalaryTitlesAndChurchRequirement()
    {
        var careers = ParseCsv(RepositoryFiles.ReadText("data", "Career", "careers.csv"));
        Assert.Equal(76, careers.Count);

        var priest = Row(careers, "priest_vocation");
        var nun = Row(careers, "nun_vocation");
        Assert.Equal("300", priest["BaseSalary"]);
        Assert.Equal("220", nun["BaseSalary"]);
        Assert.Equal("Bishop", priest["Level5Title"]);
        Assert.Equal("Abbess", nun["Level5Title"]);
        Assert.Equal("religion", priest["CareerFamily"]);
        Assert.Equal("religion", nun["CareerFamily"]);

        var requirements = ParseCsv(
            RepositoryFiles.ReadText("data", "TownLife", "career_institution_requirements.csv"));
        Assert.Equal("church", Row(requirements, "priest_vocation", "CareerId")["InstitutionId"]);
        Assert.Equal("1", Row(requirements, "priest_vocation", "CareerId")["MinimumTier"]);
        Assert.Equal("church", Row(requirements, "nun_vocation", "CareerId")["InstitutionId"]);
        Assert.Equal("1", Row(requirements, "nun_vocation", "CareerId")["MinimumTier"]);

        var system = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.ReligiousVocation", "ReligiousVocationYearSystem.cs");
        Assert.Contains("sex == Sex.Female", system);
        Assert.Contains("_institutions.Resolve(town, gameState.Year).GetTier(\"church\") < 1", system);
        Assert.Contains("_career.AssignCareer(", system);
        Assert.Contains("careerId,", system);
        Assert.Contains("1,", system);
    }

    [Fact]
    public void CallingOnlyCareersAreExcludedFromOrdinaryEmploymentAndRelocationReplacement()
    {
        var service = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "StandardCareerService.cs");
        var employment = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "StandardCareerService.Employment.cs");
        var opportunities = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "StandardCareerService.Opportunities.cs");

        Assert.Contains("PriestVocationCareerId = \"priest_vocation\"", service);
        Assert.Contains("NunVocationCareerId = \"nun_vocation\"", service);
        Assert.Contains("IsCallingOnlyCareer", service);
        Assert.Contains("!IsCallingOnlyCareer(definition)", opportunities);
        Assert.Contains("IsCallingOnlyCareer(definition)", opportunities);
        Assert.Contains("person.Tags.Has(\"vocation.religious.active\")", employment);
        Assert.Contains("return career.JobLevel > 0;", employment);
    }

    [Fact]
    public void ActiveClergyKeepPromotionAndRecoveryButCannotQuitLoseJobOrRetire()
    {
        var actions = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "CareerPlugin.Actions.cs");
        var support = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "CareerPlugin.FamilySupportActions.cs");
        var loss = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "CareerJobLossYearSystem.cs");
        var retirement = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "CareerRetirementYearSystem.cs");
        var advancement = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "CareerAdvancementYearSystem.cs");

        Assert.Contains("Id = \"career.work_harder\"", actions);
        Assert.Contains("Id = \"career.quit_job\"", actions);
        Assert.Contains("!actionContext.Actor.Tags.Has(\"vocation.religious.active\")", actions);
        Assert.Contains("target.Tags.Has(\"vocation.religious.active\")", support);
        Assert.Contains("person.Tags.Has(\"vocation.religious.active\")", loss);
        Assert.Contains("person.Tags.Has(\"vocation.religious.active\")", retirement);
        Assert.Contains("!person.Tags.Has(\"vocation.religious.active\")", advancement);
        Assert.Contains("career.JobLevel >= 5", advancement);
    }

    [Fact]
    public void CelibacyAndSuccessionGuardsCoverAllSpecifiedSystems()
    {
        AssertTagGuard("plugins", "Dynastia.Mechanics.Relationships", "MarriageYearSystem.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Relationships", "FemaleRemarriageYearSystem.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Relationships", "StandardPartnerSearchService.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Reproduction", "ReproductionEligibilityRules.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Reproduction", "NonmaritalBirthYearSystem.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Adoption", "AdoptionYearSystem.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Succession", "StandardSuccessionService.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Households", "StandardHouseholdService.Succession.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Households", "StandardHouseholdService.MembershipReconciliation.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Farming", "StandardFarmingService.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Crafts", "StandardCraftService.cs");
        AssertTagGuard("plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationService.cs");
    }

    [Fact]
    public void ClergyRelativeStatusUsesActiveChildSiblingBonusesAndCaps()
    {
        using var document = JsonDocument.Parse(
            RepositoryFiles.ReadText("data", "LocalSociety", "clerical_relative_status_rules.json"));
        var root = document.RootElement;
        var bonuses = root.GetProperty("bonuses").EnumerateArray().ToArray();

        Assert.Contains(bonuses, item =>
            item.GetProperty("relationship").GetString() == "Child"
            && TryGetInt(item, "activeVocationLevelMax") == 4
            && item.GetProperty("renown").GetDouble() == 2
            && item.GetProperty("reputation").GetDouble() == 0.5);
        Assert.Contains(bonuses, item =>
            item.GetProperty("relationship").GetString() == "Child"
            && TryGetInt(item, "activeVocationLevelMin") == 5
            && item.GetProperty("renown").GetDouble() == 4
            && item.GetProperty("reputation").GetDouble() == 1);
        Assert.Contains(bonuses, item =>
            item.GetProperty("relationship").GetString() == "Sibling"
            && TryGetInt(item, "activeVocationLevelMax") == 4
            && item.GetProperty("renown").GetDouble() == 1
            && item.GetProperty("reputation").GetDouble() == 0.25);
        Assert.False(root.GetProperty("formerVocationCounts").GetBoolean());
        Assert.Equal(6, root.GetProperty("perPersonCaps").GetProperty("renown").GetDouble());
        Assert.Equal(2, root.GetProperty("perPersonCaps").GetProperty("reputation").GetDouble());

        var status = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Status", "StandardStatusService.cs");
        Assert.Contains("_family.GetChildren(person)", status);
        Assert.Contains("\"Child\"", status);
        Assert.Contains("\"Sibling\"", status);
        Assert.Contains("relative.Tags.Has(\"vocation.religious.active\")", status);
        Assert.Contains("Math.Min(_clericalRelativeStatus.RenownCap, renown)", status);
        Assert.Contains("Math.Min(_clericalRelativeStatus.ReputationCap, reputation)", status);
    }

    private static int? TryGetInt(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value)
            ? value.GetInt32()
            : null;

    private static void AssertTagGuard(params string[] parts) =>
        Assert.Contains("vocation.religious.active", RepositoryFiles.ReadText(parts));

    private static Dictionary<string, string> Row(
        IReadOnlyList<Dictionary<string, string>> rows,
        string id,
        string key = "Id") =>
        rows.Single(row => row[key].Equals(id, StringComparison.OrdinalIgnoreCase));

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

        return lines.Skip(1)
            .Select(line =>
            {
                var values = line.Split(',').Select(value => value.Trim()).ToArray();
                return headers
                    .Select((header, index) => new { header, value = values[index] })
                    .ToDictionary(item => item.header, item => item.value, StringComparer.OrdinalIgnoreCase);
            })
            .ToArray();
    }

}
