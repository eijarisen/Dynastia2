using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

internal sealed class MentalHealthYearSystem : IYearSystem
{
    private readonly StandardHealthService _health;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public MentalHealthYearSystem(StandardHealthService health, IFamilyService family, IEconomyService economy, IGameRandom random, IGameEventBus events)
    {
        _health = health;
        _family = family;
        _economy = economy;
        _random = random;
        _events = events;
    }

    public string Id => "health.life_stress";
    public YearPhase Phase => YearPhase.PostYear;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        var yearEvents = _events.GetEventsForYear(gameState.Year);
        foreach (var person in gameState.People.Where(p => p.Tags.Has("state.alive") && !SimulationState.IsInactive(p)))
        {
            var stress = CalculateStress(gameState, person, yearEvents);
            var household = _economy.GetHousehold(person);
            if (household is not null && household.Wealth <= 0)
                stress += 1;

            if (person.Age < 18)
            {
                if (person.Tags.Has("child.happiness.miserable")) stress += 2;
                else if (person.Tags.Has("child.happiness.unhappy")) stress += 1;
            }

            if (stress <= 0 || person.Age < 5)
                continue;

            var hasAnxiety = _health.HasCondition(person, "anxiety");
            var hasDepression = _health.HasCondition(person, "depression");
            if (hasAnxiety && hasDepression)
                continue;

            var chance = 0.0015 + stress * 0.011;
            chance *= TemperamentMultiplier(person);
            if (hasAnxiety || hasDepression)
                chance *= 0.55;
            chance = Math.Clamp(chance, 0, 0.12);

            if (_random.NextDouble() >= chance)
                continue;

            var depressionChance = person.Tags.Has("personality.melancholic") ? 0.65
                : person.Tags.Has("personality.choleric") ? 0.35 : 0.50;
            var condition = _random.NextDouble() < depressionChance ? "depression" : "anxiety";
            if ((condition == "depression" && hasDepression) || (condition == "anxiety" && hasAnxiety))
                condition = condition == "depression" ? "anxiety" : "depression";

            if (!_health.AddCondition(person, condition))
                continue;

            var info = _health.GetDefinition(condition);
            _events.Publish(new GameEvent
            {
                Type = "health.illness",
                Year = gameState.Year,
                SubjectId = person.Id,
                Data = new Dictionary<string, string>
                {
                    ["conditionId"] = condition,
                    ["condition"] = info?.Name ?? condition,
                    ["familyNews"] = "false",
                    ["lifeStress"] = stress.ToString(),
                    ["text"] = $"{_family.GetDisplayName(person)} developed {info?.Name ?? condition} after a difficult period."
                }
            });
        }
    }

    private int CalculateStress(IGameState state, IPerson person, IReadOnlyList<GameEvent> events)
    {
        var stress = 0;
        foreach (var e in events)
        {
            var subject = Find(state, e.SubjectId);
            if (subject is null) continue;

            if (e.Type.Equals("life.death", StringComparison.OrdinalIgnoreCase))
            {
                if (subject.Id == person.Id) continue;
                if (WasSpouseThisYear(person, subject, state.Year)) stress += 5;
                else if (IsParentOf(person, subject) || IsChildOf(person, subject))
                {
                    var same = SameHousehold(person, subject);
                    stress += same ? (IsParentOf(person, subject) ? 4 : 5) : 2;
                    if (person.Age < 18 && IsParentOf(person, subject) && BothParentsDead(person)) stress += 5;
                }
                else if (AreSiblings(person, subject)) stress += 2;
            }
            else if (e.Type.Contains("divorce", StringComparison.OrdinalIgnoreCase) || e.Type.Equals("relationship.affair", StringComparison.OrdinalIgnoreCase))
            {
                var related = e.RelatedPersonIds.Contains(person.Id);
                if (subject.Id == person.Id || related) stress += 4;
                else if (person.Age < 18 && IsParentOf(person, subject) && e.RelatedPersonIds.Any(id => IsParentId(person, id))) stress += 4;
                else if (IsCloseRelative(person, subject)) stress += 1;
            }
            else if (e.Type.Equals("justice.crime", StringComparison.OrdinalIgnoreCase))
            {
                if (subject.Id == person.Id) stress += 4;
                else if (WasSpouseThisYear(person, subject, state.Year) || IsParentOf(person, subject) || IsChildOf(person, subject)) stress += SameHousehold(person, subject) ? 4 : 1;
                else if (IsCloseRelative(person, subject)) stress += 1;
            }
            else if (e.Type.Equals("rare.wrongful_arrest", StringComparison.OrdinalIgnoreCase))
            {
                if (subject.Id == person.Id) stress += 4;
                else if (WasSpouseThisYear(person, subject, state.Year) || IsParentOf(person, subject) || IsChildOf(person, subject)) stress += SameHousehold(person, subject) ? 4 : 1;
                else if (IsCloseRelative(person, subject)) stress += 1;
            }
            else if (e.Type.Equals("justice.crime_uncaught", StringComparison.OrdinalIgnoreCase)
                     && subject.Id == person.Id
                     && e.Data.TryGetValue("category", out var crimeCategory)
                     && (crimeCategory.Contains("violent", StringComparison.OrdinalIgnoreCase)
                         || crimeCategory.Equals("extreme", StringComparison.OrdinalIgnoreCase)))
            {
                stress += 2;
            }
            else if (e.Type is "rare.assault" or "rare.workplace_accident" or "rare.traffic_accident" or "rare.structural_accident")
            {
                if (subject.Id == person.Id && HasTraumaticDamage(e)) stress += 3;
            }
        }
        return stress;
    }

    private static bool HasTraumaticDamage(GameEvent e)
    {
        if (e.Type.Equals("rare.assault", StringComparison.OrdinalIgnoreCase)
            || e.Type.Equals("rare.workplace_accident", StringComparison.OrdinalIgnoreCase)
            || e.Type.Equals("rare.traffic_accident", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return e.Data.TryGetValue("healthDamage", out var raw)
            && double.TryParse(raw, out var damage)
            && damage > 0;
    }

    private bool WasSpouseThisYear(IPerson person, IPerson other, int year) =>
        _family.GetSpouse(person)?.Id == other.Id
        || _family.GetRelationshipHistory(person).Any(r =>
            r.SpouseId == other.Id
            && r.StartYear <= year
            && (r.EndYear is null || r.EndYear >= year));

    private bool SameHousehold(IPerson a, IPerson b) => _economy.GetHouseholdId(a) is Guid x && _economy.GetHouseholdId(b) == x;
    private bool BothParentsDead(IPerson p) => (_family.GetFather(p) is not { } f || f.Tags.Has("state.dead")) && (_family.GetMother(p) is not { } m || m.Tags.Has("state.dead"));
    private bool IsParentOf(IPerson p, IPerson other) => _family.GetFather(p)?.Id == other.Id || _family.GetMother(p)?.Id == other.Id;
    private bool IsParentId(IPerson p, Guid id) => _family.GetFather(p)?.Id == id || _family.GetMother(p)?.Id == id;
    private bool IsChildOf(IPerson p, IPerson other) => _family.GetChildren(p).Any(c => c.Id == other.Id);
    private bool IsCloseRelative(IPerson p, IPerson other) => IsParentOf(p, other) || IsChildOf(p, other) || AreSiblings(p, other);
    private bool AreSiblings(IPerson a, IPerson b)
    {
        var father = _family.GetFather(a);
        if (father is not null && _family.GetChildren(father).Any(c => c.Id == b.Id))
            return true;

        var mother = _family.GetMother(a);
        return mother is not null && _family.GetChildren(mother).Any(c => c.Id == b.Id);
    }
    private static IPerson? Find(IGameState s, Guid? id) => id is Guid x ? s.People.FirstOrDefault(p => p.Id == x) : null;
    private static double TemperamentMultiplier(IPerson p) => p.Tags.Has("personality.melancholic") ? 1.60 : p.Tags.Has("personality.choleric") ? 1.35 : p.Tags.Has("personality.sanguine") ? 0.65 : p.Tags.Has("personality.phlegmatic") ? 0.55 : 1.0;
}
