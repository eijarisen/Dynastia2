using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed partial class ThoughtPhraseRenderer
{
    private readonly IStatsService _stats;

    public ThoughtPhraseRenderer(
        IStatsService stats)
    {
        _stats =
            stats;
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

        var voice =
            ResolveVoice(
                person);

        var variants =
            GetMoralityVariants(
                person,
                candidate,
                voice);

        if (variants.Count == 0)
        {
            variants =
                GetVariants(
                    candidate,
                    voice);
        }

        if (variants.Count == 0)
        {
            variants =
                GetVariants(
                    candidate,
                    ThoughtVoice.AdultNormal);
        }

        if (variants.Count == 0)
        {
            return "Things are pretty ordinary right now.";
        }

        return DeterministicThoughtRandom.Choose(
            variants,
            dynastyKey,
            person.Id.ToString(),
            year.ToString(),
            candidate.Id,
            "thought-wording");
    }


    private static IReadOnlyList<string> GetMoralityVariants(
        IPerson person,
        ThoughtCandidate candidate,
        ThoughtVoice voice)
    {
        if (person.Age < 18
            || !candidate.WordingKey.Equals(
                "support.success",
                StringComparison.OrdinalIgnoreCase)
            || !candidate.Context.TryGetValue(
                "supportRole",
                out var supportRole)
            || !supportRole.Equals(
                "donor",
                StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (person.Tags.Has("morals.good"))
        {
            return voice switch
            {
                ThoughtVoice.AdultRough =>
                    ["Glad I could help family."],

                ThoughtVoice.AdultElaborate =>
                    ["I am glad I was able to help; family obligations matter when they are genuinely needed."],

                _ =>
                    ["I'm glad I could help them."]
            };
        }

        if (person.Tags.Has("morals.evil"))
        {
            return voice switch
            {
                ThoughtVoice.AdultRough =>
                    ["They'd better remember what that cost me."],

                ThoughtVoice.AdultElaborate =>
                    ["They had better appreciate the cost to me; generosity is rarely free."],

                _ =>
                    ["They'd better appreciate what this cost me."]
            };
        }

        return [];
    }

    private ThoughtVoice ResolveVoice(
        IPerson person)
    {
        if (person.Age <= 11)
            return ThoughtVoice.Child;

        if (person.Age <= 17)
            return ThoughtVoice.Adolescent;

        var intellect =
            _stats.GetStats(
                person)
            .FirstOrDefault(
                stat =>
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
