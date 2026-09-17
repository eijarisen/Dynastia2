using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StandardStressService : IStressService
{
    public const double MaximumStress = 10;

    private readonly IGameState _state;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly StandardHealthService _health;
    private readonly IGameEventBus _events;
    private readonly IStressModifierRegistry _modifiers;

    public StandardStressService(
        IGameState state,
        IFamilyService family,
        IEconomyService economy,
        StandardHealthService health,
        IGameEventBus events,
        IStressModifierRegistry modifiers)
    {
        _state = state;
        _family = family;
        _economy = economy;
        _health = health;
        _events = events;
        _modifiers = modifiers;
    }

    public StressSnapshot GetStress(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        var contributions = new List<StressContribution>();
        AddEventStress(person, _events.GetEventsForYear(_state.Year), contributions);

        var household = _economy.GetHousehold(person);
        if (household is not null && household.Wealth <= 0)
            contributions.Add(new("economy.wealth_zero", 1));

        if (person.Age < 18)
        {
            if (person.Tags.Has("child.happiness.miserable"))
                contributions.Add(new("childhood.miserable", 2));
            else if (person.Tags.Has("child.happiness.unhappy"))
                contributions.Add(new("childhood.unhappy", 1));

            if (BothParentsDead(person))
                contributions.Add(new("family.orphaned", 1));

            if (person.Tags.Has("state.parents_divorced"))
                contributions.Add(new("family.parents_divorced", 1));
        }

        var health = _health.GetHealth(person);
        var seriousPressure = health.Conditions
            .Select(condition => _health.GetDefinition(condition.Id))
            .Where(definition => definition is not null
                                 && definition.Category.Equals("Serious", StringComparison.OrdinalIgnoreCase))
            .Select(definition => definition!.Course.Equals("Terminal", StringComparison.OrdinalIgnoreCase) ? 2.0 : 1.0)
            .DefaultIfEmpty(0)
            .Max();
        if (seriousPressure > 0)
            contributions.Add(new("health.serious_illness", seriousPressure));

        if (health.Percentage <= 25)
            contributions.Add(new("health.very_low", 2));
        else if (health.Percentage <= 40)
            contributions.Add(new("health.low", 1));

        contributions.AddRange(_modifiers.GetStressContributions(person, _state.Year));

        var normalized = contributions
            .Where(contribution => contribution.Value > 0)
            .GroupBy(contribution => contribution.SourceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new StressContribution(group.Key, group.Sum(item => item.Value)))
            .OrderByDescending(contribution => contribution.Value)
            .ThenBy(contribution => contribution.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new StressSnapshot(
            Math.Clamp(normalized.Sum(contribution => contribution.Value), 0, MaximumStress),
            normalized);
    }

    private void AddEventStress(
        IPerson person,
        IReadOnlyList<GameEvent> events,
        List<StressContribution> contributions)
    {
        foreach (var gameEvent in events)
        {
            var subject = Find(gameEvent.SubjectId);
            if (subject is null)
                continue;

            if (gameEvent.Type.Equals("life.death", StringComparison.OrdinalIgnoreCase))
            {
                if (subject.Id == person.Id)
                    continue;

                if (WasSpouseThisYear(person, subject, _state.Year))
                    contributions.Add(new("bereavement.spouse", 5));
                else if (IsParentOf(person, subject) || IsChildOf(person, subject))
                {
                    var same = SameHousehold(person, subject);
                    var value = same ? (IsParentOf(person, subject) ? 4 : 5) : 2;
                    contributions.Add(new(IsParentOf(person, subject) ? "bereavement.parent" : "bereavement.child", value));
                    if (person.Age < 18 && IsParentOf(person, subject) && BothParentsDead(person))
                        contributions.Add(new("family.orphaned_recent", 5));
                }
                else if (AreSiblings(person, subject))
                    contributions.Add(new("bereavement.sibling", 2));

                continue;
            }

            if (gameEvent.Type.Contains("divorce", StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals("relationship.affair", StringComparison.OrdinalIgnoreCase))
            {
                var partner = gameEvent.RelatedPersonIds
                    .Select(Find)
                    .FirstOrDefault(candidate => candidate is not null && candidate.Id != subject.Id);

                if (subject.Id == person.Id || partner?.Id == person.Id)
                    contributions.Add(new("relationship.divorce", 4));
                else if (partner is not null && IsSharedBiologicalChild(person, subject, partner))
                    contributions.Add(new("family.parental_divorce", person.Age < 18 ? 4 : 1));
                else if (!IsBiologicalChildOf(person, subject)
                         && (partner is null || !IsBiologicalChildOf(person, partner))
                         && IsCloseRelative(person, subject))
                    contributions.Add(new("relationship.relative_divorce", 1));

                continue;
            }

            if (gameEvent.Type.Equals("justice.crime", StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals("rare.wrongful_arrest", StringComparison.OrdinalIgnoreCase))
            {
                if (subject.Id == person.Id)
                    contributions.Add(new(gameEvent.Type.Equals("justice.crime", StringComparison.OrdinalIgnoreCase)
                        ? "justice.conviction"
                        : "justice.wrongful_arrest", 4));
                else if (WasSpouseThisYear(person, subject, _state.Year)
                         || IsParentOf(person, subject)
                         || IsChildOf(person, subject))
                    contributions.Add(new("justice.close_relative", SameHousehold(person, subject) ? 4 : 1));
                else if (IsCloseRelative(person, subject))
                    contributions.Add(new("justice.relative", 1));
                continue;
            }

            if (gameEvent.Type.Equals("justice.crime_uncaught", StringComparison.OrdinalIgnoreCase)
                && subject.Id == person.Id
                && gameEvent.Data.TryGetValue("category", out var category)
                && (category.Contains("violent", StringComparison.OrdinalIgnoreCase)
                    || category.Equals("extreme", StringComparison.OrdinalIgnoreCase)))
            {
                contributions.Add(new("justice.violent_incident", 2));
                continue;
            }

            if (gameEvent.Type is "rare.assault" or "rare.workplace_accident" or "rare.traffic_accident" or "rare.structural_accident")
            {
                if (subject.Id == person.Id && HasTraumaticDamage(gameEvent))
                    contributions.Add(new("trauma.major_incident", 3));
                continue;
            }

            if (gameEvent.Type.Equals("career.fired", StringComparison.OrdinalIgnoreCase)
                && subject.Id == person.Id)
            {
                contributions.Add(new("career.job_loss", 2));
            }
        }
    }

    private static bool HasTraumaticDamage(GameEvent gameEvent)
    {
        if (gameEvent.Type.Equals("rare.assault", StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals("rare.workplace_accident", StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals("rare.traffic_accident", StringComparison.OrdinalIgnoreCase))
            return true;

        return gameEvent.Data.TryGetValue("healthDamage", out var raw)
               && double.TryParse(raw, out var damage)
               && damage > 0;
    }

    private bool WasSpouseThisYear(IPerson person, IPerson other, int year) =>
        _family.GetSpouse(person)?.Id == other.Id
        || _family.GetRelationshipHistory(person).Any(relationship =>
            relationship.SpouseId == other.Id
            && relationship.StartYear <= year
            && (relationship.EndYear is null || relationship.EndYear >= year));

    private bool SameHousehold(IPerson first, IPerson second) =>
        _economy.GetHouseholdId(first) is Guid householdId
        && _economy.GetHouseholdId(second) == householdId;

    private bool BothParentsDead(IPerson person) =>
        (_family.GetFather(person) is not { } father || father.Tags.Has("state.dead"))
        && (_family.GetMother(person) is not { } mother || mother.Tags.Has("state.dead"));

    private bool IsParentOf(IPerson person, IPerson other) =>
        _family.GetFather(person)?.Id == other.Id || _family.GetMother(person)?.Id == other.Id;

    private bool IsBiologicalChildOf(IPerson child, IPerson parent) =>
        _family.GetFather(child)?.Id == parent.Id || _family.GetMother(child)?.Id == parent.Id;

    private bool IsSharedBiologicalChild(IPerson child, IPerson firstParent, IPerson secondParent)
    {
        var fatherId = _family.GetFather(child)?.Id;
        var motherId = _family.GetMother(child)?.Id;
        return (fatherId == firstParent.Id && motherId == secondParent.Id)
               || (fatherId == secondParent.Id && motherId == firstParent.Id);
    }

    private bool IsChildOf(IPerson person, IPerson other) =>
        _family.GetChildren(person).Any(child => child.Id == other.Id);

    private bool IsCloseRelative(IPerson person, IPerson other) =>
        IsParentOf(person, other) || IsChildOf(person, other) || AreSiblings(person, other);

    private bool AreSiblings(IPerson first, IPerson second)
    {
        var father = _family.GetFather(first);
        if (father is not null && _family.GetChildren(father).Any(child => child.Id == second.Id))
            return true;
        var mother = _family.GetMother(first);
        return mother is not null && _family.GetChildren(mother).Any(child => child.Id == second.Id);
    }

    private IPerson? Find(Guid? id) =>
        id is Guid personId ? _state.People.FirstOrDefault(person => person.Id == personId) : null;
}
