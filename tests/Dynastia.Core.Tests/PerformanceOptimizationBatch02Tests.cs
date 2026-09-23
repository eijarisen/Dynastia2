using Dynastia.Contracts;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;

namespace Dynastia.Core.Tests;

public sealed class PerformanceOptimizationBatch02Tests
{
    [Fact]
    public void EventYearIndexPreservesPublicationOrderAndSnapshotSemantics()
    {
        var events = new GameEventBus();
        var first = Event("first", 1900);
        var otherYear = Event("other", 1901);
        var second = Event("second", 1900);

        events.Publish(first);
        events.Publish(otherYear);

        var originalSnapshot =
            events.GetEventsForYear(1900);

        Assert.Single(originalSnapshot);
        Assert.Same(first, originalSnapshot[0]);
        Assert.Same(
            originalSnapshot,
            events.GetEventsForYear(1900));

        events.Publish(second);

        Assert.Single(originalSnapshot);

        var refreshedSnapshot =
            events.GetEventsForYear(1900);

        Assert.Equal(2, refreshedSnapshot.Count);
        Assert.NotSame(originalSnapshot, refreshedSnapshot);
        Assert.Same(first, refreshedSnapshot[0]);
        Assert.Same(second, refreshedSnapshot[1]);

        Assert.Equal(
            new[] { first, otherYear, second },
            events.AllEvents);
    }

    [Fact]
    public void RestoreEventsRebuildsYearIndexWithoutPublishing()
    {
        var events = new GameEventBus();
        var published = 0;

        events.EventPublished +=
            (_, _) => published++;

        var first = Event("first", 1900);
        var second = Event("second", 1901);
        var third = Event("third", 1900);

        events.Publish(Event("discarded", 1899));
        published = 0;

        events.RestoreEvents(
            new[] { first, second, third });

        Assert.Equal(0, published);
        Assert.Equal(
            new[] { first, second, third },
            events.AllEvents);
        Assert.Equal(
            new[] { first, third },
            events.GetEventsForYear(1900));
        Assert.Equal(
            new[] { second },
            events.GetEventsForYear(1901));
    }

    [Fact]
    public void PersonLookupPreservesFirstMatchSemanticsForDuplicateIds()
    {
        var state = new GameState();
        IPersonLookup lookup = state;
        var id = Guid.Parse(
            "00000000-0000-0000-0000-000000000101");

        var first = state.CreatePerson(
            "First",
            "Test",
            30,
            id);

        _ = state.CreatePerson(
            "Second",
            "Test",
            40,
            id);

        Assert.Same(
            first,
            lookup.FindPerson(id));
        Assert.Same(
            first,
            lookup.FindPerson((Guid?)id));

        state.ClearPeople();

        Assert.Null(
            lookup.FindPerson(id));

        var replacement = state.CreatePerson(
            "Replacement",
            "Test",
            20,
            id);

        Assert.Same(
            replacement,
            lookup.FindPerson(id));
    }

    private static GameEvent Event(
        string type,
        int year) =>
        new()
        {
            Type = type,
            Year = year
        };
}
