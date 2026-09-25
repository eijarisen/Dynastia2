using Dynastia.App.ViewModels;
using Dynastia.Contracts;
using static Dynastia.App.Tests.ActionPresentationCharacterizationTests;

namespace Dynastia.App.Tests;

public sealed class ActionPresentationMetadataTests
{
    [Theory]
    [InlineData("career.seek_employment")]
    [InlineData("church.attend")]
    [InlineData("plugin.never_seen_before")]
    public void ExplicitPresentationOverridesMisleadingIdsWithoutCentralMapChanges(string id)
    {
        var metadata = new ActionPresentationMetadata
        {
            Emoji = "🧭", Categories = [ActionPresentationCategories.Skills],
            AdjacencyGroup = "plugin.exploration", GroupOrder = 12,
            ShowInPrimaryActionList = true
        };
        var definition = Action(id, metadata);
        Assert.Same(metadata, ActionPresentationPolicy.Resolve(definition));
        var executions = 0;
        var item = new AvailableActionViewModel(definition, () => executions++, "Contextual title");
        Assert.Equal(ActionCategory.Skills, Assert.Single(item.Categories));
        Assert.Equal("🧭 Contextual title", item.Label);
        item.ExecuteCommand.Execute(null);
        Assert.Equal(1, executions);
    }

