namespace Dynastia.Contracts;

public interface ICareerService
{
    void EnsureCareer(IPerson person);
    CareerSnapshot GetCareer(IPerson person);
    void InitializeCareer(IPerson person, int jobLevel, int jobSatisfaction);
    void SetJobLevel(IPerson person, int jobLevel);
    void ChangeJobSatisfaction(IPerson person, int amount);
    void Retire(IPerson person);
    decimal GetAnnualIncome(IPerson person);
}
