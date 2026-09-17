using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
    private void ProcessHouseholdEvents(IGameState gameState)
    {
        var gate = _poolRules.GetGateChance("Household");
        foreach (var household in GetLivingHouseholds(gameState))
        {
            if (_random.NextDouble() >= gate)
                continue;

            var selectionSubject = household.Head.Tags.Has("state.alive")
                ? household.Head
                : household.PrimaryOccupant;

            var candidates = _catalog.GetPool("Household")
                .Where(definition => IsEligible(definition, gameState, selectionSubject, household.Head))
                .Select(definition => new EventCandidate(
                    definition,
                    GetSelectionWeight(definition, gameState, selectionSubject)))
                .Where(candidate => candidate.Weight > 0)
                .ToList();

            var selected = SelectWeighted(candidates);
            if (selected is not null)
                ExecuteHouseholdEvent(gameState, household, selected.Definition);
        }
    }

    private void ProcessPersonalEvent(IGameState gameState, IPerson person)
    {
        if (_random.NextDouble() >= _poolRules.GetGateChance("Personal"))
            return;

        var financeHead = ResolveFinanceHead(person);
        var candidates = _catalog.GetPool("Personal")
            .Where(definition => IsEligible(definition, gameState, person, financeHead))
            .Select(definition => new EventCandidate(
                definition,
                GetSelectionWeight(definition, gameState, person)))
            .Where(candidate => candidate.Weight > 0)
            .ToList();

        var selected = SelectWeighted(candidates);
        if (selected is not null)
            ExecutePersonalEvent(gameState, person, financeHead, selected.Definition);
    }

    private void ProcessSpecialEvents(IGameState gameState, IPerson person)
    {
        foreach (var definition in _catalog.GetPool("Special"))
        {
            if (!definition.HandlerId.Equals("special.suicide", StringComparison.OrdinalIgnoreCase)
                || !IsEligible(definition, gameState, person, ResolveFinanceHead(person)))
            {
                continue;
            }

            var stress = _stress.GetStress(person).Total;
            var health = _health.GetHealth(person);
            var personality = _personality.GetPersonality(person);
            var chance = RareEventRules.GetSuicideChance(
                stress,
                HasCondition(health, "depression"),
                HasCondition(health, "anxiety"),
                HasCondition(health, "alcoholism"),
                HasCondition(health, "drug_dependence"),
                _recent.HasFlag(person, "recent.bereavement"),
                _recent.HasFlag(person, "recent.divorce"),
                _recent.HasFlag(person, "recent.job_loss") && IsHouseholdBroke(person),
                health.Percentage < 25,
                personality?.Temperament);

            if (chance > 0 && _random.NextDouble() < chance)
            {
                ExecuteSuicide(gameState, person);
                return;
            }
        }
    }

    private void ExecuteHouseholdEvent(
        IGameState gameState,
        HouseholdContext household,
        RareEventDefinition definition)
    {
        switch (definition.HandlerId.ToLowerInvariant())
        {
            case "bespoke.house_fire":
                ExecuteHouseFire(gameState, household);
                return;
            case "bespoke.burglary":
                ExecuteBurglary(gameState, household);
                return;
            case "bespoke.storm_flood":
                ExecuteStormOrFlood(gameState, household);
                return;
            case "bespoke.structural_accident":
                ExecuteStructuralAccident(gameState, household);
                return;
            case "bespoke.exceptional_harvest":
                ExecuteExceptionalHarvest(gameState, household, definition);
                return;
            case "bespoke.crop_failure":
                ExecuteCropFailure(gameState, household, definition);
                return;
            case "bespoke.local_epidemic":
                ExecuteLocalEpidemic(gameState, household, definition);
                return;
            default:
                if (definition.HandlerId.StartsWith("generic.", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteSimpleEffects(gameState, definition, household.PrimaryOccupant, household.Head, household);
                    return;
                }
                throw new InvalidOperationException($"Unsupported household rare-event handler '{definition.HandlerId}'.");
        }
    }

    private void ExecutePersonalEvent(
        IGameState gameState,
        IPerson person,
        IPerson? financeHead,
        RareEventDefinition definition)
    {
        switch (definition.HandlerId.ToLowerInvariant())
        {
            case "bespoke.assault": ExecuteAssault(gameState, person); return;
            case "bespoke.mugging": ExecuteMugging(gameState, person, financeHead!); return;
            case "bespoke.workplace_accident": ExecuteWorkplaceAccident(gameState, person, definition); return;
            case "bespoke.traffic_accident": ExecuteTrafficAccident(gameState, person, definition); return;
            case "bespoke.lightning_strike": ExecuteLightningStrike(gameState, person); return;
            case "bespoke.serious_fall": ExecuteSeriousFall(gameState, person); return;
            case "bespoke.lottery_win": ExecuteLotteryWin(gameState, person, financeHead!); return;
            case "bespoke.fraud": ExecuteFraud(gameState, person, financeHead!); return;
            case "bespoke.wrongful_arrest": ExecuteWrongfulArrest(gameState, person); return;
            case "bespoke.scholarship": ExecuteScholarship(gameState, person, definition); return;
            case "bespoke.professional_recognition": ExecuteProfessionalRecognition(gameState, person, definition); return;
            case "bespoke.craft_commission": ExecuteCraftCommission(gameState, person, financeHead!, definition); return;
            default:
                if (definition.HandlerId.StartsWith("generic.", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteSimpleEffects(gameState, definition, person, financeHead, null);
                    return;
                }
                throw new InvalidOperationException($"Unsupported personal rare-event handler '{definition.HandlerId}'.");
        }
    }
}
