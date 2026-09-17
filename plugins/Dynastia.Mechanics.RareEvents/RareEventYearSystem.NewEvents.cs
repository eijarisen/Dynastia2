using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
    private void ExecuteScholarship(
        IGameState gameState,
        IPerson person,
        RareEventDefinition definition)
    {
        var before = _education.GetEducationLevel(person);
        if (before >= 5) return;
        _education.IncreaseEducation(person);
        var after = _education.GetEducationLevel(person);
        var displayName = _variants.ResolveName(definition, gameState.Year);
        PublishPersonalEvent(
            gameState,
            person,
            definition.EventId,
            $"{_family.GetDisplayName(person)} received a {displayName.ToLowerInvariant()} and advanced from Education {before} to {after} without tuition cost.",
            new Dictionary<string, string>
            {
                ["previousEducation"] = before.ToString(),
                ["newEducation"] = after.ToString()
            },
            displayName);
    }

    private void ExecuteProfessionalRecognition(
        IGameState gameState,
        IPerson person,
        RareEventDefinition definition)
    {
        var career = _career.GetCareer(person);
        var before = career.JobSatisfaction;
        _career.ChangeJobSatisfaction(person, 1);
        var after = _career.GetCareer(person).JobSatisfaction;
        var displayName = _variants.ResolveName(definition, gameState.Year);
        PublishPersonalEvent(
            gameState,
            person,
            definition.EventId,
            $"{_family.GetDisplayName(person)} received {displayName.ToLowerInvariant()} for their work, improving Job Satisfaction from {before} to {after}.",
            new Dictionary<string, string>
            {
                ["previousJobSatisfaction"] = before.ToString(),
                ["newJobSatisfaction"] = after.ToString(),
                ["promotionForced"] = "False"
            },
            displayName);
    }

    private void ExecuteExceptionalHarvest(
        IGameState gameState,
        HouseholdContext household,
        RareEventDefinition definition)
    {
        var farming = _farmingResolver();
        if (farming is null) return;
        var snapshot = farming.GetSnapshot(household.Head);
        if (snapshot.LocalParcelCount <= 0) return;

        var expected = Math.Max(0m, snapshot.ExpectedAnnualIncome);
        var reference = expected > 0m ? expected : snapshot.LocalParcelCount * 250m;
        var bonus = Math.Round(reference * (decimal)(0.75 + _random.NextDouble() * 0.75), 0, MidpointRounding.AwayFromZero);
        _economy.ChangeWealth(household.Head, bonus);
        var displayName = _variants.ResolveName(definition, gameState.Year);
        PublishHouseholdEvent(
            gameState,
            household,
            definition.EventId,
            $"The household of {HouseholdDisplayName(household)} enjoyed an exceptional harvest, earning an additional {bonus:N0} zł from {snapshot.LocalParcelCount} local farmland parcel{(snapshot.LocalParcelCount == 1 ? "" : "s")}.",
            new Dictionary<string, string>
            {
                ["amount"] = bonus.ToString("0"),
                ["localParcels"] = snapshot.LocalParcelCount.ToString(),
                ["expectedFarmingIncome"] = expected.ToString("0")
            },
            displayName: displayName);
    }

    private void ExecuteCropFailure(
        IGameState gameState,
        HouseholdContext household,
        RareEventDefinition definition)
    {
        var farming = _farmingResolver();
        if (farming is null) return;
        var snapshot = farming.GetSnapshot(household.Head);
        if (snapshot.LocalParcelCount <= 0) return;

        var expected = Math.Max(0m, snapshot.ExpectedAnnualIncome);
        var reference = expected > 0m ? expected : snapshot.LocalParcelCount * 250m;
        var requested = Math.Round(reference * (decimal)(0.50 + _random.NextDouble() * 0.50), 0, MidpointRounding.AwayFromZero);
        var loss = RemoveHouseholdWealth(household.Head, requested);
        var displayName = _variants.ResolveName(definition, gameState.Year);
        PublishHouseholdEvent(
            gameState,
            household,
            definition.EventId,
            $"The household of {HouseholdDisplayName(household)} suffered a crop failure, losing {loss:N0} zł against the year's farming proceeds.",
            new Dictionary<string, string>
            {
                ["amount"] = loss.ToString("0"),
                ["localParcels"] = snapshot.LocalParcelCount.ToString(),
                ["expectedFarmingIncome"] = expected.ToString("0")
            },
            displayName: displayName);
    }

    private void ExecuteLocalEpidemic(
        IGameState gameState,
        HouseholdContext household,
        RareEventDefinition definition)
    {
        var available = _epidemics.Entries.Where(entry => entry.IsAvailable(gameState.Year)).ToList();
        if (available.Count == 0) return;
        var total = available.Sum(entry => entry.BaseWeight);
        var target = _random.NextDouble() * total;
        var cumulative = 0.0;
        var condition = available[^1];
        foreach (var entry in available)
        {
            cumulative += entry.BaseWeight;
            if (target <= cumulative) { condition = entry; break; }
        }

        var susceptible = household.Occupants
            .Where(person => person.Tags.Has("state.alive") && !_health.HasCondition(person, condition.ConditionId))
            .ToList();
        if (susceptible.Count == 0) return;

        var maximum = Math.Min(condition.MaximumAffected, susceptible.Count);
        var minimum = Math.Min(condition.MinimumAffected, maximum);
        var count = minimum == maximum ? minimum : _random.NextInt(minimum, maximum);
        var selected = SamplePeople(susceptible, count);
        var affected = new List<IPerson>();
        foreach (var person in selected)
            if (_health.AddCondition(person, condition.ConditionId, gameState.Year)) affected.Add(person);
        if (affected.Count == 0) return;

        var displayName = _variants.ResolveName(definition, gameState.Year);
        var names = string.Join(", ", affected.Select(_family.GetDisplayName));
        PublishHouseholdEvent(
            gameState,
            household,
            definition.EventId,
            $"A {displayName.ToLowerInvariant()} affected the household of {HouseholdDisplayName(household)}: {names} contracted {condition.ConditionId.Replace('_', ' ')}.",
            new Dictionary<string, string>
            {
                ["conditionId"] = condition.ConditionId,
                ["affectedCount"] = affected.Count.ToString(),
                ["affectedIds"] = string.Join(";", affected.Select(person => person.Id))
            },
            preferredSubject: affected[0],
            displayName: displayName);
    }

    private void ExecuteCraftCommission(
        IGameState gameState,
        IPerson person,
        IPerson householdHead,
        RareEventDefinition definition)
    {
        var crafts = _craftResolver();
        if (crafts is null) return;
        var known = crafts.GetKnownCrafts(person);
        if (known.Count == 0) return;
        var snapshot = crafts.GetSnapshot(person);
        var craft = known
            .OrderByDescending(item => snapshot.WorkYearsByCraft.TryGetValue(item.Id, out var years) ? years : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .First();
        snapshot.WorkYearsByCraft.TryGetValue(craft.Id, out var workYears);
        var baseIncome = string.IsNullOrWhiteSpace(craft.PrimaryCareerId)
            ? Math.Max(500m, crafts.GetExpectedAnnualIncome(person))
            : _career.GetLevelOneSalary(craft.PrimaryCareerId);
        var proficiency = 1m + Math.Min(10, Math.Max(0, workYears)) * 0.10m;
        var reward = Math.Round(baseIncome * proficiency * (decimal)(0.50 + _random.NextDouble() * 0.75), 0, MidpointRounding.AwayFromZero);
        _economy.ChangeWealth(householdHead, reward);
        var displayName = _variants.ResolveName(definition, gameState.Year);
        PublishPersonalEvent(
            gameState,
            person,
            definition.EventId,
            $"{_family.GetDisplayName(person)} secured a {displayName.ToLowerInvariant()} in {craft.Name}, earning {reward:N0} zł.",
            new Dictionary<string, string>
            {
                ["craftId"] = craft.Id,
                ["craftName"] = craft.Name,
                ["workYears"] = workYears.ToString(),
                ["amount"] = reward.ToString("0")
            },
            displayName);
    }
}
