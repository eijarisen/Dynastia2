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
        if (person.Tags.Has(
            "state.dead"))
        {
            return "💀";
        }

        // Status emoji are intentionally separate from physical portraits.
        // Imprisonment is an overriding state and therefore wins over thoughts.
        if (person.Tags.Has(
            "state.imprisoned")
            || justice?.IsImprisoned(person) == true)
        {
            return "⛓️";
        }

        // Children below five do not have stored thoughts yet, so retain the
        // small immediate-health override that previously represented their
        // meaningful status. A healthy child simply uses the neutral status.
        if (person.Age < 5)
        {
            var healthState =
                health?.GetHealth(
                    person);

            if (healthState is not null)
            {
                var serious =
                    healthState.Percentage <= 20
                    || healthState.Conditions.Any(
                        condition =>
                            condition.Type.Equals(
                                "terminal",
                                StringComparison.OrdinalIgnoreCase));

                if (serious)
                    return "😣";

                var minorIllness =
                    healthState.Conditions.Any(
                        condition =>
                            condition.Type.Equals(
                                "seasonal",
                                StringComparison.OrdinalIgnoreCase)
                            || condition.Type.Equals(
                                "curable",
                                StringComparison.OrdinalIgnoreCase));

                if (minorIllness)
                    return "🤒";
            }

            return "🙂";
        }

        var thought =
            thoughts?.GetCurrentThought(
                person);

        if (thought is not null
            && !string.IsNullOrWhiteSpace(
                thought.Emoji)
            && !thought.Emoji.Equals(
                "🙂",
                StringComparison.Ordinal))
        {
            return thought.Emoji;
        }

        // Neutral state presentation is deliberately generic. Physical
        // appearance is shown only in portrait contexts.
        return "🙂";
    }
}
