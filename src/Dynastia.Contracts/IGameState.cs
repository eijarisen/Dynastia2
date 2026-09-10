namespace Dynastia.Contracts;

public interface IGameState
{
    int Year { get; set; }

    IReadOnlyList<IPerson> People { get; }

    IPerson CreatePerson(
        string name,
        string surname,
        int age);
}