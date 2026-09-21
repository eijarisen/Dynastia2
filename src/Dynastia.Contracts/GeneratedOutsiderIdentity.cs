namespace Dynastia.Contracts;

public sealed record GeneratedOutsiderIdentity(
    string NationalityId,
    string DisplayNationality,
    string NameCultureId,
    string FirstName,
    string Surname,
    string? ForeignBirthplaceCity = null,
    string? ForeignBirthplaceCountry = null)
{
    public string? ForeignBirthplaceDisplayName =>
        string.IsNullOrWhiteSpace(ForeignBirthplaceCity)
        || string.IsNullOrWhiteSpace(ForeignBirthplaceCountry)
            ? null
            : $"{ForeignBirthplaceCity}, {ForeignBirthplaceCountry}";
}
