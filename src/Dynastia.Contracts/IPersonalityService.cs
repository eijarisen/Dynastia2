namespace Dynastia.Contracts;

public interface IPersonalityService
{
    PersonalitySnapshot? GetPersonality(
        IPerson person);

    void ReconcileAll();

    bool ShiftMorals(IPerson person, int steps);

    bool HasMoralsProtection(IPerson person);

    void GrantMoralsProtection(IPerson person);
}
