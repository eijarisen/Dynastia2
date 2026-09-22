namespace Dynastia.Contracts;

public interface IPartnerSearchService
{
    IReadOnlyList<PartnerCandidateInfo> GetCandidates(
        IPerson seeker,
        int count = 3);

    IReadOnlyList<PartnerCandidateInfo> GetCandidatesFor(
        IPerson seeker,
        Sex partnerSex,
        string poolKey,
        int count = 3,
        int minimumSeekerAge = 18,
        int? minimumPartnerAge = null,
        int? maximumPartnerAge = null);

    IPerson MaterializeCandidate(
        PartnerCandidateInfo candidate,
        int? currentAge = null,
        IPerson? householdSpouse = null,
        bool seedHouseholdResources = false);

    double GetPartnerValue(IPerson person);

    IReadOnlyDictionary<string, string> BuildActionParameters(
        PartnerCandidateInfo candidate);

    GameActionResult ResolveArrangedMarriage(
        GameActionContext actionContext,
        HistoricalActionVariant variant);
}
