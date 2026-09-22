namespace Dynastia.Contracts;

public interface IEducationService
{
    void EnsureEducation(IPerson person);
    int GetEducationLevel(IPerson person);
    void SetEducationLevel(IPerson person, int level);
    void IncreaseEducation(IPerson person, int amount = 1);
    double GetPaidEducationSuccessChance(IPerson person);
    double GetPrivateTutorSuccessChance(IPerson person);
    int GetHelpedEducationCeiling(int year);

    int GetLocalEducationCeiling(IPerson person, int year);
    int GetLocalEducationCeiling(TownInfo town, int year);

    EducationGenerationRange GetGeneratedAdultRange(int year);
    EducationGenerationRange GetGeneratedAdultRange(int year, TownInfo town);
}
