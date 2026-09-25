using Dynastia.Contracts;
using Dynastia.Mechanics.Church;
using Dynastia.Mechanics.TownLife;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyChurchBatch2Tests
{
    [Fact]
    public void ChurchIsAlwaysPresentAndTierNeverFallsAsPopulationRises()
    {
        var rules = ChurchInstitutionRules.Load(
            new RepositoryDataService(RepositoryFiles.Root));
        var populations = new[]
        {
            0, 1_999, 2_000, 9_999, 10_000,
            39_999, 40_000, 99_999, 100_000, 500_000
        };

        var resolved = populations
            .Select(rules.Resolve)
            .ToArray();

        Assert.All(resolved, church =>
        {
            Assert.Equal("church", church.InstitutionId);
            Assert.True(church.IsAvailable);
            Assert.InRange(church.Tier, 1, 5);
        });

        for (var index = 1; index < resolved.Length; index++)
            Assert.True(resolved[index].Tier >= resolved[index - 1].Tier);

        Assert.Equal("Chapel", resolved[0].TierName);
        Assert.Equal("Parish Church", resolved[2].TierName);
        Assert.Equal("Large Church", resolved[4].TierName);
        Assert.Equal("Major Church", resolved[6].TierName);
        Assert.Equal("Principal City Church", resolved[8].TierName);
    }

    [Fact]
    public void ChurchDonationAndDirectCharityUseDifferentStatusAndMoralsProfiles()
    {
        var rules = ChurchRules.Load(
            new RepositoryDataService(RepositoryFiles.Root));

        Assert.Equal(500m, rules.CalculateDonationAmount(
            rules.ChurchDonationTiers,
            "modest",
            10_000m));
        Assert.Equal(1_500m, rules.CalculateDonationAmount(
            rules.ChurchDonationTiers,
            "generous",
            10_000m));
        Assert.Equal(3_500m, rules.CalculateDonationAmount(
            rules.ChurchDonationTiers,
            "major",
            10_000m));

        var church = rules.ChurchDonationTiers["major"];
        var poor = rules.PoorFamilyTiers["major"];
        Assert.True(church.Renown > poor.Renown);
        Assert.True(church.Reputation < poor.Reputation);
        Assert.True(church.MoralsImproveChance < poor.MoralsImproveChance);
    }

    [Fact]
    public void WelfareAndAttendanceMatchTheSuppliedLimits()
    {
        var rules = ChurchRules.Load(
            new RepositoryDataService(RepositoryFiles.Root));

        Assert.Equal(0m, rules.Welfare.MaximumWealth);
        Assert.Equal(-10d, rules.Welfare.MinimumReputation, 10);
        Assert.True(rules.Welfare.OncePerHouseholdPerYear);
        Assert.True(rules.Welfare.GuaranteedIfEligible);
        Assert.Equal(0d, rules.Welfare.StatusPenalty, 10);
        Assert.False(rules.IsWelfareEligible(1m, 0d));
        Assert.False(rules.IsWelfareEligible(0m, -10.01d));
        Assert.True(rules.IsWelfareEligible(0m, -10d));
        Assert.Equal(1_000m, rules.CalculateWelfareAmount(10_000m, 1));
        Assert.Equal(1_500m, rules.CalculateWelfareAmount(10_000m, 5));

        Assert.Equal(0.05d, rules.Attend.MoralsImproveChance, 10);
        Assert.Equal(0.05d, rules.Attend.GoodMoralsProtectionChance, 10);
        Assert.Contains(
            "church.attend,0.15,0.25,actor",
            RepositoryFiles.ReadText("data", "LocalSociety", "status_event_effects.csv"));

        var personality = RepositoryFiles.ReadText(
            "plugins",
            "Dynastia.Mechanics.Personality",
            "PersonalityPlugin.cs");
        Assert.Contains("rules.GetSuccessChance(churchTier)", personality);
        Assert.Contains("random.NextDouble() >= successChance", personality);
        Assert.True(rules.Attend.MoralsImproveChance < 0.45d);
    }

    [Fact]
    public void ChurchActionsRespectEligibilityAndTownAffairsWiring()
    {
        var church = RepositoryFiles.ReadText(
            "plugins",
            "Dynastia.Mechanics.Church",
            "ChurchPlugin.cs");
        var townLife = RepositoryFiles.ReadText(
            "plugins",
            "Dynastia.Mechanics.TownLife",
            "StandardTownInstitutionService.cs");
        var window = RepositoryFiles.ReadText(
            "src",
            "Dynastia.App",
            "Views",
            "TownLifeWindow.axaml");
        var news = RepositoryFiles.ReadText(
            "data",
            "LocalSociety",
            "local_society_news_templates.csv");

        Assert.Contains("church.attend", church);
        Assert.Contains("church.donate", church);
        Assert.Contains("church.aid_poor_family", church);
        Assert.Contains("church.ask_welfare", church);
        Assert.Contains("rules.IsWelfareEligible", church);
        Assert.Contains("LastWelfareYear", church);
        Assert.Contains("YearPhase.QueuedActionsEarly", church);
        Assert.Contains("_church.Resolve(town.Population)", townLife);

        Assert.Contains("Header=\"Church\"", window);
        Assert.DoesNotContain("Snapshot.ChurchCard", window);
        Assert.Contains("ChurchActions", window);
        Assert.Contains("<primitives:UniformGrid Columns=\"3\" />", window);
        Assert.DoesNotContain("ToolTip.Tip=\"{Binding Description}\"", window);
        Assert.Contains("Text=\"{Binding Description}\"", window);
        var presentation = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        Assert.Contains("AddChurchMoneyAction(\"church.donate\")", presentation);
        Assert.Contains("AddChurchMoneyAction(\"church.aid_poor_family\")", presentation);
        Assert.Contains("ResolveChurchDonationTier", presentation);

        Assert.Contains("church.attend", news);
        Assert.Contains("church.donate", news);
        Assert.Contains("church.aid_poor", news);
        Assert.Contains("church.welfare", news);
    }

    private sealed class RepositoryDataService(string root) : IGameDataService
    {
        public string ReadText(string relativePath) =>
            File.ReadAllText(
                Path.Combine(
                    root,
                    "data",
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));

        public IReadOnlyList<string> GetStringList(string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(
            string relativePath) =>
            throw new NotSupportedException();
    }
}
