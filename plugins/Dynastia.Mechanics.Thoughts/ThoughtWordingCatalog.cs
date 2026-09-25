using System.Text.Json;
using System.Text.RegularExpressions;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class ThoughtWordingRegistry : IThoughtWordingRegistry
{
    private sealed record Registration(
        string OwnerId,
        ThoughtWordingDefinition Definition);

    private readonly Dictionary<string, Registration> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public void RegisterCatalogue(
        string ownerId,
        IReadOnlyDictionary<string, ThoughtWordingDefinition> definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (var pair in definitions)
        {
            var key = pair.Key?.Trim() ?? string.Empty;
            if (key.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Thought wording owner '{ownerId}' attempted to register an empty wording key.");
            }

            ThoughtWordingCatalog.ValidateDefinition(
                key,
                pair.Value,
                ownerId);

            if (_definitions.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Thought wording key '{key}' is already owned by '{existing.OwnerId}' and cannot also be registered by '{ownerId}'.");
            }

            _definitions[key] =
                new Registration(ownerId, pair.Value);
        }
    }

    public bool TryGet(
        string wordingKey,
        out ThoughtWordingDefinition definition)
    {
        if (_definitions.TryGetValue(
                wordingKey,
                out var registration))
        {
            definition = registration.Definition;
            return true;
        }

        definition = null!;
        return false;
    }
}

internal static partial class ThoughtWordingCatalog
{
    private static readonly HashSet<string> VoiceProperties =
        new(StringComparer.Ordinal)
        {
            nameof(ThoughtWordingDefinition.Child),
            nameof(ThoughtWordingDefinition.Adolescent),
            nameof(ThoughtWordingDefinition.AdultRough),
            nameof(ThoughtWordingDefinition.AdultNormal),
            nameof(ThoughtWordingDefinition.AdultElaborate)
        };

    [GeneratedRegex(@"\{[A-Za-z][A-Za-z0-9_]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    public static IReadOnlyDictionary<string, ThoughtWordingDefinition> LoadCore(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        const string path = "Thoughts/core_thought_phrases.json";
        using var document = JsonDocument.Parse(data.ReadText(path));

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Thought wording catalogue '{path}' must contain a JSON object.");
        }

        var result =
            new Dictionary<string, ThoughtWordingDefinition>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var entry in document.RootElement.EnumerateObject())
        {
            if (!result.TryAdd(
                    entry.Name,
                    ParseDefinition(entry.Name, entry.Value, path)))
            {
                throw new InvalidOperationException(
                    $"Thought wording catalogue '{path}' contains duplicate key '{entry.Name}'.");
            }
        }

        if (!result.TryGetValue("fallback", out var fallback)
            || fallback.AdultNormal.Count == 0)
        {
            throw new InvalidOperationException(
                $"Thought wording catalogue '{path}' must define fallback.AdultNormal with at least one phrase.");
        }

        return result;
    }

    internal static void ValidateDefinition(
        string wordingKey,
        ThoughtWordingDefinition definition,
        string ownerId)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var lists = new[]
        {
            definition.Child,
            definition.Adolescent,
            definition.AdultRough,
            definition.AdultNormal,
            definition.AdultElaborate
        };

        if (lists.All(list => list.Count == 0))
        {
            throw new InvalidOperationException(
                $"Thought wording '{wordingKey}' registered by '{ownerId}' has no phrase variants.");
        }

        foreach (var phrase in lists.SelectMany(list => list))
        {
            ValidatePhrase(wordingKey, phrase, ownerId);
        }
    }

    internal static IReadOnlyList<string> GetPlaceholders(string phrase) =>
        PlaceholderRegex()
            .Matches(phrase)
            .Select(match => match.Value[1..^1])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static ThoughtWordingDefinition ParseDefinition(
        string wordingKey,
        JsonElement element,
        string ownerId)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Thought wording '{wordingKey}' in '{ownerId}' must be an object.");
        }

        var voices =
            new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (!VoiceProperties.Contains(property.Name))
            {
                throw new InvalidOperationException(
                    $"Thought wording '{wordingKey}' in '{ownerId}' uses unknown voice '{property.Name}'.");
            }

            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"Thought wording '{wordingKey}.{property.Name}' in '{ownerId}' must be an array.");
            }

            var phrases = property.Value
                .EnumerateArray()
                .Select(item =>
                    item.ValueKind == JsonValueKind.String
                        ? item.GetString() ?? string.Empty
                        : throw new InvalidOperationException(
                            $"Thought wording '{wordingKey}.{property.Name}' in '{ownerId}' contains a non-string phrase."))
                .ToList();

            if (phrases.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Thought wording '{wordingKey}.{property.Name}' in '{ownerId}' must not be empty.");
            }

            voices[property.Name] = phrases;
        }

        var definition = new ThoughtWordingDefinition
        {
            Child = Get(voices, nameof(ThoughtWordingDefinition.Child)),
            Adolescent = Get(voices, nameof(ThoughtWordingDefinition.Adolescent)),
            AdultRough = Get(voices, nameof(ThoughtWordingDefinition.AdultRough)),
            AdultNormal = Get(voices, nameof(ThoughtWordingDefinition.AdultNormal)),
            AdultElaborate = Get(voices, nameof(ThoughtWordingDefinition.AdultElaborate))
        };

        ValidateDefinition(wordingKey, definition, ownerId);
        return definition;
    }

    private static IReadOnlyList<string> Get(
        IReadOnlyDictionary<string, IReadOnlyList<string>> voices,
        string voice) =>
        voices.TryGetValue(voice, out var phrases)
            ? phrases
            : [];

    private static void ValidatePhrase(
        string wordingKey,
        string phrase,
        string ownerId)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            throw new InvalidOperationException(
                $"Thought wording '{wordingKey}' registered by '{ownerId}' contains an empty phrase.");
        }

        var withoutValidPlaceholders = PlaceholderRegex().Replace(phrase, string.Empty);
        if (withoutValidPlaceholders.Contains('{')
            || withoutValidPlaceholders.Contains('}'))
        {
            throw new InvalidOperationException(
                $"Thought wording '{wordingKey}' registered by '{ownerId}' contains a malformed placeholder in '{phrase}'.");
        }
    }
}
