using Dynastia.Contracts;
using Dynastia.Core.Entities;

namespace Dynastia.Core.Simulation;

public sealed class GameState : IGameState
{
    private readonly List<IPerson> _people = [];

    public int Year { get; set; } = 1900;

    public IReadOnlyList<IPerson> People => _people;

    public IPerson CreatePerson(
        string name,
        string surname,
        int age)
    {
        var person = new Person(
            name,
            surname,
            age);

        _people.Add(person);

        return person;
    }
}