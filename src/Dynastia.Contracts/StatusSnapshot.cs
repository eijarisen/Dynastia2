namespace Dynastia.Contracts;

public sealed record StatusSnapshot(
    double Renown,
    double LocalRenown,
    double Reputation,
    string RenownLabel,
    string ReputationLabel)
{
    public string RenownText => $"Renown: {Renown:0.#} — {RenownLabel}";
    public string ReputationText => $"Reputation: {Reputation:0.#} — {ReputationLabel}";
}

public sealed record HouseholdSocialStatusSnapshot(
    double Renown,
    double Reputation,
    int AdultCount);

public sealed record StatusCandidateProfile(
    decimal NetWorth,
    int EducationLevel,
    int CareerLevel,
    int CraftMasteryLevel,
    bool IsActiveFarmWorker,
    bool IsCivicHead = false,
    double PersistentRenownDelta = 0,
    double PersistentReputationDelta = 0);
