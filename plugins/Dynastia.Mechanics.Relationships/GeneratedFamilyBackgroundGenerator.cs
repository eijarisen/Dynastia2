using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal static class GeneratedFamilyBackgroundGenerator
{
    private const string MaleNamesPath =
        "Names/polish_male.csv";

    private const string FemaleNamesPath =
        "Names/polish_female.csv";

    public static void Assign(
        IPerson person,
        string familySurname,
        IFamilyService family,
        IGameDataService data,
        IGameRandom random)
    {
        var fatherName =
            $"{RandomWeightedFrom(data, random, MaleNamesPath)} " +
            familySurname;

        var motherName =
            $"{RandomWeightedFrom(data, random, FemaleNamesPath)} " +
            family.FormatSurname(
                familySurname,
                Sex.Female);

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

            var name =
                RandomWeightedFrom(
                    data,
                    random,
                    sex == Sex.Male
                        ? MaleNamesPath
                        : FemaleNamesPath);

            siblings.Add(
                $"{name} " +
                family.FormatSurname(
                    familySurname,
                    sex));
        }

        family.SetGeneratedFamilyBackground(
            person,
            new GeneratedFamilyBackgroundInfo(
                fatherName,
                motherName,
                siblings));
    }

    private static string RandomWeightedFrom(
        IGameDataService data,
        IGameRandom random,
        string relativePath)
    {
        var entries =
            data.GetWeightedStringList(
                relativePath);

        var totalWeight =
            entries.Sum(
                entry =>
                    (double)entry.Weight);

        var roll =
            random.NextDouble()
            * totalWeight;

        foreach (var entry in entries)
        {
            if (roll < entry.Weight)
                return entry.Value;

            roll -= entry.Weight;
        }

        return entries[^1].Value;
    }
}
