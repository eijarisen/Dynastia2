namespace Dynastia.Contracts;

public sealed record LoanOfferInfo(
    string OfferKey,
    string CounterpartyName,
    Sex CounterpartySex,
    int CounterpartyAge,
    string PortraitEmoji,
    LoanTermsInfo Terms)
{
    public string OriginTownId { get; init; } = string.Empty;

    public string OriginTownDisplayName { get; init; } = string.Empty;

    public string NationalityId { get; init; } = "polish";

    public string DisplayNationality { get; init; } = "Polish";
}
