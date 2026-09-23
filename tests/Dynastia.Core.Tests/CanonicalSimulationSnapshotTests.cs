using Dynastia.Contracts;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;

namespace Dynastia.Core.Tests;

public sealed class CanonicalSimulationSnapshotTests
{
    [Fact]
    public void CanonicalSnapshotIgnoresTagAndDictionaryInsertionOrder()
    {
        var first =
            CreateFixture(
                reversePeople: false,
                reverseMetadata: false);

        var second =
            CreateFixture(
                reversePeople: false,
                reverseMetadata: true);

        Assert.Equal(
            first.Snapshot,
            second.Snapshot);
    }

    [Fact]
    public void CanonicalSnapshotDetectsAuthoritativeOrderAndStateChanges()
    {
        var baseline =
            CreateFixture(
                reversePeople: false,
                reverseMetadata: false);

        var peopleReordered =
            CreateFixture(
                reversePeople: true,
                reverseMetadata: false);

        Assert.NotEqual(
            baseline.Snapshot,
            peopleReordered.Snapshot);

        baseline.State.People[0]
            .Components.Get<TestComponent>()!
            .Value++;

        var componentChanged =
            CanonicalSimulationSnapshot.Capture(
                baseline.State,
                baseline.Events,
                baseline.Queued,
                baseline.Random,
                baseline.State.People[0].Id,
                baseline.State.People[1].Id);

        Assert.NotEqual(
            baseline.Snapshot,
            componentChanged);

        baseline.Random.NextDouble();

        var rngChanged =
            CanonicalSimulationSnapshot.Capture(
                baseline.State,
                baseline.Events,
                baseline.Queued,
                baseline.Random,
                baseline.State.People[0].Id,
                baseline.State.People[1].Id);

        Assert.NotEqual(
            componentChanged,
            rngChanged);
    }

    [Fact]
    public void CanonicalSnapshotDetectsEventAndQueuedActionOrderChanges()
    {
        var fixture =
            CreateFixture(
                reversePeople: false,
                reverseMetadata: false);

        var reversedEvents =
            new GameEventBus();

        foreach (var gameEvent in
            fixture.Events.AllEvents.Reverse())
        {
            reversedEvents.Publish(gameEvent);
        }

        var eventOrderChanged =
            CanonicalSimulationSnapshot.Capture(
                fixture.State,
                reversedEvents,
                fixture.Queued,
                fixture.Random);

        Assert.NotEqual(
            fixture.Snapshot,
            eventOrderChanged);

        var queueOrderChanged =
            CanonicalSimulationSnapshot.Capture(
                fixture.State,
                fixture.Events,
                fixture.Queued.Reverse().ToList(),
                fixture.Random);

        Assert.NotEqual(
            fixture.Snapshot,
            queueOrderChanged);
    }

    private static Fixture CreateFixture(
        bool reversePeople,
        bool reverseMetadata)
    {
        var random =
            new GameRandom(12345);

        var state =
            new GameState(random)
            {
                DynastySurname = "Snapshot",
                Year = 1800,
                StartYear = 1700
            };

        var ids =
            new[]
            {
                Guid.Parse("00000000-0000-0000-0000-000000000101"),
                Guid.Parse("00000000-0000-0000-0000-000000000102")
            };

        var order =
            reversePeople
                ? ids.Reverse()
                : ids;

        foreach (var id in order)
        {
            var person =
                state.CreatePerson(
                    id == ids[0] ? "Adam" : "Jan",
                    "Snapshot",
                    id == ids[0] ? 30 : 20,
                    id);

            if (reverseMetadata)
            {
                person.Tags.Add("tag.z");
                person.Tags.Add("tag.a");
            }
            else
            {
                person.Tags.Add("tag.a");
                person.Tags.Add("tag.z");
            }

            person.Components.Set(
                new TestComponent
                {
                    Value =
                        id == ids[0]
                            ? 7
                            : 9,

                    Data = reverseMetadata
                        ? new Dictionary<string, string>
                        {
                            ["z"] = "2",
                            ["a"] = "1"
                        }
                        : new Dictionary<string, string>
                        {
                            ["a"] = "1",
                            ["z"] = "2"
                        }
                });
        }

        var events =
            new GameEventBus();

        events.Publish(
            new GameEvent
            {
                Type = "event.one",
                Year = 1800,
                SubjectId = ids[0],
                Data = Metadata(reverseMetadata)
            });

        events.Publish(
            new GameEvent
            {
                Type = "event.two",
                Year = 1800,
                SubjectId = ids[1]
            });

        IReadOnlyList<QueuedActionInfo> queued =
        [
            new QueuedActionInfo(
                "action.one",
                "One",
                YearPhase.LifeEvents,
                ids[0],
                ids[0],
                Parameters: Metadata(reverseMetadata)),

            new QueuedActionInfo(
                "action.two",
                "Two",
                YearPhase.LifeEvents,
                ids[1],
                ids[1])
        ];

        var snapshot =
            CanonicalSimulationSnapshot.Capture(
                state,
                events,
                queued,
                random,
                ids[0],
                ids[1]);

        return new Fixture(
            state,
            events,
            queued,
            random,
            snapshot);
    }

    private static IReadOnlyDictionary<string, string> Metadata(
        bool reverse) =>
        reverse
            ? new Dictionary<string, string>
            {
                ["z"] = "2",
                ["a"] = "1"
            }
            : new Dictionary<string, string>
            {
                ["a"] = "1",
                ["z"] = "2"
            };

    [PersistedComponentId("test.snapshot")]
    private sealed class TestComponent
    {
        public int Value { get; set; }

        public Dictionary<string, string> Data { get; set; } = [];
    }

    private sealed record Fixture(
        GameState State,
        GameEventBus Events,
        IReadOnlyList<QueuedActionInfo> Queued,
        GameRandom Random,
        string Snapshot);
}
