namespace Dynastia.Contracts;

public enum LocalEconomicStrength
{
    Strong,
    Supported,
    Normal,
    Weak
}

public sealed record TownInstitutionInfo(
    string InstitutionId,
    string DisplayName,
    int Tier,
    string TierName)
{
    public string Emoji =>
        InstitutionId.ToLowerInvariant() switch
        {
            "school" => "🏫",
            "bank" => "🏦",
            "medical" => "🏥",
            "court" => "⚖️",
            "administration" => "🏛️",
            "post_office" => "📮",
            "railway_station" => "🚉",
            "port" => "⚓",
            _ => "🏢"
        };

    public bool IsAvailable => Tier > 0;

    public string Summary =>
        IsAvailable
            ? $"{TierName} (Tier {Tier})"
            : "Unavailable";
}

public sealed record TownInstitutionSnapshot(
    TownInfo Town,
    int Year,
    IReadOnlyList<TownInstitutionInfo> Institutions)
{
    public TownInstitutionInfo? Find(string institutionId) =>
        Institutions.FirstOrDefault(
            institution => institution.InstitutionId.Equals(
                institutionId,
                StringComparison.OrdinalIgnoreCase));

    public int GetTier(string institutionId) =>
        Find(institutionId)?.Tier ?? 0;
}

public interface ITownInstitutionService
{
    TownInstitutionSnapshot Resolve(
        TownInfo town,
        int year);
}

public sealed record TownProsperityHistoryPoint(
    int Year,
    int Index);