    [Fact]
    public void LegacyAndNullMetadataRetainAllFallbacks()
    {
        var legacy = Action("church.attend");
        Assert.Same(ActionPresentationMetadata.Empty, legacy.Presentation);
        Assert.False(ActionPresentationPolicy.Resolve(legacy).ShowInPrimaryActionList);
        Assert.Equal("⛪", ActionEmojiMap.GetEmoji(legacy));
        Assert.Equal(ActionCategory.Personal, Assert.Single(ActionPresentationPolicy.GetCategories(legacy)));
        var nullMetadata = new GameActionDefinition
        {
            Id = "career.future", Label = "Legacy", Description = "Legacy", Presentation = null!,
            Execute = _ => new GameActionResult(true)
        };
        Assert.Equal(ActionCategory.Career, Assert.Single(ActionPresentationPolicy.GetCategories(nullMetadata)));
        Assert.Equal("💼", ActionEmojiMap.GetEmoji(nullMetadata));
        Assert.Equal(ActionCategory.Personal,
            Assert.Single(ActionPresentationPolicy.GetCategories(Action("unknown.legacy"))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MissingEmojiFallsBackWithoutDiscardingExplicitCategories(string? emoji)
    {
        var definition = Action("career.work_harder", new()
        {
            Emoji = emoji, Categories = [ActionPresentationCategories.Family]
        });
        Assert.Equal("💼", ActionEmojiMap.GetEmoji(definition));
        Assert.Equal(ActionCategory.Family, Assert.Single(ActionPresentationPolicy.GetCategories(definition)));
        Assert.Null(ActionPresentationPolicy.Resolve(definition).AdjacencyGroup);
    }

    [Fact]
    public void CategoriesAreCaseInsensitiveAndUnknownCategoryIdsRemainReachable()
    {
        Assert.Equal(ActionCategory.Skills, Assert.Single(ActionPresentationPolicy.GetCategories(
            Action("career.custom", new() { Categories = ["SKILLS", "skills"] }))));
        Assert.Equal(ActionCategory.Personal, Assert.Single(ActionPresentationPolicy.GetCategories(
            Action("career.custom", new() { Categories = ["plugin.unrecognized-category"] }))));
    }

    [Fact]
    public void MultipleGroupsUseEarliestSourcePositionWithStableTiesAndStablePlaceLast()
    {
        var source = new[]
        {
            Action("end.first", new() { PlaceLast = true }),
            Action("b.high", new() { AdjacencyGroup = "B", GroupOrder = 20 }),
            Action("unrelated.first"),
            Action("a.high", new() { AdjacencyGroup = "A", GroupOrder = 100 }),
            Action("b.low", new() { AdjacencyGroup = "b", GroupOrder = 10 }),
            Action("unrelated.second"),
            Action("a.first-tie", new() { AdjacencyGroup = "A", GroupOrder = 0 }),
            Action("a.second-tie", new() { AdjacencyGroup = "a", GroupOrder = 0 }),
            Action("end.second", new() { PlaceLast = true })
        };
        var before = source.ToArray();
        var ordered = ActionPresentationPolicy.Order(source);
        Assert.Equal(new[] { "b.low", "b.high", "unrelated.first", "a.first-tie", "a.second-tie", "a.high",
            "unrelated.second", "end.first", "end.second" }, ordered.Select(action => action.Id));
        Assert.Equal(before, source);
        Assert.All(ordered, item => Assert.Contains(source, original => ReferenceEquals(original, item)));
    }

    [Fact]
    public void ExplicitAndLegacyGroupMembersCanBeMixed()
    {
        var source = new[] { Action("loan.give"), Action("unrelated"), Action("plugin.borrow", new()
        { AdjacencyGroup = ActionPresentationGroups.Loans, GroupOrder = 5 }), Action("loan.take") };
        Assert.Equal(new[] { "plugin.borrow", "loan.take", "loan.give", "unrelated" },
            ActionPresentationPolicy.Order(source).Select(action => action.Id));
    }

    [Fact]
    public void GenericAnchorsPreserveFirstSearchAndFollowerOrder()
    {
        var source = new[]
        {
            Action("follower.one", new() { PlaceAfterAnchor = "custom.search" }),
            Action("other"), Action("second", new() { PlacementAnchor = "CUSTOM.SEARCH" }),
            Action("first", new() { PlacementAnchor = "custom.search" }),
            Action("follower.two", new() { PlaceAfterAnchor = "custom.search" }), Action("turn.pass")
        };
        Assert.Equal(new[] { "other", "second", "follower.one", "follower.two", "first", "turn.pass" },
            ActionPresentationPolicy.Order(source).Select(action => action.Id));
        Assert.Equal(new[] { "other", "follower.one", "follower.two", "turn.pass" },
            ActionPresentationPolicy.Order(source.Where(action => action.Presentation.PlacementAnchor is null).ToArray())
                .Select(action => action.Id));
    }

    [Fact]
    public void PrimaryVisibilityUsesMetadataAndPassIsLastRegardlessOfFilters()
    {
        using var f = new ActionPanelFixture();
        f.Actions.Register(Action("church.attend", new()
        { Emoji = "🧭", Categories = [ActionPresentationCategories.Skills] }));
        f.Actions.Register(Action("plugin.hidden", new()
        { Emoji = "🧭", Categories = [ActionPresentationCategories.Skills], ShowInPrimaryActionList = false }));
        f.Actions.Register(Action("turn.pass", new() { Emoji = "⏭️", PlaceLast = true }));
        f.Select();
        Assert.Contains(f.View.AvailableActions, action => action.Id == "church.attend" && action.Label == "🧭 church.attend");
        Assert.DoesNotContain(f.View.AvailableActions, action => action.Id == "plugin.hidden");
        Assert.Equal("turn.pass", f.View.AvailableActions.Last().Id);
        // Disable every populated filter; Pass remains the sole action.
        foreach (var filter in f.View.ActionFilters.ToArray()) filter.ToggleCommand.Execute(null);
        Assert.Equal("turn.pass", Assert.Single(f.View.AvailableActions).Id);
    }

    [Fact]
    public void ExplicitlyUnfilteredActionsDoNotDisappearFromThePanel()
    {
        using var f = new ActionPanelFixture();
        f.Actions.Register(Action("plugin.unfiltered", new() { Emoji = "🧭" }));
        f.Select();
        foreach (var filter in f.View.ActionFilters.ToArray()) filter.ToggleCommand.Execute(null);
        Assert.Equal("plugin.unfiltered", Assert.Single(f.View.AvailableActions).Id);
    }

    [Fact]
    public void DynamicProviderPresentationIsReReadWithoutChangingActionIdentity()
    {
        using var f = new ActionPanelFixture();
        var visible = true;
        f.Actions.RegisterDynamicProvider((_, _) => [Action("plugin.dynamic", new()
        {
            Emoji = "🧭", Categories = [ActionPresentationCategories.Family], ShowInPrimaryActionList = visible
        })]);
        f.Select();
        Assert.Contains(f.View.AvailableActions, action => action.Id == "plugin.dynamic" && action.Label.StartsWith("🧭"));
        visible = false;
        f.Select();
        Assert.DoesNotContain(f.View.AvailableActions, action => action.Id == "plugin.dynamic");
    }
}
