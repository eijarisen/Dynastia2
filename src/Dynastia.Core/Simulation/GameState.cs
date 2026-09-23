using Dynastia.Contracts;
using Dynastia.Core.Entities;

namespace Dynastia.Core.Simulation;

public sealed class GameState : IGameState, IPersonLookup
{
    private readonly List<IPerson>
        _people = [];

    private readonly Dictionary<Guid, IPerson>
        _peopleById = [];

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

        // Preserve the existing FirstOrDefault lookup semantics for invalid
        // duplicate IDs: the first person remains the lookup result. Save
        // validation remains responsible for rejecting duplicate IDs.
        _peopleById.TryAdd(
            person.Id,
            person);

        return person;
    }

    public IPerson? FindPerson(Guid id)
    {
        return _peopleById.GetValueOrDefault(id);
    }

    public IPerson? FindPerson(Guid? id)
    {
        return id is Guid personId
            ? FindPerson(personId)
            : null;
    }

    public void ClearPeople()
    {
        _people.Clear();
        _peopleById.Clear();
    }
}
