namespace Dynastia.Contracts;

public interface IPersonalityService
{
    PersonalitySnapshot? GetPersonality(
        IPerson person);

    void ReconcileAll();
}
