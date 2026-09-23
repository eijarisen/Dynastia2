using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Career;
using Dynastia.Mechanics.Education;
using Dynastia.Mechanics.TownLife;

namespace Dynastia.Core.Tests;

public sealed class TownLifeBatch3Tests
{
    [Fact]
    public void SchoolTierCapsPassiveAndGeneratedLocalEducation()
    {
        var rules = EducationLocalityRules.Load(DataService());
        var era = new EducationEraRule(
            1900,
            null,
            1.0,
            PassiveMaxLevel: 5,
            HelpedMaxLevel: 5,
            FounderMinLevel: 1,
            FounderMaxLevel: 3,
            GeneratedAdultMinLevel: 2,
            GeneratedAdultMaxLevel: 5);

        Assert.Equal(0, rules.GetMaximumLocalEducation(0));
        Assert.Equal(2, rules.GetMaximumLocalEducation(2));
        Assert.Equal(5, rules.GetMaximumLocalEducation(5));

        Assert.Equal(
            2,
            EducationProgressionRules.GetLocalPassiveChildhoodCeiling(
                intellect: 5,
                eraMaximum: 5,
                localSchoolCeiling: 2));

        var noSchool = rules.GetGeneratedAdultRange(era, 0);
        Assert.Equal(0, noSchool.MinimumLevel);
        Assert.Equal(0, noSchool.MaximumLevel);

        var villageSchool = rules.GetGeneratedAdultRange(era, 2);
        Assert.Equal(2, villageSchool.MinimumLevel);
        Assert.Equal(2, villageSchool.MaximumLevel);
    }

    [Fact]
    public void HelpInLearningCanExceedSchoolButNeverHelperEducation()
    {
        var ordinaryLocalCeiling =
            EducationProgressionRules.GetLocalPassiveChildhoodCeiling(
                intellect: 5,
                eraMaximum: 5,
                localSchoolCeiling: 2);
        var helpedCeiling =
            EducationProgressionRules.GetHelpedChildhoodCeiling(
                intellect: 5,
                eraMaximum: 5,
                helperEducation: 4);
        var lowEducationHelper =
            EducationProgressionRules.GetHelpedChildhoodCeiling(
                intellect: 5,
                eraMaximum: 5,
                helperEducation: 1);

        Assert.Equal(2, ordinaryLocalCeiling);
        Assert.Equal(4, helpedCeiling);
        Assert.Equal(1, lowEducationHelper);
    }

    [Fact]
    public void InstitutionRequirementsAreOptionalHardCareerRequirements()
    {
        var data = DataService();
        var catalog = CareerInstitutionRequirementCatalog.Load(
            data,
            ReadCareerIds(data));

        Assert.Equal(16, catalog.Count);

        var bankTierOne = InstitutionSnapshot(
            new TownInstitutionInfo("bank", "Bank", 1, "Moneylender"));
        var bankTierTwo = InstitutionSnapshot(
            new TownInstitutionInfo("bank", "Bank", 2, "Local Bank"));
        var schoolTierThree = InstitutionSnapshot(
            new TownInstitutionInfo("school", "School", 3, "Secondary School"));
        var schoolTierFour = InstitutionSnapshot(
            new TownInstitutionInfo("school", "School", 4, "Higher School"));

        var churchTierOne = InstitutionSnapshot(
            new TownInstitutionInfo("church", "Church", 1, "Chapel"));

        Assert.False(catalog.IsSatisfied("banking", bankTierOne));
        Assert.True(catalog.IsSatisfied("banking", bankTierTwo));
        Assert.False(catalog.IsSatisfied("scientific_research", schoolTierThree));
        Assert.True(catalog.IsSatisfied("scientific_research", schoolTierFour));
        Assert.True(catalog.IsSatisfied("agriculture", bankTierOne));
        Assert.True(catalog.IsSatisfied("priest_vocation", churchTierOne));
        Assert.True(catalog.IsSatisfied("nun_vocation", churchTierOne));
    }

    [Fact]
    public void TownAffairsInstitutionCardsListCareersEnabledByCurrentTier()
    {
        var catalog = TownInstitutionCareerCatalog.Load(DataService());

        var tierOne = catalog.GetEnabledCareers("bank", 1, 1900);
        var tierTwo = catalog.GetEnabledCareers("bank", 2, 1900);
        var tierFourSchool = catalog.GetEnabledCareers("school", 4, 1900);

        Assert.DoesNotContain(tierOne, item => item.Contains("Banking", StringComparison.Ordinal));
        Assert.Contains(tierTwo, item => item.Contains("Banking", StringComparison.Ordinal));
        Assert.Contains(tierTwo, item => item.Contains("Insurance", StringComparison.Ordinal));
        Assert.Contains(tierFourSchool, item => item.Contains("Scientific Research", StringComparison.Ordinal));
    }

