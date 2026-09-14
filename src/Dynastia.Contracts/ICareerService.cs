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

    bool TryFindBetterJob(IPerson person);

    bool TryFindEmployment(
        IPerson person,
        double chanceBonus = 0);

    bool RelocateEmployment(IPerson person);
}
