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

        // Imprisonment is an overriding visual state. Do this before
        // thoughts and age-based emoji so prisoners always show chains.
        if (person.Tags.Has(
            "state.imprisoned")
            || justice?.IsImprisoned(person) == true)
        {
            return "⛓️";
        }

        // Ages 0-4 never receive a thought. Only their immediate physical
        // condition may replace the normal infant emoji.
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

            return GetNeutralPortrait(person, family, appearance);
        }

        var thought =
            thoughts?.GetCurrentThought(
                person);

        if (thought is not null
            && !string.IsNullOrWhiteSpace(
                thought.Emoji))
        {
            if (thought.Emoji.Equals(
                    "🙂",
                    StringComparison.Ordinal))
            {
                return GetNeutralPortrait(
                    person,
                    family,
                    appearance);
            }

            return thought.Emoji;
        }

        // Defensive fallback only. Under normal gameplay every living person
        // aged 5+ has a stored thought. Neutral presentation uses the person's
        // physical portrait rather than a generic yellow face.
        return GetNeutralPortrait(
            person,
            family,
            appearance);
    }

    private static string GetNeutralPortrait(
        IPerson person,
        IFamilyService? family,
        IAppearanceService? appearance)
    {
        if (appearance is not null)
        {
            return appearance.GetPortrait(
                person,
                useDeadOverride: false);
        }

        var sex = family is not null
            ? family.GetSex(person)
            : person.Tags.Has("sex.female")
                ? Sex.Female
                : Sex.Male;

        if (person.Age <= 4)
            return "👶🏻";

        if (person.Age <= 11)
            return sex == Sex.Male ? "👦🏻" : "👧🏻";

        if (person.Age <= 17)
            return "🧑🏻";

        if (person.Age >= 70)
            return sex == Sex.Male ? "👴🏻" : "👵🏻";

        return sex == Sex.Male ? "👨🏻" : "👩🏻";
    }
}
