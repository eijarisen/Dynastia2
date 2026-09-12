using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed class RareEventDeathService
{
    private const double GriefHealthPenalty =
        15;

    private readonly IFamilyService _family;
    private readonly IHealthService _health;
    private readonly IEconomyService _economy;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;
    private readonly IGameEventBus _events;

    public RareEventDeathService(
        IFamilyService family,
        IHealthService health,
        IEconomyService economy,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        _family = family;
        _health = health;
        _economy = economy;
        _random = random;
        _calendar = calendar;
        _events = events;
    }

    public bool Kill(
        IGameState gameState,
        IPerson person,
        string cause)
    {
        if (person.Tags.Has(
            "state.dead"))
        {
            return false;
        }

        var spouse =
            _family.GetSpouse(
                person);

        var children =
            _family.GetChildren(
                person)
            .ToList();

        var father =
            _family.GetFather(
                person);

        var mother =
            _family.GetMother(
                person);

        var siblings =
            GetSiblings(person);

        _health.SetHealth(
            person,
            0);

        person.Tags.Remove(
            "state.alive");

        person.Tags.Remove(
            "control.playable");

        person.Tags.Add(
            "state.dead");

        person.DeathDate =
            RandomDateInYear(
                gameState.Year);

        var related =
            new List<Guid>();

        if (spouse is not null)
            related.Add(
                spouse.Id);

        related.AddRange(
            children.Select(
                child =>
                    child.Id));

        if (father is not null)
            related.Add(
                father.Id);

        if (mother is not null)
            related.Add(
                mother.Id);

        related.AddRange(
            siblings.Select(sibling => sibling.Id));

        ApplyGrief(
            spouse,
            person,
            extendedRelative: false);

        foreach (var child in
            children)
        {
            ApplyGrief(
                child,
                person,
                extendedRelative: true);
        }

        ApplyGrief(
            father,
            person,
            extendedRelative: true);

        ApplyGrief(
            mother,
            person,
            extendedRelative: true);

        foreach (var sibling in siblings)
        {
            ApplyGrief(
                sibling,
                person,
                extendedRelative: true);
        }

        if (spouse is not null
            && !spouse.Tags.Has(
                "state.dead"))
        {
            // Keep the deceased person's spouse reference, but end the
            // surviving spouse's current relationship and close the
            // marriage-history record. This matches the intended death
            // semantics without relying on the removed ClearCurrentSpouse API.
            _family.EndRelationship(
                person,
                spouse,
                gameState.Year,
                "death",
                clearFirst:
                    false,
                clearSecond:
                    true);
        }

        _events.Publish(
            new GameEvent
            {
                Type =
                    "life.death",

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                RelatedPersonIds =
                    related
                        .Distinct()
                        .ToList(),

                Data =
                    new Dictionary<string, string>
                    {
                        ["cause"] =
                            cause,

                        ["age"] =
                            person.Age.ToString(),

                        ["text"] =
                            $"{person.Name} " +
                            $"{person.Surname} " +
                            $"died at age " +
                            $"{person.Age}." +
                            BuildSurvivorText(person, spouse, children)
                    }
            });

        return true;
    }

    private string BuildSurvivorText(
        IPerson person,
        IPerson? spouse,
        IReadOnlyList<IPerson> children)
    {
        var livingSpouse = spouse is not null && spouse.Tags.Has("state.alive")
            ? spouse
            : null;
        var livingChildren = children
            .Where(child => child.Tags.Has("state.alive"))
            .Select(child => child.Name)
            .ToList();

        if (livingSpouse is null && livingChildren.Count == 0)
            return string.Empty;

        var pronoun = _family.GetSex(person) == Sex.Female ? " She" : " He";
        var parts = new List<string>();
        if (livingSpouse is not null)
            parts.Add($"spouse {livingSpouse.Name}");
        if (livingChildren.Count > 0)
            parts.Add(livingChildren.Count == 1
                ? $"child {livingChildren[0]}"
                : $"children {string.Join(", ", livingChildren)}");

        return $"{pronoun} left " + string.Join(" and ", parts) + ".";
    }

    private void ApplyGrief(
        IPerson? relative,
        IPerson deceased,
        bool extendedRelative)
    {
        if (relative is null
            || relative.Tags.Has(
                "state.dead")
            || SimulationState.IsInactive(relative))
        {
            return;
        }

        var penalty =
            FamilyShockRules.ScaleHealthLoss(
                relative,
                GriefHealthPenalty,
                AreInSameHousehold(relative, deceased),
                extendedRelative);

        _health.ChangeHealth(
            relative,
            -penalty);
    }

    private IReadOnlyList<IPerson> GetSiblings(
        IPerson person)
    {
        var result =
            new Dictionary<Guid, IPerson>();

        var father = _family.GetFather(person);
        var mother = _family.GetMother(person);

        if (father is not null)
        {
            foreach (var sibling in _family.GetChildren(father))
            {
                if (sibling.Id != person.Id)
                    result[sibling.Id] = sibling;
            }
        }

        if (mother is not null)
        {
            foreach (var sibling in _family.GetChildren(mother))
            {
                if (sibling.Id != person.Id)
                    result[sibling.Id] = sibling;
            }
        }

        return result.Values.ToList();
    }

    private bool AreInSameHousehold(
        IPerson first,
        IPerson second)
    {
        var firstId = _economy.GetHouseholdId(first);
        var secondId = _economy.GetHouseholdId(second);

        return firstId is not null
            && secondId is not null
            && firstId == secondId;
    }

    private GameDate RandomDateInYear(
        int year)
    {
        var month =
            _random.NextInt(
                1,
                12);

        var day =
            _random.NextInt(
                1,
                _calendar.GetDaysInMonth(
                    year,
                    month));

        return new GameDate(
            year,
            month,
            day);
    }
}
