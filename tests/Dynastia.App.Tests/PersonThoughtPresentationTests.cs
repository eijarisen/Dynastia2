using Dynastia.App.ViewModels;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;
using Avalonia.Media;

namespace Dynastia.App.Tests;

public sealed class PersonThoughtPresentationTests
{
    [Fact]
    public void NeutralThoughtUsesPersonStatusWhileTopicRemainsSeparateOnFamilyCard()
    {
        var state = new GameState { Year = 1900 };
        var person = state.CreatePerson(
            "Jan",
            "Test",
            30,
            Guid.Parse("10000000-0000-0000-0000-000000000001"));
        person.Tags.Add("state.alive");
        var thoughts = new ThoughtStub(new PersonThoughtSnapshot(
            1900,
            "farming.work",
            "Farming",
            "The fields need attention.",
            ThoughtMoodIds.Neutral,
            "🙂",
            "🌾",
            50,
            "farming"));

        var card = CreateCard(person, thoughts);

        Assert.Equal("🙂", card.AvatarText);
        Assert.Equal("🌾", card.ThoughtTopicEmoji);
        Assert.True(card.HasThoughtTopicEmoji);
        Assert.Contains("fields need attention", card.TooltipThoughtText);
    }

    [Fact]
    public void NonNeutralMoodCanDriveStatusButTopicNeverDoes()
    {
        var state = new GameState { Year = 1900 };
        var person = state.CreatePerson(
            "Jan",
            "Test",
            30,
            Guid.Parse("10000000-0000-0000-0000-000000000002"));
        person.Tags.Add("state.alive");
        var thoughts = new ThoughtStub(new PersonThoughtSnapshot(
            1900,
            "finance.debt",
            "Finances",
            "I am worried about our debts.",
            ThoughtMoodIds.Concerned,
            "😟",
            "💰",
            80,
            "loans"));

        Assert.Equal(
            "😟",
            PersonEmojiResolver.GetPersonEmoji(
                person,
                null,
                null,
                null,
                null,
                null,
                thoughts));
    }

    [Fact]
    public void DeadAndImprisonedStatesOverrideThoughtMood()
    {
        var state = new GameState { Year = 1900 };
        var person = state.CreatePerson(
            "Jan",
            "Test",
            30,
            Guid.Parse("10000000-0000-0000-0000-000000000003"));
        person.Tags.Add("state.alive");
        var thoughts = new ThoughtStub(new PersonThoughtSnapshot(
            1900,
            "family.good_news",
            "Family",
            "This is wonderful.",
            ThoughtMoodIds.Happy,
            "😄",
            "👪",
            50,
            "family"));

        person.Tags.Add("state.imprisoned");
        Assert.Equal(
            "⛓️",
            PersonEmojiResolver.GetPersonEmoji(
                person, null, null, null, null, null, thoughts));

        person.Tags.Remove("state.imprisoned");
        person.Tags.Add("state.dead");
        Assert.Equal(
            "💀",
            PersonEmojiResolver.GetPersonEmoji(
                person, null, null, null, null, null, thoughts));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(7, false)]
    [InlineData(7, true)]
    [InlineData(14, true)]
    [InlineData(30, true)]
    [InlineData(75, false)]
    public void NeutralFallbackUsesGenericYellowStatusFace(
        int age,
        bool female)
    {
        var state = new GameState { Year = 1900 };
        var person = state.CreatePerson(
            "Person",
            "Test",
            age,
            Guid.NewGuid());
        person.Tags.Add("state.alive");
        if (female)
            person.Tags.Add("sex.female");

        Assert.Equal(
            "🙂",
            PersonEmojiResolver.GetPersonEmoji(
                person, null, null, null, null, null));
    }

    [Fact]
    public void UnderFiveHealthOverridesRemainAheadOfThoughtPresentation()
    {
        var state = new GameState { Year = 1900 };
        var person = state.CreatePerson(
            "Jan",
            "Test",
            3,
            Guid.Parse("10000000-0000-0000-0000-000000000004"));
        person.Tags.Add("state.alive");

        Assert.Equal(
            "🤒",
            PersonEmojiResolver.GetPersonEmoji(
                person,
                null,
                new HealthStub(new HealthSnapshot(
                    80,
                    100,
                    [new HealthConditionInfo("cold", "Cold", "seasonal", -5, 1)])),
                null,
                null,
                null));

        Assert.Equal(
            "😣",
            PersonEmojiResolver.GetPersonEmoji(
                person,
                null,
                new HealthStub(new HealthSnapshot(
                    80,
                    100,
                    [new HealthConditionInfo("cancer", "Cancer", "terminal", -10, null)])),
                null,
                null,
                null));
    }

    [Fact]
    public void ZeroStressIsDisplayedWithoutDecimalsAndUsesGreenPresentation()
    {
        var state = new GameState { Year = 1900 };
        var person = state.CreatePerson(
            "Jan",
            "Test",
            30,
            Guid.Parse("10000000-0000-0000-0000-000000000005"));
        person.Tags.Add("state.alive");

        var card = CreateCard(
            person,
            thoughts: null,
            stress: new StressStub(new StressSnapshot(0, [])));

        Assert.Equal("Stress: 0/100", card.StressTooltipText);
        Assert.Equal(
            Color.Parse("#43A047"),
            Assert.IsType<SolidColorBrush>(card.StressBrush).Color);
    }

    private static FamilyMemberCardViewModel CreateCard(
        IPerson person,
        IThoughtService? thoughts,
        IStressService? stress = null) =>
        new(
            person,
            null,
            null,
            stress,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            thoughts,
            null,
            false,
            false,
            _ => { });

    private sealed class ThoughtStub(PersonThoughtSnapshot thought) : IThoughtService
    {
        public PersonThoughtSnapshot? GetCurrentThought(IPerson person) => thought;
        public void EnsureCurrentThoughts() { }
        public void ResetAfterLoad() { }
    }

    private sealed class HealthStub(HealthSnapshot snapshot) : IHealthService
    {
        public void EnsureHealth(IPerson person) { }
        public HealthSnapshot GetHealth(IPerson person) => snapshot;
        public void SetHealth(IPerson person, double value) => throw new NotSupportedException();
        public void ChangeHealth(IPerson person, double amount) => throw new NotSupportedException();
        public void ChangeHealthUnclamped(IPerson person, double amount) => throw new NotSupportedException();
        public bool HasCondition(IPerson person, string conditionId) => false;
        public bool AddCondition(IPerson person, string conditionId) => throw new NotSupportedException();
        public bool AddCondition(IPerson person, string conditionId, int year) => throw new NotSupportedException();
        public bool RemoveCondition(IPerson person, string conditionId) => throw new NotSupportedException();
    }

    private sealed class StressStub(StressSnapshot snapshot) : IStressService
    {
        public StressSnapshot GetStress(IPerson person) => snapshot;
    }
}
