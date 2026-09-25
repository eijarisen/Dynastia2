using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public static class PersonEmojiResolver
{
    public static string GetPersonEmoji(
        IPerson person,
        IFamilyService? family,
        IHealthService? health,
        ICareerService? career,
        IJusticeService? justice,
        IStatsService? stats,
        IThoughtService? thoughts = null,
        IAppearanceService? appearance = null)
    {
        if (person.Tags.Has("state.dead"))
            return "💀";

        if (person.Tags.Has("state.imprisoned")
            || justice?.IsImprisoned(person) == true)
        {
            return "⛓️";
        }

        if (person.Age < 5)
        {
            var healthState = health?.GetHealth(person);

            if (healthState is not null)
            {
                var serious =
                    healthState.Percentage <= 20
                    || healthState.Conditions.Any(condition =>
                        condition.Type.Equals(
                            "terminal",
                            StringComparison.OrdinalIgnoreCase)
                        || condition.Type.Equals(
                            "critical",
                            StringComparison.OrdinalIgnoreCase));

                if (serious)
                    return "😣";

                var minorIllness =
                    healthState.Conditions.Any(condition =>
                        condition.Type.Equals(
                            "seasonal",
                            StringComparison.OrdinalIgnoreCase)
                        || condition.Type.Equals(
                            "curable",
                            StringComparison.OrdinalIgnoreCase));

                if (minorIllness)
                    return "🤒";
            }

            return ResolveNeutralPersonEmoji(person, family);
        }

        var thought = thoughts?.GetCurrentThought(person);

        if (thought is not null
            && !string.IsNullOrWhiteSpace(thought.MoodId)
            && !string.Equals(
                thought.MoodId,
                ThoughtMoodIds.Neutral,
                StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(thought.MoodEmoji))
        {
            return thought.MoodEmoji;
        }

        return ResolveNeutralPersonEmoji(person, family);
    }

    internal static string ResolveNeutralPersonEmoji(
        IPerson person,
        IFamilyService? family)
    {
        // Status indicators are deliberately generic yellow-face emoji.
        // Character appearance/age/sex belongs to the portrait presentation,
        // not to the transient status icon.
        return "🙂";
    }
}
