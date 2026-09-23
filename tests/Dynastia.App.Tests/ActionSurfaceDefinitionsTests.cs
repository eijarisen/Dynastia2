using Dynastia.App.ViewModels.Actions;
using Dynastia.Contracts;
using static Dynastia.App.Tests.ActionOptionTestServices;

namespace Dynastia.App.Tests;

public sealed class ActionSurfaceDefinitionsTests
{
    [Fact]
    public void TownNavigationRetainsActualResidenceMembershipAgeLifeAndImprisonmentGuards()
    {
        using var f = new ActionPanelFixture();
        var justice = new Justice();
        var surfaces = new ActionSurfaceDefinitions(f.Locations, f.Economy, justice);
        var other = f.Person(30, "Resident");
        Assert.True(surfaces.CanOpenTownAffairs(f.Head, f.Head));
        Assert.False(surfaces.CanOpenTownAffairs(f.Head, other));
        f.Economy.Members.Add(other.Id);
        Assert.True(surfaces.CanOpenTownAffairs(f.Head, other));
        other.Age = 17;
        Assert.False(surfaces.CanOpenTownAffairs(f.Head, other));
        other.Age = 18;
        other.Tags.Remove("state.alive");
        Assert.False(surfaces.CanOpenTownAffairs(f.Head, other));
        other.Tags.Add("state.alive");
        justice.Prisoners.Add(other.Id);
        Assert.False(surfaces.CanOpenTownAffairs(f.Head, other));
        justice.Prisoners.Clear();
        justice.Prisoners.Add(f.Head.Id);
        Assert.False(surfaces.CanOpenTownAffairs(f.Head, other));
        Assert.False(surfaces.CanOpenTownAffairs(null, other));
        Assert.False(surfaces.CanOpenTownAffairs(f.Head, null));
        Assert.False(new ActionSurfaceDefinitions(null, f.Economy, null).CanOpenTownAffairs(f.Head, f.Head));
        Assert.False(new ActionSurfaceDefinitions(f.Locations, null, null).CanOpenTownAffairs(f.Head, f.Head));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImprisonedActorOrTargetHidesTownNavigationInTheActualPanel(bool imprisonActor)
    {
        var justice = new Justice();
        using var f = new ActionCoordinatorFixture(justice: justice);
        var resident = f.Source.Person(30, "Resident");
        f.Source.Economy.Members.Add(resident.Id);
        f.Selected = resident;
        f.Source.Register("turn.pass");
        f.Panel.Refresh(false);
        Assert.Contains(f.Panel.AvailableActions, a => a.Id == "ui.town_affairs");
        justice.Prisoners.Add(imprisonActor ? f.Source.Head.Id : resident.Id);
        f.Panel.Refresh(false);
        Assert.Equal("turn.pass", Assert.Single(f.Panel.AvailableActions).Id);
    }

    [Theory]
    [InlineData(50000, "City Affairs")]
    [InlineData(150000, "City Affairs")]
    [InlineData(2000, "Town Affairs")]
    public void NavigationFactoryKeepsLocalLabelAndPresentation(int population, string expected)
    {
        using var f = new ActionPanelFixture();
        var locations = new ActionPanelFixture.LocationsStub(f.Economy.Residence with { Population = population });
        var surfaces = new ActionSurfaceDefinitions(locations, f.Economy, null);
        var action = surfaces.CreateTownAffairsPresentationAction(f.Head);
        Assert.Equal("ui.town_affairs", action.Id);
        Assert.Equal(expected, action.Label);
        Assert.Equal("🏛️", action.Presentation.Emoji);
        Assert.Equal(ActionPresentationCategories.Personal, Assert.Single(action.Presentation.Categories));
        Assert.Equal("Town Affairs", surfaces.GetTownLifeNavigationLabel(null));
        Assert.Empty(f.Registry.Submissions);
    }

    [Theory]
    [InlineData("household.buy_house", "ui.manage_properties", "Manage Properties")]
    [InlineData("household.extend_house", "ui.manage_properties", "Manage Properties")]
    [InlineData("farming.add_livestock", "ui.manage_properties", "Manage Properties")]
    [InlineData("heirloom.sell", "ui.manage_properties", "Manage Properties")]
    [InlineData("loan.give", "ui.manage_finances", "Manage Finances")]
    [InlineData("economy.lifestyle.thrifty", "ui.manage_finances", "Manage Finances")]
    [InlineData("craft.start.writing", "ui.craft_profession", "Work in a Profession")]
    public void AggregatesKeepIdsLabelsAndCaseInsensitiveRecognition(string underlying, string surface, string label)
    {
        Assert.Equal(surface, ActionSurfaceDefinitions.GetAggregateActionId(underlying.ToUpperInvariant()));
        var definition = ActionSurfaceDefinitions.CreateAggregateAction(surface);
        Assert.Equal(surface, definition.Id);
        Assert.Equal(label, definition.Label);
        Assert.True(ActionSurfaceDefinitions.RequiresSelection(surface));
        if (surface == "ui.craft_profession")
        {
            Assert.Equal(ActionExecutionMode.Queued, definition.Mode);
            Assert.Equal(YearPhase.LifeEvents, definition.QueuePhase);
        }
        else Assert.Equal(ActionExecutionMode.Immediate, definition.Mode);
    }
}
