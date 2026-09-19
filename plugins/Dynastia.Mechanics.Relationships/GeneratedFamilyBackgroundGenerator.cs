using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public static class GeneratedFamilyBackgroundGenerator
{
    public static void Assign(
        IPerson person,
        string familySurname,
        string nameCultureId,
        IFamilyService family,
        IHistoricalNameService historicalNames,
        IGameRandom random)
    {
        var personBirthYear =
            person.BirthDate?.Year
            ?? throw new InvalidOperationException(
                "Generated adults must have a birth date before family background generation.");

        var fatherBirthYear =
            personBirthYear
            - (20 + DeterministicOffset(person.Id, 7, 16));

        var motherBirthYear =
            personBirthYear
            - (20 + DeterministicOffset(person.Id, 11, 16));

        var fatherName =
            $"{historicalNames.GetRandomFirstName(Sex.Male, fatherBirthYear, nameCultureId, random)} " +
            historicalNames.FormatSurname(
                familySurname,
                Sex.Male,
                nameCultureId);

        var motherName =
            $"{historicalNames.GetRandomFirstName(Sex.Female, motherBirthYear, nameCultureId, random)} " +
            historicalNames.FormatSurname(
                familySurname,
                Sex.Female,
                nameCultureId);

        var siblings =
            new List<string>();

        var count =
            random.NextInt(0, 4);

        for (var i = 0; i < count; i++)
        {
            var sex =
                random.NextDouble() > 0.5
                    ? Sex.Male
                    : Sex.Female;

            var siblingBirthYear =
                personBirthYear
                + DeterministicOffset(
                    person.Id,
                    19 + i,
                    17)
                - 8;

            var name =
                historicalNames.GetRandomFirstName(
                    sex,
                    siblingBirthYear,
                    nameCultureId,
                    random);

            siblings.Add(
                $"{name} " +
                historicalNames.FormatSurname(
                    familySurname,
                    sex,
                    nameCultureId));
        }

        family.SetGeneratedFamilyBackground(
            person,
            new GeneratedFamilyBackgroundInfo(
                fatherName,
                motherName,
                siblings));
    }

    private static int DeterministicOffset(
        Guid id,
        int salt,
        int range)
    {
        var bytes = id.ToByteArray();

        var first =
            bytes[salt % bytes.Length];

        var second =
            bytes[(salt * 5 + 3)
                % bytes.Length];

        return (first * 31
                + second
                + salt * 17)
            % range;
    }
}
