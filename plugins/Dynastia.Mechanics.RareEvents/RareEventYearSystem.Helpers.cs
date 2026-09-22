using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
    private static bool HasCondition(HealthSnapshot health, string name) =>
        health.Conditions.Any(condition =>
            condition.Id.Equals(name, StringComparison.OrdinalIgnoreCase)
            || condition.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private bool IsHouseholdBroke(IPerson person)
    {
        var head = _households.ResolveHouseholdHead(person);
        return head is not null && _households.GetStatus(head)?.HasUnfundedBasicNeeds == true;
    }

    private bool IsEligible(
        RareEventDefinition definition,
        IGameState gameState,
        IPerson subject,
        IPerson? financeHead)
    {
        if (!subject.Tags.Has("state.alive") || SimulationState.IsInactive(subject)
            || !definition.IsAvailable(gameState.Year, subject.Age))
            return false;

        var location = _localOpportunities.GetOpportunitySnapshot(subject);
        if (location.Town.SettlementClass < definition.MinimumSettlementClass)
            return false;

        var opportunityTags = GetOpportunityTags(location);
        if (!definition.RequiredOpportunityTags.All(opportunityTags.Contains))
            return false;

        var finance = financeHead is null ? null : _economy.GetHousehold(financeHead);
        if (definition.RequiresFinanceHousehold && finance is null)
            return false;
        if (definition.MinimumHouseholdWealth > 0 && (finance is null || finance.Wealth < definition.MinimumHouseholdWealth))
            return false;
        if (definition.RequiresOwnedHouse && (finance is null || finance.Houses.Count == 0))
            return false;
        if (definition.RequiresEmployment && !_career.IsEmployed(subject))
            return false;

        if (definition.RequiresFarmland)
        {
            var farming = _farmingResolver();
            if (farming is null || financeHead is null)
                return false;
            var snapshot = farming.GetSnapshot(financeHead);
            if (snapshot.TotalParcelCount == 0 || snapshot.LocalParcelCount == 0)
                return false;
        }

        if (definition.RequiresCraft)
        {
            var crafts = _craftResolver();
            if (crafts is null || crafts.GetKnownCrafts(subject).Count == 0)
                return false;
        }

        if (definition.MinimumStress > 0 && _stress.GetStress(subject).Total < definition.MinimumStress)
            return false;

        if (definition.EventId.Equals("rare.scholarship", StringComparison.OrdinalIgnoreCase)
            && _education.GetEducationLevel(subject) >= 5)
            return false;

        return true;
    }

    private double GetSelectionWeight(
        RareEventDefinition definition,
        IGameState gameState,
        IPerson subject)
    {
        var location = _localOpportunities.GetOpportunitySnapshot(subject);
        var personality = _personality.GetPersonality(subject);
        var context = new ContextWeightContext(
            gameState.Year,
            subject.Age,
            _family.GetSex(subject),
            personality?.Temperament,
            personality?.Morals,
            location.Town.SettlementClass);

        var weight = definition.BaseWeight
            * _contextWeights.GetMultiplier(definition.EventId, context)
            * RareEventRules.GetTownPreferenceMultiplier(definition.TownPreference, location.Town.SettlementClass)
            * RareEventRules.GetPreferredOpportunityMultiplier(definition.PreferredOpportunityTags, GetOpportunityTags(location));

        if (definition.StatId is not null)
        {
            var stat = _stats.GetStats(subject)
                .FirstOrDefault(item => item.Id.Equals(definition.StatId, StringComparison.OrdinalIgnoreCase))?.Value ?? 3;
            weight *= RareEventRules.GetStatMultiplier(definition.StatDirection, stat);
        }

        var family = _career.GetCareerFamily(subject);
        weight *= _careerFamilyWeights.GetMultiplier(definition.EventId, family);
        weight *= RareEventRules.GetPreferredCareerFamilyMultiplier(definition.PreferredCareerFamilies, family);
        return Math.Max(0, weight);
    }

    private static IReadOnlySet<string> GetOpportunityTags(LocationOpportunitySnapshot location) =>
        location.RegionOpportunityTags.Concat(location.TownOpportunityTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private EventCandidate? SelectWeighted(IReadOnlyList<EventCandidate> candidates)
    {
        if (candidates.Count == 0) return null;
        var total = candidates.Sum(candidate => candidate.Weight);
        if (total <= 0) return null;
        var target = _random.NextDouble() * total;
        var cumulative = 0.0;
        foreach (var candidate in candidates)
        {
            cumulative += candidate.Weight;
            if (target <= cumulative) return candidate;
        }
        return candidates[^1];
    }

    private IReadOnlyList<HouseholdContext> GetLivingHouseholds(IGameState gameState)
    {
        var result = new Dictionary<Guid, HouseholdContextBuilder>();
        foreach (var person in gameState.People)
        {
            if (!person.Tags.Has("state.alive") || SimulationState.IsInactive(person)) continue;
            var head = _households.ResolveHouseholdHead(person);
            if (head is null || !_economy.HasHousehold(head)) continue;
            if (!result.TryGetValue(head.Id, out var builder))
            {
                builder = new HouseholdContextBuilder(head);
                result[head.Id] = builder;
            }
            if (!builder.Occupants.Any(occupant => occupant.Id == person.Id)) builder.Occupants.Add(person);
        }

        return result.Values.Where(builder => builder.Occupants.Count > 0)
            .Select(builder => new HouseholdContext(
                builder.Head,
                builder.Occupants,
                builder.Occupants.FirstOrDefault(person => person.Age >= 18) ?? builder.Occupants[0]))
            .ToList();
    }

    private IPerson? ResolveFinanceHead(IPerson person)
    {
        var head = _households.ResolveHouseholdHead(person);
        return head is not null && _economy.HasHousehold(head) ? head : null;
    }

    private double ApplyNonFatalDamage(IPerson person, int minimum, int maximum)
    {
        var health = _health.GetHealth(person);
        var rolled = _random.NextInt(minimum, maximum);
        var target = Math.Max(1, health.Current - rolled);
        var actual = Math.Max(0, health.Current - target);
        _health.SetHealth(person, target);
        return actual;
    }

    private decimal RemoveHouseholdWealth(IPerson head, decimal requestedLoss)
    {
        var finance = _economy.GetHousehold(head);
        if (finance is null || finance.Wealth <= 0) return 0;
        var actual = Math.Min(finance.Wealth, Math.Max(0, requestedLoss));
        _economy.ChangeWealth(head, -actual);
        return actual;
    }

    private decimal RandomMoney(int minimum, int maximum) => _random.NextInt(minimum, maximum);

    private decimal RandomMoney(double minimum, double maximum)
    {
        var value = minimum + _random.NextDouble() * (maximum - minimum);
        return Math.Round((decimal)value, 0, MidpointRounding.AwayFromZero);
    }

    private IReadOnlyList<IPerson> SamplePeople(IReadOnlyList<IPerson> people, int count)
    {
        var pool = people.ToList();
        var selected = new List<IPerson>();
        while (pool.Count > 0 && selected.Count < count)
        {
            var index = _random.NextInt(0, pool.Count - 1);
            selected.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return selected;
    }

    private void PublishPersonalEvent(
        IGameState gameState,
        IPerson person,
        string eventType,
        string text,
        IReadOnlyDictionary<string, string> data,
        string? displayName = null)
    {
        var eventData = data.ToDictionary(pair => pair.Key, pair => pair.Value);
        eventData["text"] = text;
        if (!string.IsNullOrWhiteSpace(displayName)) eventData["displayName"] = displayName;
        _events.Publish(new GameEvent { Type = eventType, Year = gameState.Year, SubjectId = person.Id, Data = eventData });
    }

    private void PublishHouseholdEvent(
        IGameState gameState,
        HouseholdContext household,
        string eventType,
        string text,
        IReadOnlyDictionary<string, string> data,
        IPerson? preferredSubject = null,
        string? displayName = null)
    {
        var subject = preferredSubject ?? household.PrimaryOccupant;
        var related = household.Occupants.Where(person => person.Id != subject.Id).Select(person => person.Id).ToList();
        var eventData = data.ToDictionary(pair => pair.Key, pair => pair.Value);
        eventData["text"] = text;
        if (!string.IsNullOrWhiteSpace(displayName)) eventData["displayName"] = displayName;
        _events.Publish(new GameEvent
        {
            Type = eventType,
            Year = gameState.Year,
            SubjectId = subject.Id,
            RelatedPersonIds = related,
            Data = eventData
        });
    }

    private string HouseholdDisplayName(HouseholdContext household)
    {
        var livingHead = household.Head.Tags.Has("state.alive") ? household.Head : household.PrimaryOccupant;
        return _family.GetDisplayName(livingHead);
    }
}
