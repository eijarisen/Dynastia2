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
        Label = "Request Money",
        Description = "Request financial help from this relative's household. The action is offered when their household is at least as wealthy as yours; acceptance depends on Familiarity and Sympathy.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && !targetHead.Tags.Has("control.playable")
            && economy.GetHousehold(targetHead) is { } targetFinance
            && economy.GetHousehold(c.Actor) is { } actorFinance
            && actorFinance.Wealth <= targetFinance.Wealth
            && CanAffordMoneySelection(c, targetFinance.Wealth),
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null
                || targetHead.Tags.Has("control.playable"))
            {
                return new(false);
            }

            var targetFinance = economy.GetHousehold(targetHead);
            var amount = ResolveMoneyAmount(c);

            if (!IsValidMoneyAmount(amount)
                || targetFinance is null
                || targetFinance.Wealth < amount)
            {
                return new(false, "That household can no longer afford the selected amount.");
            }

            var accepted =
                random.NextDouble()
                < relations.EvaluateRequestWillingness(c.Actor, c.Target);

            if (!accepted)
            {
                relations.RecordInteraction(c.Actor, c.Target, 2, -8);
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
            relations.RecordInteraction(c.Actor, c.Target, 5, 5);
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
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family) => new()
    {
        Id = "family_relations.give_money",
        Label = "Send Money",
        Description = "Offer money to this relative's household in full-thousand increments. The action is offered while your household is at least as wealthy as theirs. Only very hostile relatives are likely to refuse a gift.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && economy.GetHousehold(targetHead) is { } targetFinance
            && economy.GetHousehold(c.Actor) is { } actorFinance
            && actorFinance.Wealth >= targetFinance.Wealth
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

            if (random.NextDouble() >= relations.EvaluateOfferWillingness(c.Actor, c.Target))
            {
                relations.RecordInteraction(c.Actor, c.Target, 1, -2);
                Publish(
                    events,
                    c,
                    "family_relations.money_gift_refused",
                    family,
                    $"{family.GetDisplayName(c.Target)} refused {family.GetDisplayName(c.Actor)}'s offer of {amount:N0} zł.",
                    suppressChronicle: false);
                return new(true);
            }

            economy.ChangeWealth(c.Actor, -amount);
            economy.ChangeWealth(targetHead, amount);
            relations.RecordInteraction(c.Actor, c.Target, 4, 5);
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
