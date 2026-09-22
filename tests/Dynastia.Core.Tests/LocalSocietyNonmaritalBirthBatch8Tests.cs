using System.Text.Json;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyNonmaritalBirthBatch8Tests
{
    [Fact]
    public void RulesKeepEventRareAndApplySpecifiedAgeFertilityPersonalityModifiers()
    {
        using var document = JsonDocument.Parse(
            Read("data", "LocalSociety", "nonmarital_birth_rules.json"));
        var root = document.RootElement;

        Assert.Equal(15, root.GetProperty("minimumAge").GetInt32());
        Assert.Equal(45, root.GetProperty("maximumAge").GetInt32());
        Assert.Equal(0.002, root.GetProperty("baseAnnualChance").GetDouble(), 10);
        Assert.Equal(0.6, root.GetProperty("fertilityMultipliers").GetProperty("1").GetDouble(), 10);
        Assert.Equal(1.3, root.GetProperty("fertilityMultipliers").GetProperty("5").GetDouble(), 10);
        Assert.Equal(1.5, root.GetProperty("temperamentMultipliers").GetProperty("Sanguine").GetDouble(), 10);
        Assert.Equal(1.35, root.GetProperty("temperamentMultipliers").GetProperty("Choleric").GetDouble(), 10);
        Assert.Equal(0.75, root.GetProperty("temperamentMultipliers").GetProperty("Phlegmatic").GetDouble(), 10);
        Assert.Equal(0.85, root.GetProperty("temperamentMultipliers").GetProperty("Melancholic").GetDouble(), 10);
        Assert.Equal(2.0, root.GetProperty("moralsMultipliers").GetProperty("Evil").GetDouble(), 10);

        var source = Read(
            "plugins", "Dynastia.Mechanics.Reproduction", "NonmaritalBirthYearSystem.cs");
        Assert.Contains("person.Age < _rules.MinimumAge", source);
        Assert.Contains("person.Age > _rules.MaximumAge", source);
        Assert.Contains("_family.GetSpouse(person) is not null", source);
        Assert.Contains("person.Tags.Has(\"state.imprisoned\")", source);
        Assert.Contains("GetStat(person, \"fertility\") > 0", source);
    }

    [Fact]
    public void UnknownFatherBirthUsesMotherIdentityAndMaternalOnlyInheritance()
    {
        var system = Read(
            "plugins", "Dynastia.Mechanics.Reproduction", "NonmaritalBirthYearSystem.cs");
        var reproduction = Read(
            "plugins", "Dynastia.Mechanics.Reproduction", "ReproductionYearSystem.cs");

        Assert.Contains("father: null", system);
        Assert.Contains("Type = \"reproduction.unknown_father_birth\"", system);
        Assert.Contains("Guid.Empty, mother.Id", system);
        Assert.Contains("father is null\n            ? _nationalities.GetNationality(mother)", reproduction);
        Assert.Contains("father?.Surname\n            ?? mother.Surname", reproduction);
        Assert.Contains("maternalOnly: father is null", reproduction);
        Assert.Contains("InheritMaternalStats(mother)", reproduction);
        Assert.Contains("_family.SetParents(\n            child,\n            father,\n            mother)", reproduction);
    }

    [Fact]
    public void SameYearMarriageReusesFullPartnerPipelineAndCreatesOnlyOneSpecialChild()
    {
        using var document = JsonDocument.Parse(
            Read("data", "LocalSociety", "nonmarital_birth_rules.json"));
        var root = document.RootElement;
        Assert.Equal(0.55, root.GetProperty("outcomes").GetProperty("unknownFatherChance").GetDouble(), 10);
        Assert.Equal(0.45, root.GetProperty("outcomes").GetProperty("marryFatherSameYearChance").GetDouble(), 10);
        Assert.True(root.GetProperty("outcomes").GetProperty("singleBirthOnly").GetBoolean());

        var system = Read(
            "plugins", "Dynastia.Mechanics.Reproduction", "NonmaritalBirthYearSystem.cs");
        var partner = Read(
            "plugins", "Dynastia.Mechanics.Relationships", "StandardPartnerSearchService.cs");
        var contract = Read(
            "src", "Dynastia.Contracts", "IPartnerSearchService.cs");

        Assert.Contains("\"nonmarital_father\"", system);
        Assert.Contains("minimumPartnerAge: _rules.FatherMinimumAge", system);
        Assert.Contains("mother.Age + _rules.FatherMaximumAgeOffsetFromMother", system);
        Assert.Contains("_partnerSearch.MaterializeCandidate(", system);
        Assert.Contains("seedHouseholdResources: true", system);
        Assert.Contains("_family.SetSpouses(", system);
        Assert.Contains("Type = \"relationship.married\"", system);
        Assert.Contains("Type = \"reproduction.birth_and_marriage\"", system);
        Assert.Contains("CreateNonmaritalChild(", system);
        Assert.Contains("public IPerson MaterializeCandidate(", partner);
        Assert.Contains("IPerson MaterializeCandidate(", contract);
    }

    [Fact]
    public void SystemRunsAfterMarriageAndAffairsButBeforeOrdinaryBirths()
    {
        var system = Read(
            "plugins", "Dynastia.Mechanics.Reproduction", "NonmaritalBirthYearSystem.cs");
        var eligibility = Read(
            "plugins", "Dynastia.Mechanics.Reproduction", "ReproductionEligibilityRules.cs");

        Assert.Contains("public string Id => \"reproduction.nonmarital_births\"", system);
        Assert.Contains("Before => [\"reproduction.births\"]", system);
        Assert.Contains("\"relationships.marriage\"", system);
        Assert.Contains("\"relationships.affairs\"", system);
        Assert.Contains("activeMarriage.StartYear == year", eligibility);
    }

    [Fact]
    public void TeenageAndUnknownFatherStatusEffectsStackByNamedTargets()
    {
        var data = Read(
            "data", "LocalSociety", "status_extension_event_effects.csv");
        var catalog = Read(
            "plugins", "Dynastia.Mechanics.Status", "StatusEventCatalog.cs");
        var plugin = Read(
            "plugins", "Dynastia.Mechanics.Status", "StatusPlugin.cs");

        Assert.Contains("reproduction.unknown_father_birth,2,-6,mother", data);
        Assert.Contains("reproduction.teen_birth,3,-8,mother", data);
        Assert.Contains("reproduction.teen_marriage,2,-5,mother", data);
        Assert.Contains("reproduction.teen_marriage,1,-2,husband", data);
        Assert.Contains("Dictionary<string, IReadOnlyList<StatusEventEffect>>", catalog);
        Assert.Contains(".Concat(entry.Value)", catalog);
        Assert.Contains("ResolveEventTargets(gameEvent, effect.TargetRule)", plugin);
        Assert.Contains("NamedTarget(gameEvent, \"motherId\")", plugin);
        Assert.Contains("NamedTarget(gameEvent, \"husbandId\")", plugin);
    }

    [Fact]
    public void NonmaritalEventsAppearAsFamilyNewsWithoutDuplicatingNormalBirthChronicle()
    {
        var system = Read(
            "plugins", "Dynastia.Mechanics.Reproduction", "NonmaritalBirthYearSystem.cs");
        var familyNews = Read(
            "plugins", "Dynastia.Mechanics.Households", "StandardHouseholdService.FamilyNews.cs");
        var emoji = Read(
            "src", "Dynastia.App", "ViewModels", "EventEmojiMap.cs");

        Assert.Contains("[\"suppressChronicle\"] = \"true\"", system);
        Assert.Contains("reproduction.unknown_father_birth", familyNews);
        Assert.Contains("reproduction.birth_and_marriage", familyNews);
        Assert.Contains("reproduction.teen_birth", familyNews);
        Assert.Contains("reproduction.teen_marriage", familyNews);
        Assert.Contains("[\"reproduction.unknown_father_birth\"] = \"👶\"", emoji);
        Assert.Contains("[\"reproduction.birth_and_marriage\"] = \"💍\"", emoji);
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
