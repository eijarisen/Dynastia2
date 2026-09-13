using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class AboutTextBuilder
{
    private static string StrengthDescription(
        IPerson person,
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (isAlive
            && person.Age < YoungChildAge)
        {
            return value switch
            {
                <= 2 =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggests below-average strength as " +
                    $"{pronouns.SubjectLower} grows.",

                3 =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggests average strength as " +
                    $"{pronouns.SubjectLower} grows.",

                _ =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggests an unusually strong build as " +
                    $"{pronouns.SubjectLower} grows."
            };
        }

        if (!isAlive
            && person.Age < YoungChildAge)
        {
            return value switch
            {
                <= 2 =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggested relatively weak future strength.",

                3 =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggested average future strength.",

                _ =>
                    $"{pronouns.Possessive} early physical development " +
                    "suggested unusually strong future physical development."
            };
        }

        if (person.Age < AdultAge)
        {
            if (isAlive)
            {
                return value switch
                {
                    <= 2 =>
                        $"Physically, {pronouns.SubjectLower} is relatively weak for the age.",

                    3 =>
                        $"Physically, {pronouns.SubjectLower} has average strength for the age.",

                    _ =>
                        $"Physically, {pronouns.SubjectLower} is notably strong for the age."
                };
            }

            return value switch
            {
                <= 2 =>
                    $"Physically, {pronouns.SubjectLower} was relatively weak for the age.",

                3 =>
                    $"Physically, {pronouns.SubjectLower} had average strength for the age.",

                _ =>
                    $"Physically, {pronouns.SubjectLower} was notably strong for the age."
            };
        }

        if (isAlive)
        {
            return value switch
            {
                <= 2 =>
                    $"Physically, {pronouns.SubjectLower} is weak and " +
                    "not especially capable of demanding exertion.",

                3 =>
                    $"Physically, {pronouns.SubjectLower} has average strength.",

                _ =>
                    $"Physically, {pronouns.SubjectLower} is notably strong " +
                    "and capable of demanding exertion."
            };
        }

        return value switch
        {
            <= 2 =>
                $"Physically, {pronouns.SubjectLower} was weak and " +
                "not especially capable of demanding exertion.",

            3 =>
                $"Physically, {pronouns.SubjectLower} had average strength.",

            _ =>
                $"Physically, {pronouns.SubjectLower} was notably strong " +
                "and capable of demanding exertion."
        };
    }

    private static string IntellectDescription(
        int value,
        bool isAlive,
        Pronouns pronouns)
    {
        if (isAlive)
        {
            return value switch
            {
                1 =>
                    $"Mentally, {pronouns.SubjectLower} has very limited intellectual ability.",

                2 =>
                    $"Mentally, {pronouns.SubjectLower} is somewhat below average in intellect.",

                3 =>
                    $"Mentally, {pronouns.SubjectLower} has an average intellect.",

                4 =>
                    $"Mentally, {pronouns.SubjectLower} is bright and quick to understand difficult matters.",

                _ =>
                    $"Mentally, {pronouns.SubjectLower} is exceptionally gifted, with a remarkably sharp intellect."
            };
        }

        return value switch
        {
            1 =>
                $"Mentally, {pronouns.SubjectLower} had very limited intellectual ability.",

            2 =>
                $"Mentally, {pronouns.SubjectLower} was somewhat below average in intellect.",

            3 =>
                $"Mentally, {pronouns.SubjectLower} had an average intellect.",

            4 =>
                $"Mentally, {pronouns.SubjectLower} was bright and quick to understand difficult matters.",

            _ =>
                $"Mentally, {pronouns.SubjectLower} was exceptionally gifted, with a remarkably sharp intellect."
        };
    }

}
