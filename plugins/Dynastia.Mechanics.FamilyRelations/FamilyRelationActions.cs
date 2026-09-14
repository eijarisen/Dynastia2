using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static class FamilyRelationActions
{
    private const decimal MinimumMoneyTransfer = 1000m;
    private const decimal LegacyMoneyGift = 2000m;
    private const double JobConnectionBonus = 0.20;

    public static void Register(
        IActionRegistry actions,
        IGameState gameState,
        IFamilyService family,
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        ILocationService locations,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events)
    {
        actions.Register(CreateImprove(relations, households, economy, events, family, gameState));
        actions.Register(CreateAskMoney(relations, households, economy, random, events, family));
        actions.Register(CreateGiveMoney(relations, households, economy, events, family));
        actions.Register(CreateAskHouse(relations, households, economy, locations, career, random, events, family, gameState));
        actions.Register(CreateGiveHouse(relations, households, economy, locations, career, events, family, gameState));
        actions.Register(CreateAskJobHelp(relations, households, economy, career, random, events, family, gameState));
        actions.Register(CreateGiveJobHelp(relations, households, economy, career, events, family, gameState));
    }

    private static GameActionDefinition CreateImprove(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.improve",
        Label = "Improve Relations",
        Description = "Spend meaningful time together. The primary family relationship improves by 10, with a small positive spillover to other close ties between the two households.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c) && IsValidRelation(c, relations) && IsDifferentHousehold(c.Actor, c.Target, households, economy),
        Execute = c =>
        {
            if (!IsValidRelation(c, relations)) return new(false);
            relations.ModifyRelation(c.Actor, c.Target, 10);
            ApplySpillover(c.Actor, c.Target, relations, households, economy, gameState);
            Publish(events, c, "family_relations.improved", family,
                $"{family.GetDisplayName(c.Actor)} spent time rebuilding family ties with {family.GetDisplayName(c.Target)}.");
            return new(true);
        }
    };

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

    private static GameActionDefinition CreateAskHouse(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        ILocationService locations,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.ask_house",
        Label = "Ask for a House",
        Description = "Ask this autonomous relative for one spare rented property. A gifted house in another town relocates a household that owns no other home.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && economy.GetHouses(c.Actor).Count == 0
            && TryGetAutonomousTargetHead(c.Target, households, out var targetHead)
            && targetHead is not null
            && economy.GetHouses(targetHead).Any(h => h.IsRented),
        Execute = c =>
        {
            if (economy.GetHouses(c.Actor).Count != 0
                || !TryGetAutonomousTargetHead(c.Target, households, out var targetHead)
                || targetHead is null)
                return new(false);
            var spare = economy.GetHouses(targetHead).Where(h => h.IsRented).ToList();
            if (spare.Count == 0) return new(false);

            var ability = Math.Min(1.2, 1.0 + ((spare.Count - 1) * 0.10));
            if (random.NextDouble() >= relations.EvaluateRequestWillingness(c.Actor, c.Target, ability))
            {
                relations.ModifyRelation(c.Actor, c.Target, -5);
                Publish(events, c, "family_relations.house_refused", family,
                    $"{family.GetDisplayName(c.Target)} declined {family.GetDisplayName(c.Actor)}'s request for a house.");
                return new(true);
            }

            var chosen = spare[random.NextInt(0, spare.Count - 1)];
            var house = economy.TakeHouse(targetHead, chosen.Id);
            if (house is null) return new(false);
            economy.AddExistingHouse(c.Actor, house);
            var origin = locations.GetLocation(c.Actor).HomeTown;
            if (!origin.Id.Equals(house.Town.Id, StringComparison.OrdinalIgnoreCase))
                RelocateHousehold(gameState, c.Actor, house.Town, family, economy, career, events);
            relations.ModifyRelation(c.Actor, c.Target, 10);
            Publish(events, c, "family_relations.house_received", family,
                $"{family.GetDisplayName(c.Target)} gave {family.GetDisplayName(c.Actor)} a house in {house.Town.Town}.");
            return new(true);
        }
    };

    private static GameActionDefinition CreateGiveHouse(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        ILocationService locations,
        ICareerService career,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.give_house",
        Label = "Give House",
        Description = "Give one non-residence property to this relative's household. If they own no home and the property is elsewhere, they relocate there.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is not null
            && economy.GetHouses(c.Actor).Any(h => h.IsRented),
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null) return new(false);
            if (!c.Parameters.TryGetValue("propertyId", out var raw) || !Guid.TryParse(raw, out var propertyId))
                return new(false, "No property was selected.");
            var selected = economy.GetHouses(c.Actor).FirstOrDefault(h => h.Id == propertyId && h.IsRented);
            if (selected is null) return new(false, "That property is no longer available.");

            var recipientHadHouse = economy.GetHouses(targetHead).Count > 0;
            var house = economy.TakeHouse(c.Actor, propertyId);
            if (house is null) return new(false);
            economy.AddExistingHouse(targetHead, house);
            var targetTown = locations.GetLocation(targetHead).HomeTown;
            if (!recipientHadHouse && !targetTown.Id.Equals(house.Town.Id, StringComparison.OrdinalIgnoreCase))
                RelocateHousehold(gameState, targetHead, house.Town, family, economy, career, events);
            relations.ModifyRelation(c.Actor, c.Target, 10);
            Publish(events, c, "family_relations.house_given", family,
                $"{family.GetDisplayName(c.Actor)} gave {family.GetDisplayName(c.Target)} a house in {house.Town.Town}.");
            return new(true);
        }
    };

    private static GameActionDefinition CreateAskJobHelp(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.ask_job_help",
        Label = "Ask for Job Help",
        Description = "Ask this autonomous relative to use a useful career connection. Agreement grants a normal local job attempt with a family-connection bonus.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && TryGetAutonomousTargetHead(c.Target, households, out var targetHead)
            && targetHead is not null
            && FindUnemployedAdult(c.Actor, economy, career, gameState) is not null
            && HasUsefulConnection(targetHead, economy, career, gameState),
        Execute = c =>
        {
            if (!TryGetAutonomousTargetHead(c.Target, households, out var targetHead) || targetHead is null)
                return new(false);
            var worker = FindUnemployedAdult(c.Actor, economy, career, gameState);
            if (worker is null || !HasUsefulConnection(targetHead, economy, career, gameState))
                return new(false);

            if (random.NextDouble() >= relations.EvaluateRequestWillingness(c.Actor, c.Target))
            {
                relations.ModifyRelation(c.Actor, c.Target, -5);
                Publish(events, c, "family_relations.job_help_refused", family,
                    $"{family.GetDisplayName(c.Target)} declined to help {family.GetDisplayName(worker)} find work.");
                return new(true);
            }

            var found = career.TryFindEmployment(worker, JobConnectionBonus);
            relations.ModifyRelation(c.Actor, c.Target, found ? 5 : 2);
            Publish(events, c, "family_relations.job_help_received", family,
                found
                    ? $"{family.GetDisplayName(c.Target)} used family connections to help {family.GetDisplayName(worker)} find work."
                    : $"{family.GetDisplayName(c.Target)} tried to help {family.GetDisplayName(worker)} find work, but no suitable position was secured.");
            return new(true);
        }
    };

    private static GameActionDefinition CreateGiveJobHelp(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        ICareerService career,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.give_job_help",
        Label = "Give Job Help",
        Description = "Use this household's career connections to help an unemployed adult relative. No approval roll is needed; the local labour market still decides whether a job is found.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && FindUnemployedAdult(targetHead, economy, career, gameState) is not null
            && HasUsefulConnection(c.Actor, economy, career, gameState),
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null || !HasUsefulConnection(c.Actor, economy, career, gameState))
                return new(false);
            var worker = FindUnemployedAdult(targetHead, economy, career, gameState);
            if (worker is null) return new(false);
            var found = career.TryFindEmployment(worker, JobConnectionBonus);
            relations.ModifyRelation(c.Actor, c.Target, found ? 5 : 2);
            Publish(events, c, "family_relations.job_help_given", family,
                found
                    ? $"{family.GetDisplayName(c.Actor)} used family connections to help {family.GetDisplayName(worker)} find work."
                    : $"{family.GetDisplayName(c.Actor)} tried to help {family.GetDisplayName(worker)} find work, but no suitable position was secured.");
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

    private static bool IsRelationsContext(GameActionContext c) =>
        c.Parameters.TryGetValue("familyRelations", out var value)
        && value.Equals("true", StringComparison.OrdinalIgnoreCase)
        && c.Actor.Tags.Has("state.alive")
        && c.Actor.Tags.Has("control.playable")
        && c.Actor.Age >= 18;

    private static bool IsValidRelation(GameActionContext c, IFamilyRelationService relations) =>
        c.Target.Tags.Has("state.alive") && relations.GetRelation(c.Actor, c.Target) is not null;

    private static bool IsDifferentHousehold(IPerson actor, IPerson target, IHouseholdService households, IEconomyService economy)
    {
        var a = households.ResolveHouseholdHead(actor);
        var b = households.ResolveHouseholdHead(target);
        if (a is null) return false;
        if (b is null) return true;
        return economy.GetHouseholdId(a) != economy.GetHouseholdId(b);
    }

    private static IPerson? ResolveTargetHead(IPerson target, IHouseholdService households) => households.ResolveHouseholdHead(target);

    private static bool TryGetAutonomousTargetHead(IPerson target, IHouseholdService households, out IPerson? head)
    {
        head = households.ResolveHouseholdHead(target);
        return head is not null && households.IsAutonomousHousehold(head) && !head.Tags.Has("control.playable");
    }

    private static void ApplySpillover(
        IPerson actor,
        IPerson target,
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        IGameState gameState)
    {
        var actorHead = households.ResolveHouseholdHead(actor);
        var targetHead = households.ResolveHouseholdHead(target);
        if (actorHead is null || targetHead is null) return;
        var actorIds = economy.GetHouseholdMemberIds(actorHead).ToHashSet();
        var targetIds = economy.GetHouseholdMemberIds(targetHead).ToHashSet();
        foreach (var a in gameState.People.Where(p => actorIds.Contains(p.Id) && p.Tags.Has("state.alive")))
        foreach (var b in gameState.People.Where(p => targetIds.Contains(p.Id) && p.Tags.Has("state.alive")))
        {
            if ((a.Id == actor.Id && b.Id == target.Id) || relations.GetRelation(a, b) is null) continue;
            relations.ModifyRelation(a, b, 2, majorInteraction: false);
        }
    }

    private static IPerson? FindUnemployedAdult(
        IPerson householdRepresentative,
        IEconomyService economy,
        ICareerService career,
        IGameState gameState)
    {
        var ids = economy.GetHouseholdMemberIds(householdRepresentative).ToHashSet();
        return gameState.People
            .Where(p => ids.Contains(p.Id) && p.Tags.Has("state.alive") && p.Age >= 18 && !p.Tags.Has("state.imprisoned"))
            .Where(p => { var c = career.GetCareer(p); return !c.IsRetired && c.JobLevel == 0; })
            .OrderByDescending(p => p.Id == householdRepresentative.Id)
            .ThenBy(p => p.Age)
            .FirstOrDefault();
    }

    private static bool HasUsefulConnection(
        IPerson householdRepresentative,
        IEconomyService economy,
        ICareerService career,
        IGameState gameState)
    {
        var ids = economy.GetHouseholdMemberIds(householdRepresentative).ToHashSet();
        return gameState.People
            .Where(p => ids.Contains(p.Id) && p.Tags.Has("state.alive") && p.Age >= 18)
            .Any(p => { var c = career.GetCareer(p); return !c.IsRetired && c.JobLevel >= 2; });
    }

    private static void RelocateHousehold(
        IGameState gameState,
        IPerson head,
        TownInfo destination,
        IFamilyService family,
        IEconomyService economy,
        ICareerService career,
        IGameEventBus events)
    {
        var origin = economy.GetResidenceTown(head);
        if (origin.Id.Equals(destination.Id, StringComparison.OrdinalIgnoreCase)) return;
        var ids = economy.GetHouseholdMemberIds(head).ToHashSet();
        var employed = gameState.People
            .Where(p => ids.Contains(p.Id) && p.Tags.Has("state.alive") && p.Age >= 18 && !p.Tags.Has("role.nanny") && !p.Tags.Has("role.family_nanny"))
            .Where(p => { var c = career.GetCareer(p); return !c.IsRetired && c.JobLevel > 0; })
            .ToList();

        economy.SetResidenceTown(head, destination);
        foreach (var worker in employed)
            career.RelocateEmployment(worker);

        events.Publish(new GameEvent
        {
            Type = "household.moved",
            Year = gameState.Year,
            SubjectId = head.Id,
            RelatedPersonIds = ids.Where(id => id != head.Id).ToList(),
            Data = new Dictionary<string, string>
            {
                ["fromTown"] = origin.Town,
                ["toTown"] = destination.Town,
                ["text"] = $"The {head.Surname} household moved from {origin.Town} to {destination.Town}."
            }
        });
    }

    private static void Publish(
        IGameEventBus events,
        GameActionContext c,
        string type,
        IFamilyService family,
        string text,
        bool suppressChronicle = true)
    {
        var data =
            new Dictionary<string, string>
            {
                ["text"] = text
            };

        if (suppressChronicle)
        {
            data["suppressChronicle"] = "true";
        }
        else
        {
            data["familyNews"] = "true";
        }

        events.Publish(new GameEvent
        {
            Type = type,
            Year = c.GameState.Year,
            SubjectId = c.Actor.Id,
            RelatedPersonIds = [c.Target.Id],
            Data = data
        });
    }
}
