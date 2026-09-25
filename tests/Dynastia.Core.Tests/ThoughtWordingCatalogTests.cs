using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Thoughts;

namespace Dynastia.Core.Tests;

public sealed class ThoughtWordingCatalogTests
{
    [Fact]
    public void CoreCatalogueLoadsMandatoryFallbackAndSplitKeys()
    {
        var definitions = ThoughtWordingCatalog.LoadCore(
            new JsonGameDataService(RepositoryFiles.Path("data")));

        Assert.NotEmpty(definitions["fallback"].AdultNormal);
        Assert.True(definitions.ContainsKey("marriage.satisfaction.broke"));
        Assert.True(definitions.ContainsKey("improvement.fertility_restored"));
        Assert.True(definitions.ContainsKey("support.success.donor.good"));
        Assert.True(definitions.ContainsKey("craft.work.strong"));
        Assert.True(definitions.ContainsKey("farming.work.poor"));
    }

    [Fact]
    public void DuplicateWordingOwnershipIsRejectedWithBothOwners()
    {
        var registry = new ThoughtWordingRegistry();
        var catalogue = new Dictionary<string, ThoughtWordingDefinition>
        {
            ["shared.key"] = new()
            {
                AdultNormal = ["Shared text."]
            }
        };

        registry.RegisterCatalogue("plugin.one", catalogue);

        var error = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterCatalogue("plugin.two", catalogue));

        Assert.Contains("shared.key", error.Message);
        Assert.Contains("plugin.one", error.Message);
        Assert.Contains("plugin.two", error.Message);
    }

    [Fact]
    public void MalformedPlaceholderIsRejectedAtRegistration()
    {
        var registry = new ThoughtWordingRegistry();

        var error = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterCatalogue(
                "tests",
                new Dictionary<string, ThoughtWordingDefinition>
                {
                    ["bad"] = new()
                    {
                        AdultNormal = ["I miss {relation."]
                    }
                }));

        Assert.Contains("malformed placeholder", error.Message);
    }

    [Fact]
    public void MissingRequiredContextFailsInsteadOfSilentlyFallingBack()
    {
        var registry = new ThoughtWordingRegistry();
        registry.RegisterCatalogue(
            "tests",
            new Dictionary<string, ThoughtWordingDefinition>
            {
                ["fallback"] = new()
                {
                    AdultNormal = ["Fallback."]
                },
                ["needs.context"] = new()
                {
                    Child = ["I miss {relation}."]
                }
            });

        var state = new GameState();
        var person = state.CreatePerson(
            "Anna",
            "Test",
            10,
            Guid.Parse("00000000-0000-0000-0000-000000000051"));

        var renderer = new ThoughtPhraseRenderer(
            DispatchProxy.Create<IStatsService, EmptyDispatchProxy>(),
            registry);

        var candidate = new ThoughtCandidate(
            "missing-context",
            "family",
            "missing-context",
            50,
            ThoughtMoodIds.Sad,
            "👪",
            ThoughtSalienceTraits.None,
            "test",
            null,
            "needs.context");

        var error = Assert.Throws<InvalidOperationException>(() =>
            renderer.Render(person, candidate, "dynasty", 1800));

        Assert.Contains("relation", error.Message);
        Assert.Contains("needs.context", error.Message);
    }

    [Fact]
    public void VoiceFallbackPrefersDefinitionAdultNormalBeforeGlobalVoice()
    {
        var registry = new ThoughtWordingRegistry();
        registry.RegisterCatalogue(
            "tests",
            new Dictionary<string, ThoughtWordingDefinition>
            {
                ["fallback"] = new()
                {
                    Child = ["Global child."],
                    AdultNormal = ["Global adult."]
                },
                ["specific"] = new()
                {
                    AdultNormal = ["Specific adult."]
                }
            });

        var state = new GameState();
        var person = state.CreatePerson(
            "Anna",
            "Test",
            10,
            Guid.Parse("00000000-0000-0000-0000-000000000052"));

        var renderer = new ThoughtPhraseRenderer(
            DispatchProxy.Create<IStatsService, EmptyDispatchProxy>(),
            registry);

        var candidate = new ThoughtCandidate(
            "fallback-order",
            "test",
            "fallback-order",
            50,
            ThoughtMoodIds.Neutral,
            "💭",
            ThoughtSalienceTraits.None,
            "test",
            null,
            "specific");

        Assert.Equal(
            "Specific adult.",
            renderer.Render(person, candidate, "dynasty", 1800));
    }

    public class EmptyDispatchProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
                return null;

            if (targetMethod.ReturnType.IsValueType)
                return Activator.CreateInstance(targetMethod.ReturnType);

            if (targetMethod.ReturnType.IsGenericType)
            {
                var definition = targetMethod.ReturnType.GetGenericTypeDefinition();
                if (definition == typeof(IReadOnlyList<>)
                    || definition == typeof(IEnumerable<>))
                {
                    return Array.CreateInstance(
                        targetMethod.ReturnType.GetGenericArguments()[0],
                        0);
                }
            }

            return null;
        }
    }
}
