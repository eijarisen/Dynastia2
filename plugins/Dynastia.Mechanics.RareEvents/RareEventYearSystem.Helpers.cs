using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
    private double GetSuicideChance(
        IPerson person)
    {
        var health =
            _health.GetHealth(
                person);

        var chance =
            BaseSuicideChance;

        if (HasCondition(
            health,
            "depression"))
        {
            chance *=
                20;
        }

        if (HasCondition(
            health,
            "alcoholism"))
        {
            chance *=
                3;
        }

        if (HasCondition(
            health,
            "anxiety"))
        {
            chance *=
                2;
        }

        if (_recent.HasFlag(
            person,
            "recent.bereavement"))
        {
            chance *=
                3;
        }

        if (_recent.HasFlag(
            person,
            "recent.divorce"))
        {
            chance *=
                2;
        }

        if (_recent.HasFlag(
                person,
                "recent.job_loss")
            && IsHouseholdBroke(
                person))
        {
            chance *=
                2;
        }

        if (health.Percentage < 25)
        {
            chance *=
                2;
        }

        return Math.Min(
            chance,
            MaximumSuicideChance);
    }

    private static bool HasCondition(
        HealthSnapshot health,
        string name)
    {
        return health.Conditions.Any(
            condition =>
                condition.Id.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase)
                || condition.Name.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase));
    }

    private bool IsHouseholdBroke(
        IPerson person)
    {
        var head =
            _households.ResolveHouseholdHead(
                person);

        return head is not null
            && _households
                .GetStatus(
                    head)?
                .IsBroke
                == true;
    }

    private IReadOnlyList<HouseholdContext>
        GetLivingHouseholds(
            IGameState gameState)
    {
        var result =
            new Dictionary<
                Guid,
                HouseholdContextBuilder>();

        foreach (var person in
            gameState.People)
        {
            if (!person.Tags.Has(
                "state.alive"))
            {
                continue;
            }

            var head =
                _households.ResolveHouseholdHead(
                    person);

            if (head is null
                || !_economy.HasHousehold(
                    head))
            {
                continue;
            }

            if (!result.TryGetValue(
                head.Id,
                out var builder))
            {
                builder =
                    new HouseholdContextBuilder(
                        head);

                result[head.Id] =
                    builder;
            }

            if (!builder.Occupants.Any(
                occupant =>
                    occupant.Id
                    == person.Id))
            {
                builder.Occupants.Add(
                    person);
            }
        }

        return result.Values
            .Where(
                builder =>
                    builder.Occupants.Count > 0)
            .Select(
                builder =>
                new HouseholdContext(
                    builder.Head,
                    builder.Occupants,
                    builder.Occupants
                        .FirstOrDefault(
                            person =>
                                person.Age >= 18)
                    ?? builder.Occupants[0]))
            .ToList();
    }

    private IPerson? ResolveFinanceHead(
        IPerson person)
    {
        var head =
            _households.ResolveHouseholdHead(
                person);

        if (head is null
            || !_economy.HasHousehold(
                head))
        {
            return null;
        }

        return head;
    }

    private double ApplyNonFatalDamage(
        IPerson person,
        int minimum,
        int maximum)
    {
        var health =
            _health.GetHealth(
                person);

        var rolled =
            _random.NextInt(
                minimum,
                maximum);

        var target =
            Math.Max(
                1,
                health.Current
                - rolled);

        var actual =
            Math.Max(
                0,
                health.Current
                - target);

        _health.SetHealth(
            person,
            target);

        return actual;
    }

    private decimal RemoveHouseholdWealth(
        IPerson head,
        decimal requestedLoss)
    {
        var finance =
            _economy.GetHousehold(
                head);

        if (finance is null
            || finance.Wealth <= 0)
        {
            return 0;
        }

        var actual =
            Math.Min(
                finance.Wealth,
                Math.Max(
                    0,
                    requestedLoss));

        _economy.ChangeWealth(
            head,
            -actual);

        return actual;
    }

    private decimal RandomMoney(
        int minimum,
        int maximum)
    {
        return _random.NextInt(
            minimum,
            maximum);
    }

    private IReadOnlyList<IPerson> SamplePeople(
        IReadOnlyList<IPerson> people,
        int count)
    {
        var pool =
            people.ToList();

        var selected =
            new List<IPerson>();

        while (pool.Count > 0
            && selected.Count < count)
        {
            var index =
                _random.NextInt(
                    0,
                    pool.Count - 1);

            selected.Add(
                pool[index]);

            pool.RemoveAt(
                index);
        }

        return selected;
    }

    private HouseholdRareEvent? SelectEvent(
        IReadOnlyList<HouseholdEventCandidate> candidates)
    {
        var roll =
            _random.NextDouble();

        var cumulative =
            0.0;

        foreach (var candidate in
            candidates)
        {
            cumulative +=
                candidate.Chance;

            if (roll < cumulative)
            {
                return candidate.Event;
            }
        }

        return null;
    }

    private PersonalRareEvent? SelectEvent(
        IReadOnlyList<PersonalEventCandidate> candidates)
    {
        var roll =
            _random.NextDouble();

        var cumulative =
            0.0;

        foreach (var candidate in
            candidates)
        {
            cumulative +=
                candidate.Chance;

            if (roll < cumulative)
            {
                return candidate.Event;
            }
        }

        return null;
    }

    private void PublishPersonalEvent(
        IGameState gameState,
        IPerson person,
        string eventType,
        string text,
        IReadOnlyDictionary<string, string> data)
    {
        var eventData =
            data.ToDictionary(
                pair =>
                    pair.Key,
                pair =>
                    pair.Value);

        eventData["text"] =
            text;

        _events.Publish(
            new GameEvent
            {
                Type =
                    eventType,

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                Data =
                    eventData
            });
    }

    private void PublishHouseholdEvent(
        IGameState gameState,
        HouseholdContext household,
        string eventType,
        string text,
        IReadOnlyDictionary<string, string> data,
        IPerson? preferredSubject = null)
    {
        var subject =
            preferredSubject
            ?? household.PrimaryOccupant;

        var related =
            household.Occupants
                .Where(
                    person =>
                        person.Id
                        != subject.Id)
                .Select(
                    person =>
                        person.Id)
                .ToList();

        var eventData =
            data.ToDictionary(
                pair =>
                    pair.Key,
                pair =>
                    pair.Value);

        eventData["text"] =
            text;

        _events.Publish(
            new GameEvent
            {
                Type =
                    eventType,

                Year =
                    gameState.Year,

                SubjectId =
                    subject.Id,

                RelatedPersonIds =
                    related,

                Data =
                    eventData
            });
    }

    private string HouseholdDisplayName(
        HouseholdContext household)
    {
        var livingHead =
            household.Head.Tags.Has(
                "state.alive")
                    ? household.Head
                    : household.PrimaryOccupant;

        return _family.GetDisplayName(
            livingHead);
    }

}
