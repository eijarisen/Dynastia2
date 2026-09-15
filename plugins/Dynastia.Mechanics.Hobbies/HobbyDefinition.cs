namespace Dynastia.Mechanics.Hobbies;

public sealed record HobbyDefinition(
    string Id,
    string Name,
    int StartYear,
    int? EndYear,
    int MinimumAge,
    string TownPreference,
    string GenderPreference,
    string PrimaryTemperament,
    string? SecondaryTemperament,
    string Emoji)
{
    public bool IsHistoricallyAvailable(
        int year) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value);

    public bool IsAvailable(
        int year,
        int age) =>
        IsHistoricallyAvailable(year)
        && age >= MinimumAge;
}
