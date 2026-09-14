using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static partial class FamilyRelationActions
{
    private static GameActionDefinition CreateAskMoney(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family) => new()
    {
        Id = "family_relations.ask_money",
        Label = "Ask for Money",
        Description = "Ask this autonomous relative's household for financial help. Choose the amount in full thousands; relationship and the burden on their household determine whether they agree.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && TryGetAutonomousTargetHead(c.Target, households, out var targetHead)
            && targetHead is not null
            && economy.GetHousehold(targetHead) is { } targetFinance
            && CanAffordMoneySelection(c, targetFinance.Wealth),
        Execute = c =>
        {
            if (!TryGetAutonomousTargetHead(c.Target, households, out var targetHead) || targetHead is null)
                return new(false);

            var targetFinance = economy.GetHousehold(targetHead);
            var amount = ResolveMoneyAmount(c);

            if (!IsValidMoneyAmount(amount)
                || targetFinance is null
                || targetFinance.Wealth < amount)
            {
                return new(false, "That household can no longer afford the selected amount.");
            }

            var baseAbility = targetFinance.Wealth switch
            {
                < 5000m => 0.75,
                < 10000m => 0.90,
                < 20000m => 1.00,
                _ => 1.10
            };

            var burdenFactor =
                Math.Clamp(
                    (double)(targetFinance.Wealth / amount) / 3.0,
                    0.35,
                    1.15);

            var accepted =
                random.NextDouble()
                < relations.EvaluateRequestWillingness(
                    c.Actor,
                    c.Target,
                    baseAbility * burdenFactor);

            if (!accepted)
            {
                relations.ModifyRelation(c.Actor, c.Target, -5);
                Publish(
                    events,
                    c,
                    "family_relations.money_refused",
                    family,
                    $"{family.GetDisplayName(c.Target)} declined {family.GetDisplayName(c.Actor)}'s request for {amount:N0} zł in family support.",
                    suppressChronicle: false);
                return new(true);
            }

            economy.ChangeWealth(targetHead, -amount);
            economy.ChangeWealth(c.Actor, amount);
            relations.ModifyRelation(c.Actor, c.Target, 5);
            Publish(
                events,
                c,
                "family_relations.money_received",
                family,
                $"{family.GetDisplayName(c.Target)} gave {family.GetDisplayName(c.Actor)} {amount:N0} zł in family support.",
                suppressChronicle: false);
            return new(true);
        }
    };

    private static GameActionDefinition CreateGiveMoney(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        IGameEventBus events,
        IFamilyService family) => new()
    {
        Id = "family_relations.give_money",
        Label = "Give Money",
        Description = "Give money to this relative's household in full-thousand increments. No approval roll is needed and the gift improves the relationship.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is not null
            && economy.GetHousehold(c.Actor) is { } actorFinance
            && CanAffordMoneySelection(c, actorFinance.Wealth),
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            var amount = ResolveMoneyAmount(c);

            if (targetHead is null
                || !IsValidMoneyAmount(amount)
                || economy.GetHousehold(c.Actor)?.Wealth < amount)
            {
                return new(false, "The selected gift can no longer be afforded.");
            }

            economy.ChangeWealth(c.Actor, -amount);
            economy.ChangeWealth(targetHead, amount);
            relations.ModifyRelation(c.Actor, c.Target, 5);
            Publish(
                events,
                c,
                "family_relations.money_given",
                family,
                $"{family.GetDisplayName(c.Actor)} gave {family.GetDisplayName(c.Target)} {amount:N0} zł.",
                suppressChronicle: false);
            return new(true);
        }
    };

    private static bool CanAffordMoneySelection(
        GameActionContext context,
        decimal availableWealth)
    {
        if (!context.Parameters.TryGetValue(
                "amount",
                out var rawAmount))
        {
            return availableWealth >= MinimumMoneyTransfer;
        }

        return decimal.TryParse(
                rawAmount,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount)
            && IsValidMoneyAmount(amount)
            && availableWealth >= amount;
    }

    private static decimal ResolveMoneyAmount(
        GameActionContext context)
    {
        if (context.Parameters.TryGetValue(
                "amount",
                out var rawAmount)
            && decimal.TryParse(
                rawAmount,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount))
        {
            return amount;
        }

        // Compatibility for a save made while family money support used
        // the original fixed 2,000 zł transfer and stored no amount.
        return LegacyMoneyGift;
    }

    private static bool IsValidMoneyAmount(
        decimal amount) =>
        amount >= MinimumMoneyTransfer
        && amount % 1000m == 0m;

}
