using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class AboutTextBuilder
{
    private string FertilityDescription(
        IPerson person,
        int value,
        bool isAlive,
        Sex sex,
        Pronouns pronouns)
    {
        if (!isAlive
            && person.Age < AdultAge)
        {
            return
                $"{pronouns.Subject} died before adulthood, so " +
                $"{pronouns.PossessiveLower} fertility potential was never " +
                "meaningfully expressed.";
        }

        var expectation =
            FertilityExpectation.For(
                value);

        var completedFertility =
            !isAlive
            || (
                sex == Sex.Female
                && person.Age >= 45
            );

        if (!completedFertility)
        {
            return
                $"{pronouns.Possessive} fertility profile suggests " +
                $"{expectation.LivingDescription}.";
        }

        var children =
            _family.GetChildren(
                person)
            .Count;

        var actualText =
            children == 0
                ? $"{pronouns.Subject} ultimately had no children"
                : $"{pronouns.Subject} ultimately had " +
                  $"{ChildCountText(children)}";

        var comparison =
            CompareChildrenToExpectation(
                children,
                expectation);

        return
            $"{pronouns.Possessive} fertility profile suggested " +
            $"{expectation.CompletedDescription}; " +
            $"{actualText}, {comparison}.";
    }

    private static string AppealDescription(
        IPerson person,
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (person.Age < AdultAge)
        {
            if (isAlive)
            {
                return value switch
                {
                    1 =>
                        $"As a child, {pronouns.PossessiveLower} appearance " +
                        "is still developing, though " +
                        $"{pronouns.SubjectLower} currently has rather plain features.",

                    2 =>
                        $"As a child, {pronouns.PossessiveLower} appearance " +
                        "is still developing and is presently unremarkable.",

                    3 =>
                        $"As a child, {pronouns.PossessiveLower} appearance " +
                        "is still developing and is presently pleasant and ordinary.",

                    4 =>
                        $"As a child, {pronouns.SubjectLower} is already " +
                        "notably attractive for the age.",

                    _ =>
                        $"As a child, {pronouns.SubjectLower} already has " +
                        "unusually striking features."
                };
            }

            return value switch
            {
                1 =>
                    $"As a child, {pronouns.SubjectLower} had rather plain features.",

                2 =>
                    $"As a child, {pronouns.SubjectLower} had an unremarkable appearance.",

                3 =>
                    $"As a child, {pronouns.SubjectLower} had a pleasant, ordinary appearance.",

                4 =>
                    $"As a child, {pronouns.SubjectLower} was notably attractive for the age.",

                _ =>
                    $"As a child, {pronouns.SubjectLower} had unusually striking features."
            };
        }

        if (person.Age >= ElderAge)
        {
            if (isAlive)
            {
                return value switch
                {
                    <= 2 =>
                        $"In later life, {pronouns.SubjectLower} has a fairly " +
                        "plain appearance and modest social presence.",

                    3 =>
                        $"In later life, {pronouns.SubjectLower} has an ordinary " +
                        "but pleasant appearance.",

                    4 =>
                        $"In later life, {pronouns.SubjectLower} retains a " +
                        "distinguished and attractive appearance.",

                    _ =>
                        $"In later life, {pronouns.SubjectLower} retains a " +
                        "remarkably striking and charismatic presence."
                };
            }

            return value switch
            {
                <= 2 =>
                    $"In later life, {pronouns.SubjectLower} had a fairly " +
                    "plain appearance and modest social presence.",

                3 =>
                    $"In later life, {pronouns.SubjectLower} had an ordinary " +
                    "but pleasant appearance.",

                4 =>
                    $"In later life, {pronouns.SubjectLower} retained a " +
                    "distinguished and attractive appearance.",

                _ =>
                    $"In later life, {pronouns.SubjectLower} retained a " +
                    "remarkably striking and charismatic presence."
            };
        }

        if (isAlive)
        {
            return value switch
            {
                1 =>
                    $"{pronouns.Subject} is considered very unattractive.",

                2 =>
                    $"{pronouns.Subject} is not particularly visually appealing.",

                3 =>
                    $"{pronouns.Subject} has a pleasant and modest appearance.",

                4 =>
                    $"{pronouns.Subject} is regarded as good-looking.",

                _ =>
                    $"{pronouns.Subject} has a striking and highly attractive presence."
            };
        }

        return value switch
        {
            1 =>
                $"{pronouns.Subject} was considered very unattractive.",

            2 =>
                $"{pronouns.Subject} was not considered particularly visually appealing.",

            3 =>
                $"{pronouns.Subject} had a pleasant and modest appearance.",

            4 =>
                $"{pronouns.Subject} was regarded as good-looking.",

            _ =>
                $"{pronouns.Subject} had a striking and highly attractive presence."
        };
    }

}
