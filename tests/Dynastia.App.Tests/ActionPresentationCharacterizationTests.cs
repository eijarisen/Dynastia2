using Dynastia.App.ViewModels;
using Dynastia.Contracts;

namespace Dynastia.App.Tests;

public sealed class ActionPresentationCharacterizationTests
{
    [Theory]
    [InlineData("wellbeing.recover", "Personal,Career")]
    [InlineData("career.ask_to_recover", "Personal,Career,Family,Finances")]
    [InlineData("career.work_harder", "Career,Finances")]
    [InlineData("education.private_tutor", "Career,Family")]
    [InlineData("education.help_learning", "Career,Family")]
    [InlineData("education.get_education", "Career")]
    [InlineData("justice.ask_to_quit_crime", "Career,Family")]
    [InlineData("justice.commit_crime", "Career")]
    [InlineData("justice.leave_life_of_crime", "Career")]
    [InlineData("career.help_seek_employment", "Career,Family")]
    [InlineData("career.help_find_better_job", "Career,Family")]
    [InlineData("career.ask_to_quit", "Career,Family")]
    [InlineData("career.future", "Career")]
    [InlineData("wellbeing.therapy", "Personal,Family")]
    [InlineData("wellbeing.heal_relative", "Personal")]
    [InlineData("stats.improve_strength", "Personal")]
    [InlineData("relationship.find_spouse", "Family")]
    [InlineData("reproduction.try_for_baby", "Family")]
    [InlineData("childhood.raise_child", "Family")]
    [InlineData("family.adopt_polish_surname", "Family")]
    [InlineData("family_support.ask_child", "Family,Finances")]
    [InlineData("craft.teach.carpentry", "Family,Skills")]
    [InlineData("craft.start.carpentry", "Career")]
    [InlineData("craft.stop_occupation", "Career")]
    [InlineData("farming.buy_farmland", "Finances")]
    [InlineData("loan.take", "Finances")]
    [InlineData("household.buy_house", "Finances")]
    [InlineData("household.give_house_to_son", "Family,Finances")]
    [InlineData("household.hire_nanny", "Family")]
    [InlineData("household.ask_move_out", "Family")]
    [InlineData("plugin.future_action", "Personal")]
    [InlineData("turn.pass", "")]
    public void CategoriesMatchCurrentPolicyIncludingCaseInsensitivity(string id, string expected)
    {
        var categories = expected.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(Enum.Parse<ActionCategory>).OrderBy(category => category).ToArray();
        Assert.Equal(categories, ActionPresentationPolicy.GetCategories(id).OrderBy(category => category));
        Assert.Equal(categories, ActionPresentationPolicy.GetCategories(id.ToUpperInvariant()).OrderBy(category => category));
        Assert.Equal(categories, ActionPresentationPolicy.GetCategories(Action(id)).OrderBy(category => category));
        var metadata = new ActionPresentationMetadata
        {
            Categories = categories.Select(category => category.ToString().ToLowerInvariant()).ToArray()
        };
        Assert.Equal(categories, ActionPresentationPolicy.GetCategories(Action(id, metadata)).OrderBy(category => category));
    }

    public static IEnumerable<object[]> OrderingGroups()
    {
        yield return new object[] { new[] { "wellbeing.recover", "career.work_harder", "career.seek_employment",
            "career.find_another_job", "career.quit_job", "justice.commit_crime",
            "justice.leave_life_of_crime", "justice.ask_to_quit_crime" } };
        yield return new object[] { new[] { "loan.take", "loan.give" } };
        yield return new object[] { new[] { "household.buy_house", "household.sell_house",
            "farming.buy_farmland", "farming.sell_farmland", "heirloom.sell" } };
        yield return new object[] { new[] { "reproduction.try_for_baby", "relationship.repair_marriage",
            "relationship.divorce_spouse" } };
        yield return new object[] { new[] { "wellbeing.heal_relative", "wellbeing.therapy", "education.get_education",
            "stats.improve_strength", "stats.improve_intellect", "stats.improve_immunity", "stats.improve_appeal",
            "stats.improve_longevity", "stats.improve_fertility", "personality.religious_study" } };
    }

