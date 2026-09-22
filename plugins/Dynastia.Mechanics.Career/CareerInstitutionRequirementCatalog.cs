using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed record CareerInstitutionRequirement(
    string CareerId,
    string InstitutionId,
    int MinimumTier);

public sealed class CareerInstitutionRequirementCatalog
{
    private const string DataPath = "TownLife/career_institution_requirements.csv";

    private static readonly IReadOnlySet<string> KnownInstitutionIds =
        new HashSet<string>(
            [
                "school",
                "bank",
                "medical",
                "court",
                "administration",
                "post_office",
                "railway_station",
                "port",
                "church"
            ],
            StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<string, CareerInstitutionRequirement> _byCareerId;

    private CareerInstitutionRequirementCatalog(
        IReadOnlyDictionary<string, CareerInstitutionRequirement> byCareerId)
    {
        _byCareerId = byCareerId;
    }

    public int Count => _byCareerId.Count;

    public static CareerInstitutionRequirementCatalog Load(
        IGameDataService data,
        IEnumerable<string> knownCareerIds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(knownCareerIds);

        var careers = knownCareerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = data.ReadText(DataPath).Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);
        const string expectedHeader = "CareerId,InstitutionId,MinimumTier";

        if (lines.Length < 1
            || !lines[0].TrimStart('\uFEFF').Equals(
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                DataPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                expectedHeader);
        }

        var result = new Dictionary<string, CareerInstitutionRequirement>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < lines.Length; index++)
        {
            var row = index + 1;
            var fields = lines[index].Split(',');
            if (fields.Length != 3)
                throw CatalogValidation.FieldCount(DataPath, row, fields.Length, 3);

            var careerId = fields[0].Trim();
            var institutionId = fields[1].Trim();
            var minimumTier = CatalogValidation.ParseInt(
                DataPath,
                row,
                "MinimumTier",
                fields[2]);

            if (!careers.Contains(careerId))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a CareerId defined by the career catalog",
                    row,
                    item: careerId,
                    field: "CareerId",
                    value: careerId);
            }

            if (!KnownInstitutionIds.Contains(institutionId))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a supported Town / City Affairs InstitutionId",
                    row,
                    item: careerId,
                    field: "InstitutionId",
                    value: institutionId);
            }

            if (minimumTier is < 1 or > 5)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a tier from 1 through 5",
                    row,
                    item: careerId,
                    field: "MinimumTier",
                    value: minimumTier);
            }

            if (!result.TryAdd(
                    careerId,
                    new CareerInstitutionRequirement(
                        careerId,
                        institutionId,
                        minimumTier)))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a unique CareerId",
                    row,
                    item: careerId,
                    field: "CareerId",
                    value: careerId);
            }
        }

        return new CareerInstitutionRequirementCatalog(result);
    }

    public CareerInstitutionRequirement? Find(string? careerId)
    {
        if (string.IsNullOrWhiteSpace(careerId))
            return null;

        return _byCareerId.TryGetValue(careerId, out var requirement)
            ? requirement
            : null;
    }

    public bool IsSatisfied(
        string? careerId,
        TownInstitutionSnapshot institutions)
    {
        ArgumentNullException.ThrowIfNull(institutions);

        var requirement = Find(careerId);
        return requirement is null
            || institutions.GetTier(requirement.InstitutionId)
                >= requirement.MinimumTier;
    }
}
