using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
    private void ExecuteSimpleEffects(
        IGameState gameState,
        RareEventDefinition definition,
        IPerson subject,
        IPerson? householdHead,
        HouseholdContext? household)
    {
        decimal wealthGain = 0;
        decimal wealthLoss = 0;
        double healthDamage = 0;
        var fatal = false;

        foreach (var effect in _simpleEffects.GetEffects(definition.EventId))
        {
            if (_random.NextDouble() >= effect.Chance)
                continue;

            switch (effect.EffectType.ToLowerInvariant())
            {
                case "wealthgain":
                {
                    if (householdHead is null) break;
                    var amount = RandomMoney(effect.MinimumValue, effect.MaximumValue);
                    _economy.ChangeWealth(householdHead, amount);
                    wealthGain += amount;
                    break;
                }
                case "wealthloss":
                {
                    if (householdHead is null) break;
                    var amount = RandomMoney(effect.MinimumValue, effect.MaximumValue);
                    wealthLoss += RemoveHouseholdWealth(householdHead, amount);
                    break;
                }
                case "healthdamage":
                {
                    var min = Math.Max(0, (int)Math.Round(effect.MinimumValue, MidpointRounding.AwayFromZero));
                    var max = Math.Max(min, (int)Math.Round(effect.MaximumValue, MidpointRounding.AwayFromZero));
                    healthDamage += ApplyNonFatalDamage(subject, min, max);
                    break;
                }
                case "deathchance":
                {
                    var probability = effect.MinimumValue == effect.MaximumValue
                        ? effect.MinimumValue
                        : effect.MinimumValue + _random.NextDouble() * (effect.MaximumValue - effect.MinimumValue);
                    fatal |= _random.NextDouble() < Math.Clamp(probability, 0, 1);
                    break;
                }
            }
        }

        var displayName = _variants.ResolveName(definition, gameState.Year);
        var data = new Dictionary<string, string>
        {
            ["displayName"] = displayName,
            ["wealthGain"] = wealthGain.ToString("0"),
            ["wealthLoss"] = wealthLoss.ToString("0"),
            ["healthDamage"] = healthDamage.ToString("0"),
            ["fatal"] = fatal.ToString()
        };
        var text = BuildSimpleEffectText(definition, subject, householdHead, wealthGain, wealthLoss, healthDamage);

        if (household is not null)
            PublishHouseholdEvent(gameState, household, definition.EventId, text, data, subject, displayName);
        else
            PublishPersonalEvent(gameState, subject, definition.EventId, text, data, displayName);

        if (fatal && subject.Tags.Has("state.alive"))
            _death.Kill(gameState, subject, definition.EventId);
    }

    private string BuildSimpleEffectText(
        RareEventDefinition definition,
        IPerson subject,
        IPerson? householdHead,
        decimal wealthGain,
        decimal wealthLoss,
        double healthDamage)
    {
        var person = _family.GetDisplayName(subject);
        return definition.EventId.ToLowerInvariant() switch
        {
            "rare.legal_dispute" => $"The household of {person} became involved in a costly legal dispute and lost {wealthLoss:N0} zł.",
            "rare.major_repair" => $"The household of {person} faced major property repairs costing {wealthLoss:N0} zł.",
            "rare.house_discovery" => $"The household of {person} made a valuable discovery at home worth {wealthGain:N0} zł.",
            "rare.distant_inheritance" => $"{person} unexpectedly inherited {wealthGain:N0} zł from a distant relative outside the known family tree.",
            "rare.found_property" => $"{person} found valuable property worth {wealthGain:N0} zł.",
            "rare.water_accident" => $"{person} suffered a serious water accident and lost {healthDamage:0} Health.",
            "rare.animal_accident" => $"{person} was injured in an accident involving an animal and lost {healthDamage:0} Health.",
            "rare.craft_setback" => $"{person} suffered a costly setback while practicing a craft, costing the household {wealthLoss:N0} zł.",
            "rare.patronage" => $"{person} received unexpected patronage worth {wealthGain:N0} zł.",
            "rare.prize_award" => $"{person} received a prize or award worth {wealthGain:N0} zł.",
            _ => wealthGain > 0
                ? $"{person} experienced {definition.Name} and gained {wealthGain:N0} zł."
                : wealthLoss > 0
                    ? $"{person} experienced {definition.Name} and lost {wealthLoss:N0} zł."
                    : healthDamage > 0
                        ? $"{person} experienced {definition.Name} and lost {healthDamage:0} Health."
                        : $"{person} experienced {definition.Name}."
        };
    }
}
