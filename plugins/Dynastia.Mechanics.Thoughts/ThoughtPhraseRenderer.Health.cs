using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed partial class ThoughtPhraseRenderer
{
    private static IReadOnlyList<string> HealthVariants(
        ThoughtVoice voice,
        string condition,
        bool terminal)
    {
        if (voice == ThoughtVoice.Child)
        {
            return
                [
                    "I'm really sick. I want to feel better.",
                    "I don't like being this sick."
                ];
        }

        if (voice == ThoughtVoice.Adolescent)
        {
            return terminal
                ? ["Being this sick is frightening.", "I'm scared by how serious this illness is."]
                : ["I'm tired of being this sick.", $"I'm tired of dealing with {condition}."];
        }

        if (voice == ThoughtVoice.AdultRough)
        {
            return terminal
                ? ["I'm very sick. It's bad.", "This illness is serious."]
                : ["Feel awful. Want to get better.", $"This {condition} is awful."];
        }

        if (voice == ThoughtVoice.AdultElaborate)
        {
            return terminal
                ? ["The seriousness of my illness is becoming impossible to ignore.",
                   "My illness has become serious enough that it dominates almost every other concern."]
                : [$"Dealing with {condition} has become increasingly exhausting.",
                   $"The continuing effects of {condition} are becoming difficult to ignore."];
        }

        return terminal
            ? ["I'm seriously ill, and it's frightening.", "The seriousness of this illness worries me."]
            : [$"This {condition} is really wearing me down.", $"I'm tired of dealing with {condition}."];
    }

    private static IReadOnlyList<string> ConditionVariants(
        ThoughtVoice voice,
        string condition,
        string normalSuffix)
    {
        if (voice == ThoughtVoice.Child)
            return ["I don't feel good.", "I wish I felt better."];

        if (voice == ThoughtVoice.Adolescent)
            return [$"I'm tired of dealing with {condition}.", $"{condition} has been bothering me a lot."];

        if (voice == ThoughtVoice.AdultRough)
            return [$"{condition} is bad lately.", $"Sick of this {condition}."];

        if (voice == ThoughtVoice.AdultElaborate)
            return [$"The continuing effects of {condition} are becoming increasingly difficult to ignore.",
                    $"{condition} has become a persistent and frustrating part of daily life."];

        return [$"My {condition} {normalSuffix}.", $"{condition} has been bothering me a lot lately."];
    }

    private static IReadOnlyList<string> ImprovementVariants(
        ThoughtVoice voice,
        string statId,
        string previousValue,
        string newValue)
    {
        var restoredFertility =
            statId.Equals(
                "fertility",
                StringComparison.OrdinalIgnoreCase)
            && previousValue == "0"
            && newValue == "1";

        if (restoredFertility)
        {
            return voice switch
            {
                ThoughtVoice.AdultRough =>
                    ["Treatment worked. Maybe I can have kids now."],

                ThoughtVoice.AdultElaborate =>
                    ["The treatment succeeded; for the first time, having children feels like a genuine possibility."],

                _ =>
                    ["The treatment worked. I may finally be able to have children."]
            };
        }

        return statId.ToLowerInvariant() switch
        {
            "strength" =>
                ["That training really paid off.", "I feel stronger after all that training."],

            "intellect" =>
                ["The intelligence training really sharpened my thinking.", "I can tell the training improved my mind."],

            "immunity" =>
                ["The therapy seems to have strengthened my health.", "I feel more resilient after the treatment."],

            "appeal" =>
                ["I'm pleased with how the surgery turned out.", "The change to my appearance turned out well."],

            "longevity" =>
                ["The preventive treatment feels like a worthwhile investment in my health.", "I'm glad I took my long-term health seriously."],

            "fertility" =>
                ["I'm hopeful the fertility treatment will make a difference.", "The treatment has made me more hopeful about having children."],

            _ =>
                ["I'm pleased with the improvement.", "That treatment seems to have paid off."]
        };
    }

}
