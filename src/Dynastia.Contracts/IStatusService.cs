namespace Dynastia.Contracts;

public interface IStatusService
{
    StatusSnapshot GetStatus(IPerson person);

    HouseholdSocialStatusSnapshot GetHouseholdStatus(IPerson householdMember);

    StatusSnapshot GetCandidateStatus(StatusCandidateProfile profile);

    string GetRenownLabel(double value) => string.Empty;

    string GetReputationLabel(double value) => string.Empty;

    double GetCareerApplicationBonus(IPerson person);

    double GetCareerPromotionBonus(IPerson person);

    void ApplyPersistentDelta(
        IPerson person,
        double renownDelta,
        double reputationDelta,
        string reason = "event");

    void SeedAdultInheritance(IPerson person);

    void ReconcileAll();
}
