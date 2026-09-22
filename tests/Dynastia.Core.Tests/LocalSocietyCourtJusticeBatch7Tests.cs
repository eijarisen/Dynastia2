using Dynastia.Contracts;
using Dynastia.Core.Entities;
using Dynastia.Mechanics.Justice;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyCourtJusticeBatch7Tests
{
    [Fact]
    public void CourtProtectionUsesWarmOrCloseBestRelativeAndExactTiers()
    {
        var rules = CourtJusticeRules.Load(
            new RepositoryDataService(RepositoryRoot()));
        var service = Read(
            "plugins", "Dynastia.Mechanics.Justice", "StandardJusticeService.cs");
        var relations = Read(
            "src", "Dynastia.Contracts", "IFamilyRelationService.cs");

        Assert.Contains("relation.Sympathy >= 60", relations);
        Assert.Contains("relation.Familiarity >= 75", relations);
        Assert.Contains("score = career.JobLevel * careerWeight * relationMultiplier", service);
        Assert.Contains("if (score <= bestScore)", service);
        Assert.Contains("bestHelper = candidate", service);

        var levelFiveCloseLegalScore = 5d * 1.0d * 1.0d;
        var exceptional = rules.ResolveProtection(levelFiveCloseLegalScore);
        Assert.Equal("exceptional", exceptional.Id);
        Assert.Equal("Exceptional", exceptional.Display);
        Assert.Equal(0.60m, exceptional.SentenceMultiplier);
        Assert.Equal(0.06d, exceptional.StolenSaleDetectionChance, 10);
    }

    [Fact]
    public void MastermindAndExceptionalProtectionRespectHalfSentenceFloor()
    {
        var rules = CourtJusticeRules.Load(
            new RepositoryDataService(RepositoryRoot()));
        var exceptional = rules.ResolveProtection(5d);
        var rawCombined = 0.80m * exceptional.SentenceMultiplier;
        var finalCombined = Math.Max(
            rules.Protection.CombinedSentenceMultiplierFloor,
            rawCombined);

        Assert.Equal(0.48m, rawCombined);
        Assert.Equal(0.50m, finalCombined);

        var crime = Read(
            "plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationService.cs");
        var justice = Read(
            "plugins", "Dynastia.Mechanics.Justice", "StandardJusticeService.cs");
        Assert.Contains("_justice.ConvictKnownOffense(", crime);
        Assert.Contains("CombinedSentenceMultiplierFloor", justice);
        Assert.Contains("Math.Ceiling(originalSentence * combinedMultiplier)", justice);
    }

    [Fact]
    public void BailIsExpensiveQueuedGuaranteedWhenAffordableAndRecordPersists()
    {
        var rules = CourtJusticeRules.Load(
            new RepositoryDataService(RepositoryRoot()));
        var actions = Read(
            "plugins", "Dynastia.Mechanics.Justice", "JusticePlugin.Court.cs");
        var state = Read(
            "plugins", "Dynastia.Mechanics.Justice", "JusticeComponent.cs");

        Assert.Equal(20_000m, rules.Bail.MinimumCost);
        Assert.Equal(27_500m, rules.CalculateBailCost(1));
        Assert.Equal(42_500m, rules.CalculateBailCost(3));
        Assert.Contains("Id = rules.Bail.ActionId", actions);
        Assert.Contains("Mode = ActionExecutionMode.Queued", actions);
        Assert.Contains("economy.ChangeWealth(context.Actor, -cost)", actions);
        Assert.Contains("justice.ReleaseFromPrison(context.Target)", actions);
        Assert.Contains("CriminalRecord", state);
        Assert.DoesNotContain("CriminalRecord.Clear", actions);
    }

    [Fact]
    public void EscapeIsIntellectFiveOncePerImprisonmentAndFailureAddsThreeYears()
    {
        var rules = CourtJusticeRules.Load(
            new RepositoryDataService(RepositoryRoot()));
        var actions = Read(
            "plugins", "Dynastia.Mechanics.Justice", "JusticePlugin.Court.cs");
        var state = Read(
            "plugins", "Dynastia.Mechanics.Justice", "JusticeComponent.cs");

        Assert.Equal(5, rules.Escape.RequiresIntellect);
        Assert.Equal(0.20d, rules.Escape.BaseSuccessChance, 10);
        Assert.Equal(0.30d, rules.Escape.MastermindSuccessChance, 10);
        Assert.Equal(3, rules.Escape.FailureSentenceExtensionYears);
        Assert.Contains("justice.HasAttemptedEscapeThisImprisonment", actions);
        Assert.Contains("justice.MarkEscapeAttempted(context.Target)", actions);
        Assert.Contains("justice.ExtendSentence(", actions);
        Assert.Contains("EscapeAttemptedCurrentImprisonment", state);
        Assert.Contains("BypassGuards = true", actions);
    }

    [Fact]
    public void StolenHeirloomRiskOccursOnlyOnSaleAndUsesProtectionTier()
    {
        var rules = CourtJusticeRules.Load(
            new RepositoryDataService(RepositoryRoot()));
        var heirlooms = Read(
            "plugins", "Dynastia.Mechanics.Heirlooms", "HeirloomsPlugin.cs");
        var status = Read(
            "data", "LocalSociety", "status_extension_event_effects.csv");

        Assert.True(rules.StolenHeirloomSale.KeepingIsHarmless);
        Assert.Contains("if (sold.IsStolen)", heirlooms);
        Assert.Contains("GetStolenHeirloomSaleDetectionChance", heirlooms);
        Assert.Contains("selling_stolen_property", heirlooms);
        Assert.Contains("justice.stolen_heirloom_sale_caught", heirlooms);
        Assert.Contains("justice.stolen_heirloom_sale_caught,0,-8,seller", status);
    }

    [Fact]
    public void RawImprisonmentDoesNotCreateKnownCriminalRecord()
    {
        var justice = new StandardJusticeService();
        var person = new Person("Jan", "Test", 40);

        justice.Imprison(
            person,
            4,
            "political_detention",
            "political detention");

        Assert.True(justice.GetStatus(person).IsImprisoned);
        Assert.Empty(justice.GetStatus(person).KnownCriminalRecord);
    }

    [Fact]
    public void ReleasePreservesRecordAndNewImprisonmentResetsEscapeAttempt()
    {
        var justice = new StandardJusticeService();
        var person = new Person("Jan", "Test", 40);

        justice.ConvictKnownOffense(
            person,
            4,
            "fraud",
            "fraud");
        Assert.Single(justice.GetStatus(person).KnownCriminalRecord);

        justice.MarkEscapeAttempted(person);
        Assert.True(justice.HasAttemptedEscapeThisImprisonment(person));
        Assert.True(justice.ReleaseFromPrison(person));
        Assert.Single(justice.GetStatus(person).KnownCriminalRecord);

        justice.Imprison(person, 2, "detention", "detention");
        Assert.False(justice.HasAttemptedEscapeThisImprisonment(person));
        Assert.Single(justice.GetStatus(person).KnownCriminalRecord);
    }

    [Fact]
    public void CourtTownAffairsShowsInstitutionProtectionActionsRecordAndRisk()
    {
        var model = Read(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var window = Read(
            "src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var relations = Read(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Relations.cs");

        Assert.Contains("GetTownAffairsCourtModel", model);
        Assert.Contains("No local court. Criminal matters are handled by outside authorities.", model);
        Assert.Contains("GetCourtProtection(subject)", model);
        Assert.Contains("GetBailCost(subject)", model);
        Assert.Contains("GetStolenHeirloomSaleDetectionChance(subject)", model);
        Assert.Contains("Header=\"Court\"", window);
        Assert.Contains("Known Criminal Record", window);
        Assert.Contains("CourtModel.Actions", window);
        Assert.Contains("justice.bail_out", relations);
        Assert.Contains("GetFamilyRelationsJusticeActions", relations);
        var familyWindow = Read(
            "src", "Dynastia.App", "Views", "FamilyRelationsWindow.axaml");
        Assert.Contains("Imprisoned Household Members", familyWindow);
        Assert.Contains("JusticeActions", familyWindow);
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
