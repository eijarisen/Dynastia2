using Dynastia.Contracts;
using Dynastia.Core.Events;

namespace Dynastia.Core.Tests;

public sealed class EventPresentationRegistryTests
{
    [Fact]
    public void ExactMappingWinsOverPrefixCaseInsensitively()
    {
        var registry = new EventPresentationRegistry();
        registry.RegisterPrefix(
            "career.",
            new EventPresentationMetadata { Emoji = "💼" },
            "career");
        registry.Register(
            "career.fired",
            new EventPresentationMetadata { Emoji = "💥" },
            "career");

        Assert.Equal("💥", registry.Resolve("CAREER.FIRED").Emoji);
        Assert.Equal("💼", registry.Resolve("career.unknown").Emoji);
    }

    [Fact]
    public void LongestPrefixWinsRegardlessOfRegistrationOrder()
    {
        var registry = new EventPresentationRegistry();
        registry.RegisterPrefix(
            "family.relation.",
            new EventPresentationMetadata { Emoji = "❤️" },
            "specific");
        registry.RegisterPrefix(
            "family.",
            new EventPresentationMetadata { Emoji = "👪" },
            "general");

        Assert.Equal(
            "❤️",
            registry.Resolve("family.relation.improved").Emoji);
    }

    [Fact]
    public void UnknownEventUsesPinDefault()
    {
        Assert.Equal(
            "📌",
            new EventPresentationRegistry()
                .Resolve("external.unregistered")
                .Emoji);
    }

    [Fact]
    public void DuplicateOwnershipFailsWithKeyAndOwners()
    {
        var registry = new EventPresentationRegistry();
        registry.Register(
            "shared.event",
            new EventPresentationMetadata { Emoji = "1" },
            "plugin.one");

        var error = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(
                "shared.event",
                new EventPresentationMetadata { Emoji = "2" },
                "plugin.two"));

        Assert.Contains("shared.event", error.Message);
        Assert.Contains("plugin.one", error.Message);
        Assert.Contains("plugin.two", error.Message);
    }

    [Fact]
    public void LegacyCataloguePreservesBroadFamilyAndLifeFallbacks()
    {
        var registry = new EventPresentationRegistry();
        LegacyEventPresentationCatalog.Register(registry);

        Assert.Equal("👪", registry.Resolve("family_unknown").Emoji);
        Assert.Equal("📜", registry.Resolve("life.unknown").Emoji);
    }

    [Fact]
    public void SameOwnerCanRepeatIdenticalRegistrationButNotChangeIt()
    {
        var registry = new EventPresentationRegistry();
        var metadata = new EventPresentationMetadata { Emoji = "✨" };

        registry.Register("career.promotion", metadata, "career");
        registry.Register("career.promotion", metadata, "career");

        Assert.Equal("✨", registry.Resolve("career.promotion").Emoji);

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(
                "career.promotion",
                new EventPresentationMetadata { Emoji = "💼" },
                "career"));
    }
}
