namespace Dynastia.Contracts;

public interface IEducationService
{
    void EnsureEducation(IPerson person);
    int GetEducationLevel(IPerson person);
    void SetEducationLevel(IPerson person, int level);
    void IncreaseEducation(IPerson person, int amount = 1);

    EducationGenerationRange GetGeneratedAdultRange(int year);
}
