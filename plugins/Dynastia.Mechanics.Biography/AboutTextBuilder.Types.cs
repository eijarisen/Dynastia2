using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class AboutTextBuilder
{
    private static string ChildCountText(
        int value)
    {
        return value == 1
            ? "1 child"
            : $"{value} children";
    }

    private static string CompareChildrenToExpectation(
        int actual,
        FertilityExpectation expectation)
    {
        if (actual < expectation.Minimum)
        {
            return
                "fewer than the fertility profile would normally suggest";
        }

        if (expectation.Maximum
                is int maximum
            && actual > maximum)
        {
            return
                "more than the fertility profile would normally suggest";
        }

        return
            "a family outcome broadly consistent with that fertility profile";
    }

    private sealed record FertilityExpectation(
        string LivingDescription,
        string CompletedDescription,
        int Minimum,
        int? Maximum)
    {
        public static FertilityExpectation For(
            int value)
        {
            return value switch
            {
                <= 0 =>
                    new FertilityExpectation(
                        "very poor fertility and little realistic prospect of parenthood",
                        "very poor fertility with little natural prospect of parenthood",
                        0,
                        0),

                1 =>
                    new FertilityExpectation(
                        "low fertility and a tendency toward either childlessness or a very small family",
                        "low fertility and a tendency toward a very small family",
                        0,
                        1),

                2 =>
                    new FertilityExpectation(
                        "below-average fertility and a tendency toward a smaller family",
                        "below-average fertility and a tendency toward a smaller family",
                        1,
                        2),

                3 =>
                    new FertilityExpectation(
                        "ordinary fertility and a moderate prospect of parenthood",
                        "ordinary fertility and a moderate family tendency",
                        2,
                        3),

                4 =>
                    new FertilityExpectation(
                        "high fertility and a strong tendency toward a larger family",
                        "high fertility and a strong tendency toward a larger family",
                        3,
                        4),

                _ =>
                    new FertilityExpectation(
                        "exceptional fertility and a strong natural tendency toward a large family",
                        "exceptional fertility and a strong natural tendency toward a large family",
                        4,
                        null)
            };
        }
    }

    private sealed record Pronouns(
        string Subject,
        string SubjectLower,
        string Possessive,
        string PossessiveLower,
        string Object)
    {
        public static Pronouns For(
            Sex sex)
        {
            return sex == Sex.Male
                ? new Pronouns(
                    "He",
                    "he",
                    "His",
                    "his",
                    "him")
                : new Pronouns(
                    "She",
                    "she",
                    "Her",
                    "her",
                    "her");
        }
    }
}
