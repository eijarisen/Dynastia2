using System.Text.RegularExpressions;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed partial class ThoughtPhraseRenderer
{
    private readonly IStatsService _stats;
    private readonly IThoughtWordingRegistry _wording;

    public ThoughtPhraseRenderer(
        IStatsService stats,
        IThoughtWordingRegistry wording)
    {
        _stats = stats;
        _wording = wording;
    }

    public string Render(
        IPerson person,
        ThoughtCandidate candidate,
        string dynastyKey,
        int year)
    {
        if (candidate.Context.TryGetValue(
                "literalText",
                out var literalText)
            && !string.IsNullOrWhiteSpace(literalText))
        {
            return literalText;
        }

        if (!_wording.TryGet(candidate.WordingKey, out var definition))
        {
            throw new InvalidOperationException(
                $"Thought wording key '{candidate.WordingKey}' used by candidate '{candidate.Id}' is not registered.");
        }

        if (!_wording.TryGet("fallback", out var fallback))
        {
            throw new InvalidOperationException(
                "Thought wording registry does not contain the mandatory 'fallback' definition.");
        }

        var voice = ResolveVoice(person);
        var variants = ResolveVariants(definition, fallback, voice);

        if (variants.Count == 0)
        {
            throw new InvalidOperationException(
                $"Thought wording key '{candidate.WordingKey}' has no usable variants for voice '{voice}'.");
        }

        var templates = variants
            .Select(template => Interpolate(candidate, template))
            .ToList();

        return DeterministicThoughtRandom.Choose(
            templates,
            dynastyKey,
            person.Id.ToString(),
            year.ToString(),
            candidate.Id,
            "thought-wording");
    }

    private static IReadOnlyList<string> ResolveVariants(
        ThoughtWordingDefinition definition,
        ThoughtWordingDefinition fallback,
        ThoughtVoice voice)
    {
        var exact = GetVoice(definition, voice);
        if (exact.Count > 0)
            return exact;

        if (definition.AdultNormal.Count > 0)
            return definition.AdultNormal;

        var fallbackExact = GetVoice(fallback, voice);
        if (fallbackExact.Count > 0)
            return fallbackExact;

        return fallback.AdultNormal;
    }

    private static IReadOnlyList<string> GetVoice(
        ThoughtWordingDefinition definition,
        ThoughtVoice voice) =>
        voice switch
        {
            ThoughtVoice.Child => definition.Child,
            ThoughtVoice.Adolescent => definition.Adolescent,
            ThoughtVoice.AdultRough => definition.AdultRough,
            ThoughtVoice.AdultElaborate => definition.AdultElaborate,
            _ => definition.AdultNormal
        };

    private static string Interpolate(
        ThoughtCandidate candidate,
        string template)
    {
        return Regex.Replace(
            template,
            @"\{([A-Za-z][A-Za-z0-9_]*)\}",
            match =>
            {
                var key = match.Groups[1].Value;
                if (candidate.Context.TryGetValue(key, out var value))
                    return value;

                throw new InvalidOperationException(
                    $"Thought wording '{candidate.WordingKey}' for candidate '{candidate.Id}' requires context key '{key}'.");
            },
            RegexOptions.CultureInvariant);
    }

    private ThoughtVoice ResolveVoice(
        IPerson person)
    {
        if (person.Age <= 11)
            return ThoughtVoice.Child;

        if (person.Age <= 17)
            return ThoughtVoice.Adolescent;

        var intellect = _stats.GetStats(person)
            .FirstOrDefault(stat =>
                stat.Id.Equals(
                    "intellect",
                    StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?? 3;

        if (intellect <= 1)
            return ThoughtVoice.AdultRough;

        if (intellect >= 5)
            return ThoughtVoice.AdultElaborate;

        return ThoughtVoice.AdultNormal;
    }
}
