using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public static class PersonEmojiResolver
{
    private const int StudentAge = 6;
    private const int AdultAge = 18;
    private const int MaleRetirementAge = 65;
    private const int FemaleRetirementAge = 60;

    public static string GetPersonEmoji(
        IPerson person,
        IFamilyService? family,
        IHealthService? health,
        ICareerService? career,
        IJusticeService? justice,
        IStatsService? stats)
    {
        // Exact Dynasty 4 precedence.
        if (person.Tags.Has("state.dead"))
            return "💀";

        if (justice?.GetStatus(person).IsImprisoned == true)
            return "⛓️";

        var conditions =
            health?.GetHealth(person).Conditions
            ?? Array.Empty<HealthConditionInfo>();

        bool HasCondition(string name) =>
            conditions.Any(
                condition =>
                    condition.Name.Equals(
                        name,
                        StringComparison.Ordinal));

        var hasPermanentOrTerminal =
            conditions.Any(
                condition =>
                    condition.Type.Equals(
                        "permanent",
                        StringComparison.OrdinalIgnoreCase)
                    || condition.Type.Equals(
                        "terminal",
                        StringComparison.OrdinalIgnoreCase));

        var hasMinorIllness =
            conditions.Any(
                condition =>
                    condition.Type.Equals(
                        "seasonal",
                        StringComparison.OrdinalIgnoreCase)
                    || condition.Type.Equals(
                        "curable",
                        StringComparison.OrdinalIgnoreCase));

        if (hasPermanentOrTerminal)
            return "😣";

        if (hasMinorIllness)
            return "🤧";

        if (HasCondition("Depression"))
            return "😥";

        if (HasCondition("Alcoholism"))
            return "🥴";

        if (HasCondition("Autism"))
            return "🧩";

        var careerState =
            career?.GetCareer(person);

        var jobLevel =
            careerState?.JobLevel ?? 0;

        var jobSatisfaction =
            careerState?.JobSatisfaction ?? 0;

        var jobTitle =
            careerState?.JobTitle
            ?? string.Empty;

        if (jobLevel > 0)
        {
            if (jobSatisfaction == 5)
                return "🤩";

            if (jobSatisfaction == 1)
                return "🤬";
        }

        var sex =
            family is not null
                ? family.GetSex(person)
                : person.Tags.Has("sex.female")
                    ? Sex.Female
                    : Sex.Male;

        if (person.Age >= AdultAge)
        {
            if (jobLevel > 0)
            {
                switch (jobTitle)
                {
                    case "Laborer":
                        return sex == Sex.Male
                            ? "👷‍♂️"
                            : "👷‍♀️";

                    case "Clerk":
                        return sex == Sex.Male
                            ? "👨‍💼"
                            : "👩‍💼";

                    case "Manager":
                        return sex == Sex.Male
                            ? "👨‍💻"
                            : "👩‍💻";

                    case "Director":
                        return sex == Sex.Male
                            ? "🤵‍♂️"
                            : "🤵‍♀️";

                    case "Magnate":
                        return sex == Sex.Male
                            ? "🤴"
                            : "👸";

                    case "Housewife":
                        return "👩‍🍳";

                    case "Nanny":
                        return "🧑‍🍼";
                }
            }
            else if (jobTitle.Equals(
                "Unemployed",
                StringComparison.Ordinal))
            {
                var values =
                    stats?.GetStats(person)
                        .ToDictionary(
                            stat => stat.Id,
                            stat => stat.Value,
                            StringComparer.OrdinalIgnoreCase);

                if (values is not null)
                {
                    if (Get(values, "immunity") == 5)
                        return "🐴";

                    if (Get(values, "immunity") == 1)
                        return "😩";

                    if (Get(values, "fertility") == 5)
                        return "😏";

                    if (Get(values, "fertility") == 1)
                        return "😶";

                    if (Get(values, "appeal") == 5)
                        return "🔥";

                    if (Get(values, "appeal") == 1)
                        return "💩";

                    if (Get(values, "strength") == 5)
                        return "🐗";

                    if (Get(values, "strength") == 1)
                        return "🥀";

                    if (Get(values, "intellect") == 5)
                        return "🧠";

                    if (Get(values, "intellect") == 1)
                        return "🤪";

                    if (Get(values, "longevity") == 5)
                        return "⌛";

                    if (Get(values, "longevity") == 1)
                        return "🫠";
                }
            }
        }

        if (person.Age < StudentAge)
            return "👶";

        if (person.Age < AdultAge)
        {
            return sex == Sex.Male
                ? "👦"
                : "👧";
        }

        var retirementAge =
            sex == Sex.Male
                ? MaleRetirementAge
                : FemaleRetirementAge;

        if (person.Age >= retirementAge)
        {
            return sex == Sex.Male
                ? "👴"
                : "👵";
        }

        return sex == Sex.Male
            ? "👨"
            : "👩";
    }

    private static int Get(
        IReadOnlyDictionary<string, int> values,
        string key)
    {
        return values.TryGetValue(
            key,
            out var value)
                ? value
                : 0;
    }
}
