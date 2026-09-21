namespace Dynastia.Contracts;

public sealed record CommunityPolicyProposerInfo(
    string Id,
    string Name,
    Sex Sex,
    int Age,
    string NationalityId,
    string Occupation,
    string ArchetypeId,
    string WealthBand,
    double Renown,
    double Reputation);

public sealed record CommunityPolicyProposalInfo(
    string PolicyId,
    string DisplayName,
    string Description,
    string Rarity,
    string ImpactTier,
    string Favorability,
    double BaseSupport,
    int DurationYears,
    string EffectKey,
    double Magnitude,
    int ProposalYear,
    string TownId,
    CommunityPolicyProposerInfo Proposer);

public sealed record CommunityActivePolicyInfo(
    string PolicyId,
    string DisplayName,
    string Description,
    int EnactedYear,
    int ExpiresAfterYear,
    string EffectKey,
    double Magnitude)
{
    public bool IsActive(int year) => year <= ExpiresAfterYear;

    public int RemainingYears(int year) =>
        Math.Max(0, ExpiresAfterYear - year + 1);
}

public sealed record CommunityPolicyModifierSnapshot(
    int ProsperityFlat = 0,
    double EducationSuccessAdd = 0,
    double JobApplicationAdd = 0,
    decimal FarmingIncomeMultiplier = 1m,
    decimal CraftIncomeMultiplier = 1m,
    decimal MedicalCostMultiplier = 1m,
    double MedicalSuccessAdd = 0,
    decimal BankQualityMultiplier = 1m,
    decimal HousingPriceMultiplier = 1m,
    int ExtraHousingOffers = 0,
    int SchoolServiceTierAdd = 0,
    int MedicalServiceTierAdd = 0,
    int BankServiceTierAdd = 0,
    decimal HistoricalWealthLossMultiplier = 1m,
    decimal HistoricalHealthLossMultiplier = 1m,
    decimal FloodLossMultiplier = 1m,
    decimal ChurchWelfareMultiplier = 1m,
    int ProsperityRecoveryBonus = 0);

public sealed record CommunityAffairsSnapshot(
    IReadOnlyList<CommunityPolicyProposalInfo> Proposals,
    IReadOnlyList<CommunityActivePolicyInfo> ActivePolicies)
{
    public static CommunityAffairsSnapshot Empty { get; } =
        new(Array.Empty<CommunityPolicyProposalInfo>(), Array.Empty<CommunityActivePolicyInfo>());
}

public interface ICommunityPolicyService
{
    IReadOnlyList<CommunityPolicyProposalInfo> GetProposals(
        TownInfo town,
        int year);

    IReadOnlyList<CommunityActivePolicyInfo> GetActivePolicies(
        TownInfo town,
        int year);

    CommunityPolicyModifierSnapshot GetModifiers(
        TownInfo town,
        int year);

    int GetParticipationCount(IPerson person);
}
