using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed class RareEventDeathService
{
    private const double GriefHealthPenalty =
        15;

    private readonly IFamilyService _family;
    private readonly IHealthService _health;
    private readonly IGameRandom _random;
    private readonly IGameCalendar _calendar;
    private readonly IGameEventBus _events;

    public RareEventDeathService(
        IFamilyService family,
        IHealthService health,
        IGameRandom random,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        _family = family;
        _health = health;
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

        ApplyGrief(
            spouse);

        foreach (var child in
            children)
        {
            ApplyGrief(
                child);
        }

        ApplyGrief(
            father);

        ApplyGrief(
            mother);

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
                            $"{person.Age}."
                    }
            });

        return true;
    }

    private void ApplyGrief(
        IPerson? relative)
    {
        if (relative is null
            || relative.Tags.Has(
                "state.dead"))
        {
            return;
        }

        _health.ChangeHealth(
            relative,
            -GriefHealthPenalty);
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
