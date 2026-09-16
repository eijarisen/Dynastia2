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
            var hasAlcoholism = _health.HasCondition(person, "alcoholism");

            var candidates = new List<(string Id, double Weight)>();

            if (!hasDepression)
            {
                candidates.Add((
                    "depression",
                    MentalHealthStressRules.GetDepressionWeight(person)));
            }

            if (!hasAnxiety)
            {
                candidates.Add((
                    "anxiety",
                    MentalHealthStressRules.GetAnxietyWeight(person)));
            }

            if (!hasAlcoholism)
            {
                var alcoholismWeight =
                    MentalHealthStressRules.GetAlcoholismWeight(
                        person,
                        stress);

                if (alcoholismWeight > 0)
                    candidates.Add(("alcoholism", alcoholismWeight));
            }

            if (candidates.Count == 0)
                continue;

            var existingStressConditions =
                (hasAnxiety ? 1 : 0)
                + (hasDepression ? 1 : 0)
                + (hasAlcoholism ? 1 : 0);

            var chance = MentalHealthStressRules.GetReactionChance(
                stress,
                person,
                existingStressConditions);

            if (_random.NextDouble() >= chance)
                continue;

            var totalWeight = candidates.Sum(candidate => candidate.Weight);
            var roll = _random.NextDouble() * totalWeight;
            var condition = candidates[^1].Id;

            foreach (var candidate in candidates)
            {
                if (roll < candidate.Weight)
                {
                    condition = candidate.Id;
                    break;
                }

                roll -= candidate.Weight;
            }

            if (!_health.AddCondition(person, condition, gameState.Year))
                continue;

            var conditionName =
                _health.GetHealth(person).Conditions
                    .First(entry => entry.Id.Equals(condition, StringComparison.OrdinalIgnoreCase))
                    .Name;
            _events.Publish(new GameEvent
            {
                Type = "health.illness",
                Year = gameState.Year,
                SubjectId = person.Id,
                Data = new Dictionary<string, string>
                {
                    ["conditionId"] = condition,
                    ["condition"] = conditionName,
                    ["familyNews"] = "false",
                    ["lifeStress"] = stress.ToString(),
                    ["text"] = $"{_family.GetDisplayName(person)} developed {conditionName} after a difficult period."
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
                var partner =
                    e.RelatedPersonIds
                        .Select(id => Find(state, id))
                        .FirstOrDefault(candidate => candidate is not null && candidate.Id != subject.Id);

                if (subject.Id == person.Id || partner?.Id == person.Id)
                {
                    stress += 4;
                }
                else if (partner is not null && IsSharedBiologicalChild(person, subject, partner))
                {
                    // Shared minors receive the full parental-divorce shock.
                    // Shared adult children hear about the breakup but do not
                    // move or receive the child divorce state.
                    stress += person.Age < 18 ? 4 : 1;
                }
                else if (IsBiologicalChildOf(person, subject)
                         || (partner is not null && IsBiologicalChildOf(person, partner)))
                {
                    // A child from either person's previous relationship is
                    // not a child of this marriage and receives no divorce
                    // stress from this couple's breakup.
                }
                else if (IsCloseRelative(person, subject))
                {
                    stress += 1;
                }
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
    private bool IsBiologicalChildOf(IPerson child, IPerson parent) => IsParentId(child, parent.Id);
    private bool IsSharedBiologicalChild(IPerson child, IPerson firstParent, IPerson secondParent)
    {
        var fatherId = _family.GetFather(child)?.Id;
        var motherId = _family.GetMother(child)?.Id;

        return (fatherId == firstParent.Id && motherId == secondParent.Id)
            || (fatherId == secondParent.Id && motherId == firstParent.Id);
    }
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
}
