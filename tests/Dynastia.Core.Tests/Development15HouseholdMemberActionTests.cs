using System.Text.Json;

namespace Dynastia.Core.Tests;

public sealed class Development15HouseholdMemberActionTests
{
    [Fact]
    public void AdultHouseholdSupportActionsUseAuthoritativeResidenceRatherThanKinshipOnly()
    {
        var contracts = RepositoryFiles.ReadText(
            "src", "Dynastia.Contracts", "HouseholdKinshipRules.cs");
        var career = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Career", "CareerPlugin.Helpers.cs");
        var education = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Education", "EducationPlugin.cs");
        var crafts = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Crafts", "CraftsPlugin.cs");
        var wellbeing = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Wellbeing", "WellbeingPlugin.TreatmentActions.cs");

        Assert.Contains("IsResidentHouseholdMember", contracts);
        Assert.Contains("economy.GetHouseholdId(target) == householdId", contracts);
        Assert.Contains("IsResidentHouseholdMember", career);
        Assert.DoesNotContain("IsSupportedResidentRelative", career);
        Assert.Contains("IsResidentHouseholdMember", education);
        Assert.Contains("requireAdult: true", education);
        Assert.Contains("IsResidentHouseholdMember", crafts);
        Assert.Contains("requireAdult: true", crafts);
        Assert.Contains("CanTreatHouseholdMember", wellbeing);
        Assert.Contains("IsResidentHouseholdMember", wellbeing);
    }

    [Fact]
    public void ArrangedMarriageRejectsOlderGenerationAndRequiresCurrentHousehold()
    {
        var relationships = RepositoryFiles.ReadText(
            "plugins",
            "Dynastia.Mechanics.Relationships",
            "RelationshipsPlugin.ArrangedMarriage.cs");

        Assert.Contains("IsSameOrLowerGeneration", relationships);
        Assert.Contains("selectedGeneration >= activeGeneration", relationships);
        Assert.Contains("family.GetMother(controller)?.Id == target.Id", relationships);
        Assert.Contains("ResolveHouseholdHead", relationships);
        Assert.Contains("== father.Id", relationships);
    }

    [Fact]
    public void CoupleActionsRemainRestrictedToTheControlledPersonsOwnMarriage()
    {
        var reproduction = RepositoryFiles.ReadText(
            "plugins",
            "Dynastia.Mechanics.Reproduction",
            "ReproductionPlugin.cs");
        var relationships = RepositoryFiles.ReadText(
            "plugins",
            "Dynastia.Mechanics.Relationships",
            "RelationshipsPlugin.ArrangedMarriage.cs");
        var succession = RepositoryFiles.ReadText(
            "plugins",
            "Dynastia.Mechanics.Succession",
            "StandardSuccessionService.cs");

        Assert.Contains("!actionContext.ActorHasControl", reproduction);
        Assert.Contains("actionContext.Target.Id != actor.Id", reproduction);
        Assert.Contains("actionContext.Target.Id != spouse.Id", reproduction);
        Assert.Contains("!actionContext.ActorHasControl", relationships);
        Assert.Contains("target.Id != actor.Id", relationships);
        Assert.Contains("target.Id != wife.Id", relationships);
        Assert.Contains("&& _economy.HasHousehold(person)", succession);
    }

    [Fact]
    public void ChurchTabSelectsAdultHouseholdMemberAndTargetsTheirPersonalActions()
    {
        var church = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Church", "ChurchPlugin.cs");
        var personality = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Personality", "PersonalityPlugin.cs");
        var hub = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var presentation = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var window = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        Assert.Contains("TownAffairsTab.Church", hub);
        Assert.Contains("person.Age >= 18", hub);
        Assert.Contains("GetTownAffairsChurchActions(Subject)", hub);
        Assert.Contains("GetTownAffairsChurchActions(\n            IPerson subject)", presentation);
        Assert.Contains("GetCandidateActions(actor, subject)", presentation);
        Assert.Contains("actor,\n                subject", presentation);
        Assert.Contains("ItemsSource=\"{Binding JobHouseholdMembers}\"", window);
        Assert.Contains("Header=\"Church\"", window);
        Assert.Contains("IsResidentHouseholdMember", church);
        Assert.Contains("SubjectId = actionContext.Target.Id", church);
        Assert.Contains("IsResidentHouseholdMember", personality);
        Assert.Contains("SubjectId = target.Id", personality);
    }

    [Fact]
    public void RecoveryActivitiesAvoidRelationshipSpecificNarratives()
    {
        var json = RepositoryFiles.ReadText(
            "data", "Common", "recovery_activities_time_based.json");
        using var document = JsonDocument.Parse(json);

        var text = document.RootElement
            .EnumerateArray()
            .Select(item => item.GetProperty("text").GetString() ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(text, value =>
            value.Contains("child", StringComparison.OrdinalIgnoreCase)
            || value.Contains("relative", StringComparison.OrdinalIgnoreCase)
            || value.Contains("family", StringComparison.OrdinalIgnoreCase));
    }
}
