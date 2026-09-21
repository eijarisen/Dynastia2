using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class StandardTownLifeService : ITownLifeService
{
    private readonly IGameState _gameState;
    private readonly ILocationService _locations;
    private readonly ILocalCareerOpportunityService _opportunities;
    private readonly ITownInstitutionService _institutions;
    private readonly ITownProsperityService _prosperity;
    private readonly ITownFacilityQualityService _facilityQuality;
    private readonly TownInstitutionCareerCatalog? _careerInstitutions;
    private readonly Func<ICommunityPolicyService?> _communityResolver;

    public StandardTownLifeService(
        IGameState gameState,
        ILocationService locations,
        ILocalCareerOpportunityService opportunities,
        ITownInstitutionService institutions,
        ITownProsperityService prosperity,
        ITownFacilityQualityService facilityQuality,
        TownInstitutionCareerCatalog? careerInstitutions = null,
        Func<ICommunityPolicyService?>? communityResolver = null)
    {
        _gameState = gameState;
        _locations = locations;
        _opportunities = opportunities;
        _institutions = institutions;
        _prosperity = prosperity;
        _facilityQuality = facilityQuality;
        _careerInstitutions = careerInstitutions;
        _communityResolver = communityResolver ?? (() => null);
    }

    public TownLifeSnapshot GetCurrentTownLife(
        IPerson householdRepresentative)
    {
        ArgumentNullException.ThrowIfNull(householdRepresentative);

        return BuildSnapshot(
            _locations.GetLocation(householdRepresentative).HomeTown);
    }

    public TownLifeSnapshot GetTownLife(
        string townId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(townId);

        var town = _locations.FindTown(townId)
            ?? throw new InvalidOperationException(
                $"Town '{townId}' is not available in {_gameState.Year}.");

        return BuildSnapshot(town);
    }

    private TownLifeSnapshot BuildSnapshot(
        TownInfo town)
    {
        var opportunitySnapshot = _opportunities.GetOpportunitySnapshot(town);
        var institutionSnapshot = _institutions.Resolve(town, _gameState.Year);
        var prosperitySnapshot = _prosperity.Get(town);
        var bankQuality = _facilityQuality.GetBankQuality(town, _gameState.Year);
        var medicalQuality = _facilityQuality.GetMedicalQuality(town, _gameState.Year);

        return new TownLifeSnapshot(
            town,
            opportunitySnapshot.RegionName,
            opportunitySnapshot,
            institutionSnapshot,
            prosperitySnapshot,
            bankQuality,
            medicalQuality,
            BuildInstitutionCards(
                institutionSnapshot,
                bankQuality,
                medicalQuality),
            BuildCommunitySnapshot(town));
    }


    private CommunityAffairsSnapshot BuildCommunitySnapshot(TownInfo town)
    {
        var community = _communityResolver();
        return community is null
            ? CommunityAffairsSnapshot.Empty
            : new CommunityAffairsSnapshot(
                community.GetProposals(town, _gameState.Year),
                community.GetActivePolicies(town, _gameState.Year));
    }

    private IReadOnlyList<TownInstitutionAffairsInfo> BuildInstitutionCards(
        TownInstitutionSnapshot institutions,
        BankOfferQualityInfo bankQuality,
        MedicalQualityInfo medicalQuality)
    {
        return institutions.Institutions
            .Where(institution =>
                !institution.InstitutionId.Equals("church", StringComparison.OrdinalIgnoreCase)
                && (institution.Tier > 0
                    || institution.InstitutionId.Equals("medical", StringComparison.OrdinalIgnoreCase)
                    || institution.InstitutionId.Equals("school", StringComparison.OrdinalIgnoreCase)
                    || institution.InstitutionId.Equals("bank", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(institution => institution.InstitutionId.ToLowerInvariant() switch
            {
                "administration" => 0,
                "medical" => 1,
                "school" => 2,
                "bank" => 3,
                _ => 10
            })
            .ThenBy(institution => institution.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(institution =>
            {
                var serviceText = institution.InstitutionId.ToLowerInvariant() switch
                {
                    "school" => institution.Tier <= 0
                        ? "Education: unavailable"
                        : $"Education: up to Level {institution.Tier}",
                    "bank" => bankQuality.Tier switch
                    {
                        <= 0 => "Loans: unavailable",
                        1 => "Loan offers: unfavorable",
                        2 => "Loan offers: modest",
                        3 => "Loan offers: standard",
                        4 => "Loan offers: favorable",
                        _ => "Loan offers: very favorable"
                    },
                    "medical" => medicalQuality.Tier switch
                    {
                        <= 0 when _gameState.Year <= 1849 =>
                            "Healthcare: visiting physician only",
                        <= 0 => "Healthcare: unavailable",
                        1 => "Healthcare: basic",
                        2 => "Healthcare: limited",
                        3 => "Healthcare: standard",
                        4 => "Healthcare: good",
                        _ => "Healthcare: excellent"
                    },
                    _ => string.Empty
                };

                var careers = _careerInstitutions?.GetEnabledCareers(
                    institution.InstitutionId,
                    institution.Tier,
                    _gameState.Year)
                    ?? Array.Empty<string>();

                return new TownInstitutionAffairsInfo(
                    institution.InstitutionId,
                    institution.DisplayName,
                    institution.Emoji,
                    institution.Summary,
                    serviceText,
                    careers.Count == 0
                        ? "Careers: —"
                        : $"Careers: {string.Join(", ", careers)}");
            })
            .ToArray();
    }

}