    [Fact]
    public void CareerGenerationApplicationsAndAnnualRevalidationUseInstitutions()
    {
        var root = RepositoryFiles.Root;
        var opportunities = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Career",
            "StandardCareerService.Opportunities.cs"));
        var employment = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Career",
            "StandardCareerService.Employment.cs"));
        var jobLoss = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Career",
            "CareerJobLossYearSystem.cs"));

        Assert.Contains("MeetsInstitutionRequirement(person, definition)", opportunities);
        Assert.Contains("context.Town", opportunities);
        Assert.Contains("MeetsInstitutionRequirement", employment);
        Assert.Contains("GetCurrentInstitutionFailure", jobLoss);
        Assert.Contains("local_institution_closed", jobLoss);
        Assert.Contains("career.fired", jobLoss);
    }

    [Fact]
    public void CraftTrainingIsLocalButKnownCraftPracticeSurvivesRelocation()
    {
        var root = RepositoryFiles.Root;
        var service = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Crafts",
            "StandardCraftService.cs"));
        var actions = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Crafts",
            "CraftsPlugin.cs"));

        Assert.Contains("MeetsTrainingAvailability(person, craft)", service);
        Assert.Contains("craft.MeetsHardAvailability", service);
        Assert.Contains("crafts.CanLearnCraft(context.Target, definition.Id)", actions);

        var startOccupation = ExtractMethod(
            service,
            "public bool StartOccupation",
            "public bool EndOccupation");
        Assert.Contains("KnowsCraft(person, craft.Id)", startOccupation);
        Assert.DoesNotContain("MeetsTrainingAvailability", startOccupation);
    }

    [Fact]
    public void GeneratedPartnersAndEducationUiUseLocalSchoolCeiling()
    {
        var root = RepositoryFiles.Root;
        var partners = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Relationships",
            "StandardPartnerSearchService.cs"));
        var educationUi = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Dynastia.App",
            "ViewModels",
            "MainWindowViewModel.CraftsEducation.cs"));
        var educationPlugin = File.ReadAllText(Path.Combine(
            root,
            "plugins",
            "Dynastia.Mechanics.Education",
            "EducationPlugin.cs"));

        Assert.Contains("GetGeneratedAdultRange(\n                _gameState.Year,\n                candidateTown)", partners);
        Assert.Contains("GetLocalEducationCeiling(target, _gameState.Year)", educationUi);
        Assert.Contains("HasLocalSchool(", educationPlugin);
        Assert.Contains("institutions.Resolve(town, year).GetTier(\"school\") > 0", educationPlugin);
        Assert.Contains("No local School is available.", educationPlugin);
        Assert.Contains("helperEducation", educationPlugin);
        Assert.Contains("Help in Learning", educationPlugin);
        Assert.Contains("IsParentOf(helper, child, family)", educationPlugin);
    }

    [Fact]
    public void Batch3DataFilesAreInstalledFromImplementationPackage()
    {
        var root = RepositoryFiles.Root;

        Assert.True(File.Exists(Path.Combine(
            root,
            "data",
            "TownLife",
            "career_institution_requirements.csv")));
        Assert.True(File.Exists(Path.Combine(
            root,
            "data",
            "TownLife",
            "education_locality_rules.json")));
        Assert.True(File.Exists(Path.Combine(
            root,
            "data",
            "TownLife",
            "craft_locality_rules.json")));
    }

    private static TownInstitutionSnapshot InstitutionSnapshot(
        TownInstitutionInfo institution) =>
        new(
            Town(),
            1900,
            [institution]);

    private static TownInfo Town() =>
        new("Test Town", "Test County", 19.0, 52.0, 25_000)
        {
            Id = "batch3-test-town",
            RegionId = "test_region",
            PolityId = "test_polity",
            PolityName = "Test Polity",
            IsDestinationAvailable = true
        };

    private static IReadOnlyList<string> ReadCareerIds(
        JsonGameDataService data)
    {
        var lines = data.ReadText("Career/careers.csv")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        return lines
            .Skip(1)
            .Select(line => line.Split(',')[1].Trim())
            .ToList();
    }

    private static string ExtractMethod(
        string source,
        string startMarker,
        string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    private static JsonGameDataService DataService() =>
        new(Path.Combine(RepositoryFiles.Root, "data"));

}
