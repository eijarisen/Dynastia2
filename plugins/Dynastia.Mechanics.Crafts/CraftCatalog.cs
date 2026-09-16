using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

internal sealed class CraftCatalog
{
    private const string DataPath = "Crafts/crafts.csv";

    private readonly IReadOnlyList<CraftInfo> _all;
    private readonly IReadOnlyDictionary<string, CraftInfo> _byId;

    private CraftCatalog(IReadOnlyList<CraftInfo> all)
    {
        _all = all;
        _byId = all.ToDictionary(craft => craft.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CraftInfo> All => _all;

    public CraftInfo? Find(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : _byId.TryGetValue(id, out var craft)
                ? craft
                : null;

    public static CraftCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var lines = data.ReadText(DataPath)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var expected = new[]
        {
            "Id", "Name", "StartYear", "Emoji", "SelfEmploymentTitle",
            "PrimaryCareerId", "RelatedCareerIds"
        };

        if (lines.Length < 2
            || !lines[0].Split(',').SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{DataPath} has an unexpected header.");
        }

        var result = new List<CraftInfo>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != expected.Length)
                throw new InvalidDataException($"{DataPath} line {index + 1} has {fields.Length} fields; expected {expected.Length}.");

            if (!int.TryParse(fields[2], out var startYear))
                throw new InvalidDataException($"{DataPath} line {index + 1} has invalid StartYear '{fields[2]}'.");

            var related = fields[6] == "-"
                ? Array.Empty<string>()
                : fields[6].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            result.Add(new CraftInfo(
                fields[0].Trim(),
                fields[1].Trim(),
                startYear,
                fields[3].Trim(),
                fields[4].Trim(),
                fields[5].Trim(),
                related));
        }

        if (result.Count != 37)
            throw new InvalidDataException($"{DataPath} must define exactly 37 crafts; found {result.Count}.");

        var duplicate = result.GroupBy(craft => craft.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{DataPath} contains duplicate craft ID '{duplicate.Key}'.");

        return new CraftCatalog(result);
    }
}
