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
    public string DisplayName => $"{Name} {DisplaySurname}";

    public string DisplaySurname
    {
        get
        {
            if (Sex != Dynastia.Contracts.Sex.Female)
                return Surname;

            if (Surname.EndsWith("ski", StringComparison.OrdinalIgnoreCase))
                return Surname[..^3] + "ska";

            if (Surname.EndsWith("cki", StringComparison.OrdinalIgnoreCase))
                return Surname[..^3] + "cka";

            return Surname;
        }
    }
}
