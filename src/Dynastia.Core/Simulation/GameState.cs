using Dynastia.Contracts;
using Dynastia.Core.Entities;

namespace Dynastia.Core.Simulation;

public sealed class GameState : IGameState
{
    private readonly List<IPerson>
        _people = [];

    private readonly IGameRandom? _random;

    public GameState(IGameRandom? random = null)
    {
        _random = random;
    }

    public string DynastySurname { get; set; } =
        string.Empty;

    public int Year { get; set; } =
        GameCalendarConfiguration.GameStartYear;

    public int StartYear { get; set; } =
        GameCalendarConfiguration.GameStartYear;

    public IReadOnlyList<IPerson> People =>
        _people;

    public IPerson CreatePerson(
        string name,
        string surname,
        int age,
        Guid? id = null)
    {
        var person =
            new Person(
                name,
                surname,
                age,
                id ?? _random?.NextGuid());

        _people.Add(
            person);

        return person;
    }

    public void ClearPeople()
    {
        _people.Clear();
    }
}
