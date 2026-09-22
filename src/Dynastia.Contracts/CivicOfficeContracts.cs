namespace Dynastia.Contracts;

public sealed record CivicOfficeProfileInfo(
    string PolityId,
    int StartYear,
    int EndYear,
    string MemberTitle,
    string HeadTitleSmallTown,
    string HeadTitleCity,
    int MinimumAge,
    string AllowedSex,
    int MinimumEducation,
    double MinimumRenown,
    double MinimumReputation,
    int MinimumParticipation)
{
    public string ResolveHeadTitle(SettlementClass settlementClass) =>
        settlementClass is SettlementClass.City or SettlementClass.MajorCity
            ? HeadTitleCity
            : HeadTitleSmallTown;

    public bool AllowsSex(Sex sex) =>
        AllowedSex.Equals("Any", StringComparison.OrdinalIgnoreCase)
        || AllowedSex.Equals(sex.ToString(), StringComparison.OrdinalIgnoreCase);
}

public sealed record CivicOfficeHeadInfo(
    string TownId,
    string Name,
    Sex Sex,
    int BirthYear,
    string NationalityId,
    string OfficeTitle,
    double Renown,
    double Reputation,
    double Approval,
    int OfficeStartYear,
    bool IsSimulated,
    Guid? PersonId,
    decimal AnnualSalary)
{
    public int Age(int year) => Math.Max(0, year - BirthYear);
}

public interface ICivicOfficeService
{
    CivicOfficeProfileInfo? GetProfile(TownInfo town, int year);

    CivicOfficeHeadInfo? GetTownHead(TownInfo town, int year);

    CivicOfficeHeadInfo? GetOffice(IPerson person);

    bool IsTownHead(IPerson person);

    string? GetOfficeTitle(IPerson person);

    bool IsEligible(IPerson person, TownInfo town, int year);

    decimal GetAnnualSalary(IPerson person);
}