    [Theory]
    [MemberData(nameof(OrderingGroups))]
    public void RelatedActionsStayContiguousAtFirstGroupPositionWithoutMutatingInput(string[] group)
    {
        var inputIds = new[] { "turn.pass", "custom.before" }
            .Concat(group.Reverse().SelectMany(id => new[] { id, "custom.after." + id })).ToArray();
        var input = inputIds.Select(Action).ToArray();
        var ordered = ActionPresentationPolicy.Order(input);
        var expected = new[] { "custom.before" }.Concat(group)
            .Concat(group.Reverse().Select(id => "custom.after." + id)).Append("turn.pass");
        Assert.Equal(expected, ordered.Select(action => action.Id));
        Assert.Equal(inputIds, input.Select(action => action.Id));
        Assert.All(ordered, item => Assert.Contains(input, original => ReferenceEquals(original, item)));

        var metadataInput = inputIds.Select(id => Action(id, id == "turn.pass"
            ? new() { PlaceLast = true }
            : group.Contains(id)
                ? new() { AdjacencyGroup = "test.related", GroupOrder = Array.IndexOf(group, id) }
                : null)).ToArray();
        Assert.Equal(expected, ActionPresentationPolicy.Order(metadataInput).Select(action => action.Id));
    }

    [Theory]
    [InlineData("career.seek_employment")]
    [InlineData("career.help_seek_employment")]
    [InlineData("career.help_find_better_job")]
    public void FamilyConnectionsImmediatelyFollowTheFirstJobSearch(string search)
    {
        var input = new[] { "career.use_family_connections", "custom.first", search, "custom.last", "turn.pass" };
        var ordered = ActionPresentationPolicy.Order(input.Select(Action).ToArray());
        Assert.Equal(new[] { "custom.first", search, "career.use_family_connections", "custom.last", "turn.pass" },
            ordered.Select(action => action.Id));
    }

    [Fact]
    public void MissingGroupsAndUnknownActionsRetainOrderWithPassAtTheEnd()
    {
        var input = new[] { "TURN.PASS", "custom.b", "career.use_family_connections", "custom.a" };
        Assert.Equal(new[] { "custom.b", "custom.a", "career.use_family_connections", "TURN.PASS" },
            ActionPresentationPolicy.Order(input.Select(Action).ToArray()).Select(action => action.Id));
        Assert.Empty(ActionPresentationPolicy.Order(Array.Empty<GameActionDefinition>()));
    }

    [Theory]
    [InlineData("craft.start.future", "🛠️")]
    [InlineData("craft.teach.future", "🛠️")]
    [InlineData("craft.future", "🛠️")]
    [InlineData("household.move.future", "🚚")]
    [InlineData("household.future", "🏠")]
    [InlineData("career.future", "💼")]
    [InlineData("relationship.future", "💞")]
    [InlineData("family_relations.future", "👪")]
    [InlineData("loan.future", "🏦")]
    [InlineData("farming.future", "🌾")]
    [InlineData("education.future", "🎓")]
    [InlineData("church.future", "⛪")]
    [InlineData("community.future", "🏛️")]
    [InlineData("unknown.future", "⚙️")]
    [InlineData("turn.pass", "⏭️")]
    [InlineData("justice.ask_to_quit_crime", "🛑")]
    [InlineData("wellbeing.recover", "🧘")]
    [InlineData("family_support.ask_child", "🙏")]
    public void EmojiLookupAndFormattingPreserveExplicitValuesAndPrefixFallbacks(string id, string emoji)
    {
        Assert.Equal(emoji, ActionEmojiMap.GetEmoji(id));
        Assert.Equal(emoji, ActionEmojiMap.GetEmoji(id.ToUpperInvariant()));
        Assert.Equal($"{emoji} Label", ActionEmojiMap.Format(id, "Label"));
        var metadataAction = Action(id, new() { Emoji = emoji });
        Assert.Equal(emoji, ActionEmojiMap.GetEmoji(metadataAction));
        Assert.Equal($"{emoji} Label", ActionEmojiMap.Format(metadataAction, "Label"));
    }

    [Fact]
    public void AvailableActionUsesContextualLabelAndExecutesExactlyOnce()
    {
        var executions = 0;
        var definition = Action("career.seek_employment");
        var categories = ActionPresentationPolicy.GetCategories(definition.Id);
        var item = new AvailableActionViewModel(definition, categories, () => executions++, "Help brother find work");
        Assert.Equal("Help brother find work", item.RawLabel);
        Assert.Equal("✅ Help brother find work", item.Label);
        Assert.Same(categories, item.Categories);
        item.ExecuteCommand.Execute(null);
        Assert.Equal(1, executions);
    }

    internal static GameActionDefinition Action(string id) => Action(id, null);

    internal static GameActionDefinition Action(string id, ActionPresentationMetadata? presentation) => new()
    {
        Id = id, Label = id, Description = id,
        Presentation = presentation ?? ActionPresentationMetadata.Empty,
        IsAvailable = _ => true,
        Execute = _ => new GameActionResult(true, "Executed")
    };
}
