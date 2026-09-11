using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed class CareerCatalog
{
    private const string DataPath =
        "Career/careers.csv";

    private readonly IReadOnlyList<CareerDefinition>
        _careers;

    private readonly IReadOnlyDictionary<
        string,
        CareerDefinition>
        _byId;

    private CareerCatalog(
        IReadOnlyList<CareerDefinition> careers)
    {
        _careers =
            careers;

        _byId =
            careers.ToDictionary(
                career =>
                    career.Id,
                StringComparer.OrdinalIgnoreCase);
    }

    public static CareerCatalog Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(
            data);

        var careers =
            Parse(
                data.ReadText(
                    DataPath));

        Validate(
            careers);

        return new CareerCatalog(
            careers);
    }

    public CareerDefinition? Find(
        string? id)
    {
        if (string.IsNullOrWhiteSpace(
            id))
        {
            return null;
        }

        return _byId.TryGetValue(
            id,
            out var career)
                ? career
                : null;
    }

    public CareerDefinition SelectForEntry(
        Sex sex,
        int gameYear,
        IGameRandom random)
    {
        var available =
            _careers
                .Where(
                    career =>
                        career.IsOpenForEntry(
                            gameYear))
                .Select(
                    career =>
                        new WeightedCareer(
                            career,
                            career.GetEntryWeight(
                                sex,
                                gameYear)))
                .Where(
                    entry =>
                        entry.Weight > 0)
                .ToList();

        if (available.Count == 0)
        {
            throw new InvalidOperationException(
                $"No career is available for entry " +
                $"in year {gameYear}.");
        }

        var total =
            available.Sum(
                entry =>
                    entry.Weight);

        var roll =
            random.NextDouble()
            * total;

        foreach (var entry in
            available)
        {
            if (roll < entry.Weight)
                return entry.Career;

            roll -=
                entry.Weight;
        }

        return available[^1].Career;
    }

    private static IReadOnlyList<CareerDefinition>
        Parse(
            string text)
    {
        var lines =
            text.Split(
                ['\r', '\n'],
                StringSplitOptions
                    .RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            throw new InvalidDataException(
                $"{DataPath} is empty.");
        }

        var header =
            lines[0].Split(',');

        var expectedHeader =
            new[]
            {
                "Number",
                "Id",
                "Name",
                "StartYear",
                "EndYear",
                "MaleEarly",
                "FemaleEarly",
                "MaleLate",
                "FemaleLate",
                "BaseSalary",
                "Level1Title",
                "Level2Title",
                "Level3Title",
                "Level4Title",
                "Level5Title"
            };

        if (!header.SequenceEqual(
            expectedHeader,
            StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"{DataPath} has an unexpected header.");
        }

        var result =
            new List<CareerDefinition>();

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields =
                lines[index].Split(',');

            if (fields.Length
                != expectedHeader.Length)
            {
                throw new InvalidDataException(
                    $"{DataPath} row {index + 1} " +
                    $"has {fields.Length} fields; " +
                    $"expected {expectedHeader.Length}.");
            }

            result.Add(
                new CareerDefinition(
                    Id:
                        fields[1],
                    Name:
                        fields[2],
                    StartYear:
                        ParseInt(
                            fields[3],
                            index),
                    EndYear:
                        string.IsNullOrWhiteSpace(
                            fields[4])
                            ? null
                            : ParseInt(
                                fields[4],
                                index),
                    MaleEarly:
                        ParseInt(
                            fields[5],
                            index),
                    FemaleEarly:
                        ParseInt(
                            fields[6],
                            index),
                    MaleLate:
                        ParseInt(
                            fields[7],
                            index),
                    FemaleLate:
                        ParseInt(
                            fields[8],
                            index),
                    BaseSalary:
                        ParseDecimal(
                            fields[9],
                            index),
                    Level1Title:
                        fields[10],
                    Level2Title:
                        fields[11],
                    Level3Title:
                        fields[12],
                    Level4Title:
                        fields[13],
                    Level5Title:
                        fields[14]));
        }

        return result;
    }

    private static void Validate(
        IReadOnlyList<CareerDefinition> careers)
    {
        if (careers.Count != 65)
        {
            throw new InvalidDataException(
                $"{DataPath} must contain exactly " +
                $"65 careers; found {careers.Count}.");
        }

        if (careers
            .Select(
                career =>
                    career.Id)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Count()
            != careers.Count)
        {
            throw new InvalidDataException(
                $"{DataPath} contains duplicate career IDs.");
        }

        foreach (var career in careers)
        {
            if (career.StartYear < 1900
                || career.StartYear
                    > CareerDefinition
                        .TechnologyFreezeYear)
            {
                throw new InvalidDataException(
                    $"{career.Name}: invalid start year " +
                    $"{career.StartYear}.");
            }

            if (career.EndYear is int endYear
                && endYear < career.StartYear)
            {
                throw new InvalidDataException(
                    $"{career.Name}: end year precedes " +
                    "start year.");
            }

            if (career.MaleEarly
                    + career.FemaleEarly
                    != 100
                || career.MaleLate
                    + career.FemaleLate
                    != 100)
            {
                throw new InvalidDataException(
                    $"{career.Name}: M/F entry weights " +
                    "must sum to 100.");
            }

            if (career.MaleEarly <= 0
                || career.FemaleEarly <= 0
                || career.MaleLate <= 0
                || career.FemaleLate <= 0)
            {
                throw new InvalidDataException(
                    $"{career.Name}: sex may influence " +
                    "career entry but may not make it " +
                    "impossible.");
            }

            if (career.BaseSalary <= 0)
            {
                throw new InvalidDataException(
                    $"{career.Name}: base salary must " +
                    "be positive.");
            }

            if (new[]
                {
                    career.Level1Title,
                    career.Level2Title,
                    career.Level3Title,
                    career.Level4Title,
                    career.Level5Title
                }
                .Any(
                    string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException(
                    $"{career.Name}: all five job titles " +
                    "are required.");
            }
        }
    }

    private static int ParseInt(
        string value,
        int rowIndex)
    {
        if (int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        throw new InvalidDataException(
            $"{DataPath} row {rowIndex + 1}: " +
            $"'{value}' is not an integer.");
    }

    private static decimal ParseDecimal(
        string value,
        int rowIndex)
    {
        if (decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        throw new InvalidDataException(
            $"{DataPath} row {rowIndex + 1}: " +
            $"'{value}' is not a decimal.");
    }

    private sealed record WeightedCareer(
        CareerDefinition Career,
        double Weight);
}
