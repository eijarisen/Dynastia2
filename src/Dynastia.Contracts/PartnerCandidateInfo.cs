namespace Dynastia.Contracts;

public sealed record PartnerCandidateInfo(
    string CandidateKey,
    string Name,
    string Surname,
    Sex Sex,
    GameDate BirthDate,
    int Age,
    PersonalitySnapshot Personality,
    int EducationLevel,
    IReadOnlyDictionary<string, int> Stats,
    IReadOnlyList<HobbyInfo> Hobbies,
    string? CareerId,
    string CareerName,
    string JobTitle,
    int JobLevel,
    int JobSatisfaction,
    decimal AnnualIncome,
    decimal EstimatedWealth,
    int EstimatedHouses,
    AppearanceSnapshot Appearance,
    string PortraitEmoji,
    double PartnerValue,
    double AcceptanceChance,
    string TownId,
    int SearchYear)
{
    public IReadOnlyList<CraftInfo> Crafts { get; init; } = [];

    public int EstimatedFarmland { get; init; }

    public string NationalityId { get; init; } = "polish";

    public string DisplayNationality { get; init; } = "Polish";

    public string OriginTownDisplayName { get; init; } = string.Empty;

    public string? ForeignBirthplaceCity { get; init; }

    public string? ForeignBirthplaceCountry { get; init; }

    public string DisplayName => $"{Name} {DisplaySurname}";

    public string DisplaySurname
    {
        get
        {
            if (Sex != Dynastia.Contracts.Sex.Female
                || !NationalityId.Equals(
                    "polish",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Surname;
            }

            return PolishSurnameRules.Feminize(Surname);
        }
    }
}
