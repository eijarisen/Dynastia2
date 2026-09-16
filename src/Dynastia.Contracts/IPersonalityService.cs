namespace Dynastia.Contracts;

public interface IPersonalityService
{
    PersonalitySnapshot? GetPersonality(
        IPerson person);

    PersonalitySnapshot GenerateCandidatePersonality(
        Guid candidateId);

    void SetPersonality(
        IPerson person,
        PersonalitySnapshot personality);

    void ReconcileAll();

    bool ShiftMorals(IPerson person, int steps);

    bool HasMoralsProtection(IPerson person);

    void GrantMoralsProtection(IPerson person);
}
