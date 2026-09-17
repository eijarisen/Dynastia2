namespace Dynastia.Contracts;

public interface IHobbyService
{
    HobbyPersonSnapshot GetHobbies(IPerson person);

    IReadOnlyList<HobbyInfo> GenerateCandidateHobbies(
        Guid candidateId,
        Sex sex,
        int age,
        int year,
        string temperament,
        SettlementClass settlementClass,
        int strength,
        int intellect,
        int appeal);

    void SetHobbies(
        IPerson person,
        IReadOnlyCollection<string> hobbyIds);

    void ReconcileAll();

    void ReconcileAfterLoad();
}
