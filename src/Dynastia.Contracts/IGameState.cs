namespace Dynastia.Contracts;

public interface IGameState
{
    string DynastySurname { get; set; }

    int Year { get; set; }

    int StartYear { get; set; }

    IReadOnlyList<IPerson> People { get; }

    IPerson CreatePerson(
        string name,
        string surname,
        int age,
        Guid? id = null);

    void ClearPeople();
}
