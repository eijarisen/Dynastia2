using Dynastia.Contracts;

namespace Dynastia.Mechanics.ReligiousVocation;

internal sealed class ReligiousVocationYearSystem : IYearSystem
{
    private readonly IFamilyService _family;
    private readonly ICareerService _career;
    private readonly ILocationService _locations;
    private readonly ITownInstitutionService _institutions;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly ReligiousCallingRules _rules;

    public ReligiousVocationYearSystem(
        IFamilyService family,
        ICareerService career,
        ILocationService locations,
        ITownInstitutionService institutions,
        IGameRandom random,
        IGameEventBus events,
        ReligiousCallingRules rules)
    {
        _family = family;
        _career = career;
        _locations = locations;
        _institutions = institutions;
        _random = random;
        _events = events;
        _rules = rules;
    }

    public string Id => "religion.calling";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => ["career.retirement"];
    public IReadOnlyCollection<string> After => ["aging.increment_age"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People.ToList())
        {
            if (!person.Tags.Has("state.alive")
                || person.Tags.Has("simulation.peripheral_inactive"))
            {
                continue;
            }

            if (person.Tags.Has(_rules.ActiveTag))
            {
                TryLeaveVocation(gameState, person);
                continue;
            }

            TryCalling(gameState, person);
        }
    }

    private void TryCalling(IGameState gameState, IPerson person)
    {
        if (person.Age != _rules.CallingAge
            || person.Tags.Has(_rules.CheckedTag))
        {
            return;
        }

        // Everyone is checked exactly once at age 18. Being married is a
        // permanent exclusion and therefore still consumes the one-time roll.
        person.Tags.Add(_rules.CheckedTag);

        if (_family.GetSpouse(person) is not null)
            return;

        if (_random.NextDouble() >= _rules.CallingChance)
            return;

        var town = _locations.GetLocation(person).HomeTown;
        if (_institutions.Resolve(town, gameState.Year).GetTier("church") < 1)
            return;

        var sex = _family.GetSex(person);
        var careerId = _rules.CareerFor(sex);
        var before = _career.GetCareer(person);

        // AssignCareer is the authoritative exclusivity path: it ends normal
        // employment, Craft self-employment and an active Life of Crime.
        _career.AssignCareer(
            person,
            careerId,
            1,
            Math.Clamp(before.JobSatisfaction, 1, 5));

        person.Tags.Remove(_rules.FormerTag);
        person.Tags.Add(_rules.ActiveTag);

        var eventType = sex == Sex.Female
            ? "religion.calling_nun"
            : "religion.calling_priest";
        var text = sex == Sex.Female
            ? $"At age 18, {_family.GetDisplayName(person)} entered religious life as a nun."
            : $"At age 18, {_family.GetDisplayName(person)} entered the priesthood.";

        _events.Publish(new GameEvent
        {
            Type = eventType,
            Year = gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["careerId"] = careerId,
                ["text"] = text
            }
        });
    }

    private void TryLeaveVocation(IGameState gameState, IPerson person)
    {
        if (person.Age < _rules.DepartureMinimumAge
            || _random.NextDouble() >= _rules.DepartureChance)
        {
            return;
        }

        var before = _career.GetCareer(person);
        _career.AssignCareer(
            person,
            null,
            0,
            Math.Clamp(before.JobSatisfaction, 1, 5));

        person.Tags.Remove(_rules.ActiveTag);
        person.Tags.Add(_rules.FormerTag);
        person.Tags.Add(_rules.CheckedTag);

        _events.Publish(new GameEvent
        {
            Type = "religion.vocation_left",
            Year = gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["text"] =
                    $"{_family.GetDisplayName(person)} left religious life and returned to ordinary secular life."
            }
        });
    }
}