public sealed record TownProsperitySnapshot(
    int Index,
    string Label,
    int Trend,
    IReadOnlyList<string> ActiveShocks,
    IReadOnlyList<TownProsperityHistoryPoint>? History = null)
{
    public IReadOnlyList<TownProsperityHistoryPoint> HistoryPoints =>
        History ?? Array.Empty<TownProsperityHistoryPoint>();

    public string HistoryRangeText =>
        HistoryPoints.Count switch
        {
            0 => string.Empty,
            1 => HistoryPoints[0].Year.ToString(),
            _ => $"{HistoryPoints[0].Year}–{HistoryPoints[^1].Year}"
        };

    public string TrendText =>
        Trend switch
        {
            > 0 => $"↑ +{Trend}",
            < 0 => $"↓ {Trend}",
            _ => "—"
        };

    public string ActiveShocksText =>
        ActiveShocks.Count == 0
            ? "None"
            : string.Join(", ", ActiveShocks.Select(FormatShockId));

    private static string FormatShockId(string value) =>
        string.Join(
            " ",
            value.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}

public interface ITownProsperityService
{
    TownProsperitySnapshot Get(TownInfo town);

    TownProsperitySnapshot Get(TownInfo town, int year) =>
        Get(town);

    decimal GetIncomeMultiplier(
        TownInfo town,
        LocalEconomicStrength strength);

    void ApplyHistoricalShock(
        IEnumerable<string> placeIds,
        string sourceId,
        int delta,
        int recoveryYears);
}


public sealed record BankOfferQualityInfo(
    int Tier,
    string DisplayName,
    decimal PrincipalMultiplierMin,
    decimal PrincipalMultiplierMax,
    decimal InterestMultiplierMin,
    decimal InterestMultiplierMax,
    decimal DurationMultiplierMin,
    decimal DurationMultiplierMax,
    decimal LendingPrincipalMultiplierMin = 1m,
    decimal LendingPrincipalMultiplierMax = 1m,
    decimal LendingInterestMultiplierMin = 1m,
    decimal LendingInterestMultiplierMax = 1m,
    decimal LendingDurationMultiplierMin = 1m,
    decimal LendingDurationMultiplierMax = 1m)
{
    public bool IsAvailable => Tier > 0;
}

public sealed record MedicalQualityInfo(
    int Tier,
    string DisplayName,
    double TreatmentSuccessAdd,
    decimal TreatmentCostMultiplier)
{
    public bool IsAvailable => Tier > 0;
}

public interface ITownFacilityQualityService
{
    BankOfferQualityInfo GetBankQuality(
        TownInfo town,
        int year);

    MedicalQualityInfo GetMedicalQuality(
        TownInfo town,
        int year);
}

public sealed record TownInstitutionAffairsInfo(
    string InstitutionId,
    string DisplayName,
    string Emoji,
    string Summary,
    string ServiceText,
    string CareerText)
{
    public bool HasServiceText =>
        !string.IsNullOrWhiteSpace(ServiceText);
}

public sealed record TownLifeSnapshot(
    TownInfo Town,
    string RegionName,
    LocationOpportunitySnapshot Opportunities,
    TownInstitutionSnapshot Institutions,
    TownProsperitySnapshot Prosperity,
    BankOfferQualityInfo BankQuality,
    MedicalQualityInfo MedicalQuality,
    IReadOnlyList<TownInstitutionAffairsInfo>? InstitutionAffairs = null)
{
    public string NavigationLabel =>
        Town.SettlementClass is SettlementClass.City
            or SettlementClass.MajorCity
                ? "City Affairs"
                : "Town Affairs";

    public string WindowTitle =>
        $"{NavigationLabel} — {Town.Town}";

    public string PopulationText =>
        Town.Population.ToString("N0");

    public string LocalOpportunityText =>
        FormatTags(Opportunities.TownOpportunityTags);

    public string RegionalOpportunityText =>
        FormatTags(Opportunities.RegionOpportunityTags);

    public TownInstitutionInfo School =>
        Institutions.Find("school")
        ?? new TownInstitutionInfo("school", "School", 0, "Unavailable");

    public TownInstitutionInfo Bank =>
        Institutions.Find("bank")
        ?? new TownInstitutionInfo("bank", "Bank", 0, "Unavailable");

    public TownInstitutionInfo Medical =>
        Institutions.Find("medical")
        ?? new TownInstitutionInfo("medical", "Medical Facility", 0, "Unavailable");

    public IReadOnlyList<TownInstitutionAffairsInfo> InstitutionCards =>
        InstitutionAffairs
        ?? Institutions.Institutions
            .Select(institution => new TownInstitutionAffairsInfo(
                institution.InstitutionId,
                institution.DisplayName,
                institution.Emoji,
                institution.Summary,
                string.Empty,
                "Careers: —"))
            .ToArray();

    public string EducationCapacityText =>
        School.Tier == 0
            ? "No ordinary local schooling"
            : $"Maximum ordinary Education: {School.Tier}";

    public string FinanceCapacityText =>
        BankQuality.Tier switch
        {
            <= 0 => "Loans unavailable",
            1 => "Loan offers: unfavorable",
            2 => "Loan offers: modest",
            3 => "Loan offers: standard",
            4 => "Loan offers: favorable",
            _ => "Loan offers: very favorable"
        };

    public string HealthcareCapacityText =>
        MedicalQuality.Tier switch
        {
            <= 0 when Institutions.Year <= 1849 => "Healthcare: visiting physician only",
            <= 0 => "Healthcare unavailable",
            1 => "Healthcare: basic",
            2 => "Healthcare: limited",
            3 => "Healthcare: standard",
            4 => "Healthcare: good",
            _ => "Healthcare: excellent"
        };

    private static string FormatTags(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
            return "None";

        return string.Join(
            ", ",
            tags.Select(tag =>
                string.Join(
                    " ",
                    tag.Split(
                            '_',
                            StringSplitOptions.RemoveEmptyEntries)
                        .Select(part =>
                            part.Length == 0
                                ? part
                                : char.ToUpperInvariant(part[0]) + part[1..]))));
    }
}

public interface ITownLifeService
{
    TownLifeSnapshot GetCurrentTownLife(
        IPerson householdRepresentative);

    TownLifeSnapshot GetTownLife(
        string townId);
}

public interface ILocalEconomicStrengthService
{
    LocalEconomicStrength ResolveCareer(
        TownInfo town,
        string? careerFamily,
        IReadOnlyCollection<string>? requiredOpportunityTags = null);

    LocalEconomicStrength ResolveCraft(
        TownInfo town,
        CraftInfo craft);

    LocalEconomicStrength ResolveFarming(
        TownInfo town);
}
