using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class StandardStressService : IStressService
{
    public const double MaximumStress = StressScale.Maximum;

    private static readonly double[] EventStressDecay =
        [1.0, 0.65, 0.35, 0.15];

    private readonly IGameState _state;
    private readonly IPersonLookup? _people;
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
        : this(
            state,
            state as IPersonLookup,
            family,
            economy,
            health,
            events,
            modifiers)
    {
    }

    public StandardStressService(
        IGameState state,
        IPersonLookup? people,
        IFamilyService family,
        IEconomyService economy,
        StandardHealthService health,
        IGameEventBus events,
        IStressModifierRegistry modifiers)
    {
        _state = state;
        _people = people;
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
        var currentYearEvents =
            _events.GetEventsForYear(_state.Year);

        // Major life events fade rather than disappearing after one year.
        // The current year is fully salient, then the effect tapers over the
        // following three years.
        for (var yearsAgo = 0; yearsAgo < EventStressDecay.Length; yearsAgo++)
        {
            var eventYear = _state.Year - yearsAgo;
            AddEventStress(
                person,
                yearsAgo == 0
                    ? currentYearEvents
                    : _events.GetEventsForYear(eventYear),
                contributions,
                eventYear,
                EventStressDecay[yearsAgo]);
        }

        var household = _economy.GetHousehold(person);
        if (household?.HasUnfundedBasicNeeds == true)
            contributions.Add(new("economy.basic_needs_shortfall", 1));

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

        var lifestyleStress = HouseholdLifestyleRules.GetStressAdjustment(person);
        if (lifestyleStress > 0)
            contributions.Add(new StressContribution("household.lifestyle.thrifty", lifestyleStress));

        var normalized = contributions
            .Where(contribution => contribution.Value > 0)
            .GroupBy(contribution => contribution.SourceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new StressContribution(group.Key, group.Sum(item => item.Value)))
            .OrderByDescending(contribution => contribution.Value)
            .ThenBy(contribution => contribution.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var drinkRelief = currentYearEvents
            .Where(gameEvent => gameEvent.SubjectId == person.Id
                && gameEvent.Type.Equals("wellbeing.drink", StringComparison.OrdinalIgnoreCase))
            .Sum(gameEvent =>
            {
                if (gameEvent.Data.TryGetValue("stressRelief", out var raw)
                    && double.TryParse(
                        raw,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var parsed))
                {
                    return Math.Max(0, parsed);
                }

                return 0.0;
            });

        var lifestyleRelief = lifestyleStress < 0
            ? -lifestyleStress
            : 0.0;

        var legacyTotal = Math.Clamp(
            normalized.Sum(contribution => contribution.Value)
            - drinkRelief
            - lifestyleRelief,
            0,
            StressScale.LegacyMaximum);

        return new StressSnapshot(
            StressScale.FromLegacy(legacyTotal),
            normalized
                .Select(contribution => new StressContribution(
                    contribution.SourceId,
                    StressScale.FromLegacy(contribution.Value)))
                .ToList());
    }

    private void AddEventStress(
        IPerson person,
        IReadOnlyList<GameEvent> events,
        List<StressContribution> contributions,
        int eventYear,
        double multiplier)
    {
        void Add(string sourceId, double value)
        {
            var scaled = value * multiplier;
            if (scaled > 0.0001)
                contributions.Add(new StressContribution(sourceId, scaled));
        }
        foreach (var gameEvent in events)
        {
            if (gameEvent.Type.Equals(
                    "historical.household_impact",
                    StringComparison.OrdinalIgnoreCase)
                && (gameEvent.SubjectId == person.Id
                    || gameEvent.RelatedPersonIds.Contains(person.Id))
                && gameEvent.Data.TryGetValue("stressGain", out var historicalStressRaw)
                && double.TryParse(
                    historicalStressRaw,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var historicalStress)
                && historicalStress > 0)
            {
                var sourceId = gameEvent.Data.TryGetValue("eventId", out var historicalEventId)
                    ? $"historical.{historicalEventId}"
                    : "historical.event";
                Add(sourceId, historicalStress);
                continue;
            }

            var subject = Find(gameEvent.SubjectId);
            if (subject is null)
                continue;

            if (gameEvent.Type.Equals("life.death", StringComparison.OrdinalIgnoreCase))
            {
                if (subject.Id == person.Id)
                    continue;

                void AddBereavement(string sourceId, double value) =>
                    Add(
                        sourceId,
                        value * FamilyShockRules.BereavementStressMultiplier);

                if (WasSpouseThisYear(person, subject, eventYear))
                    AddBereavement("bereavement.spouse", 5);
                else if (IsParentOf(person, subject) || IsChildOf(person, subject))
                {
                    var same = SameHousehold(person, subject);
                    var value = same ? (IsParentOf(person, subject) ? 4 : 5) : 2;
                    AddBereavement(
                        IsParentOf(person, subject) ? "bereavement.parent" : "bereavement.child",
                        value);
                    if (person.Age < 18 && IsParentOf(person, subject) && BothParentsDead(person))
                        AddBereavement("family.orphaned_recent", 5);
                }
                else if (AreSiblings(person, subject))
                    AddBereavement("bereavement.sibling", 2);

                continue;
            }

            if (gameEvent.Type.Contains("divorce", StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals("relationship.affair", StringComparison.OrdinalIgnoreCase))
            {
                var partner = gameEvent.RelatedPersonIds
                    .Select(id => Find(id))
                    .FirstOrDefault(candidate => candidate is not null && candidate.Id != subject.Id);

                if (subject.Id == person.Id || partner?.Id == person.Id)
                    Add("relationship.divorce", 4);
                else if (partner is not null && IsSharedBiologicalChild(person, subject, partner))
                    Add("family.parental_divorce", person.Age < 18 ? 4 : 1);
                else if (!IsBiologicalChildOf(person, subject)
                         && (partner is null || !IsBiologicalChildOf(person, partner))
                         && IsCloseRelative(person, subject))
                    Add("relationship.relative_divorce", 1);

                continue;
            }

            if (gameEvent.Type.Equals("justice.crime", StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals("rare.wrongful_arrest", StringComparison.OrdinalIgnoreCase))
            {
                if (subject.Id == person.Id)
                    Add(gameEvent.Type.Equals("justice.crime", StringComparison.OrdinalIgnoreCase)
                        ? "justice.conviction"
                        : "justice.wrongful_arrest", 4);
                else if (WasSpouseThisYear(person, subject, eventYear)
                         || IsParentOf(person, subject)
                         || IsChildOf(person, subject))
                    Add("justice.close_relative", SameHousehold(person, subject) ? 4 : 1);
                else if (IsCloseRelative(person, subject))
                    Add("justice.relative", 1);
                continue;
            }

            if (gameEvent.Type.Equals("justice.crime_uncaught", StringComparison.OrdinalIgnoreCase)
                && subject.Id == person.Id
                && IsSevereCrimeEvent(gameEvent))
            {
                Add("justice.violent_incident", 2);
                continue;
            }

            if (gameEvent.Type is "rare.assault" or "rare.workplace_accident" or "rare.traffic_accident" or "rare.structural_accident")
            {
                if (subject.Id == person.Id && HasTraumaticDamage(gameEvent))
                    Add("trauma.major_incident", 3);
                continue;
            }

            if (gameEvent.Type.Equals("career.fired", StringComparison.OrdinalIgnoreCase)
                && subject.Id == person.Id)
            {
                Add("career.job_loss", 2);
            }
        }
    }


    private static bool IsSevereCrimeEvent(GameEvent gameEvent)
    {
        if (gameEvent.Data.TryGetValue("behaviorTags", out var tags))
        {
            var parsed = tags.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parsed.Contains("violent", StringComparer.OrdinalIgnoreCase)
                || parsed.Contains("severe", StringComparer.OrdinalIgnoreCase)
                || parsed.Contains("extreme", StringComparer.OrdinalIgnoreCase);
        }

        // Compatibility fallback for events saved before explicit behavior tags.
        return gameEvent.Data.TryGetValue("category", out var category)
            && (category.Contains("violent", StringComparison.OrdinalIgnoreCase)
                || category.Equals("extreme", StringComparison.OrdinalIgnoreCase));
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
        _people?.FindPerson(id)
        ?? (id is Guid personId
            ? _state.People.FirstOrDefault(person => person.Id == personId)
            : null);
}
