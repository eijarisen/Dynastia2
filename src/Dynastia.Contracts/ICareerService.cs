namespace Dynastia.Contracts;

public interface ICareerService
{
    void EnsureCareer(IPerson person);
    CareerSnapshot GetCareer(IPerson person);
    void InitializeCareer(IPerson person, int jobLevel, int jobSatisfaction);
    void SetJobLevel(IPerson person, int jobLevel);
    void ChangeJobSatisfaction(IPerson person, int amount);
    string GetStatusLabel(string statusId);
    void Retire(IPerson person);
    decimal GetAnnualIncome(IPerson person);
    bool IsEmployed(IPerson person);

    decimal GetLevelOneSalary(string careerId);

    IReadOnlyList<JobOpportunityInfo> GetJobOpportunities(
        IPerson person,
        int count = 5);

    JobApplicationResult ApplyForJob(
        IPerson person,
        string careerId,
        int jobLevel);

    void AssignCareer(
        IPerson person,
        string? careerId,
        int jobLevel,
        int jobSatisfaction);

    GeneratedCareerProfile GenerateCandidateCareer(
        Sex sex,
        TownInfo town,
        int year,
        int jobLevel,
        int strength,
        int intellect,
        int educationLevel,
        string deterministicKey);

    bool TryFindBetterJob(IPerson person);

    bool TryFindEmployment(
        IPerson person,
        double chanceBonus = 0);

    bool RelocateEmployment(IPerson person);
}
