using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static partial class FamilyRelationActions
{
    private const decimal MinimumMoneyTransfer = 1000m;
    private const decimal LegacyMoneyGift = 2000m;

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
        actions.Register(CreateImprove(relations, households, economy, random, events, family, gameState));
        actions.Register(CreateAskMoney(relations, households, economy, random, events, family));
        actions.Register(CreateGiveMoney(relations, households, economy, random, events, family));
        actions.Register(CreateAskHouse(relations, households, economy, locations, career, random, events, family, gameState));
        actions.Register(CreateGiveHouse(relations, households, economy, locations, career, random, events, family, gameState));
        actions.Register(CreateAskFarmland(relations, households, economy, random, events, family));
        actions.Register(CreateGiveFarmland(relations, households, economy, events, family));
        actions.Register(CreateAskJobHelp(relations, households, economy, career, random, events, family, gameState));
        actions.Register(CreateGiveJobHelp(relations, households, economy, career, random, events, family, gameState));
    }

    private static GameActionDefinition CreateImprove(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.improve",
        Label = "Improve Relations",
        Description = "Spend meaningful time together. Successful contact increases Familiarity and Sympathy, with a small positive spillover to other family ties between the two households. Very hostile relatives may refuse.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && relations.GetRelation(c.Actor, c.Target) is { Score: < 100 }
            && IsDifferentHousehold(c.Actor, c.Target, households, economy),
        Execute = c =>
        {
            if (!IsValidRelation(c, relations)) return new(false);
            if (random.NextDouble() >= relations.EvaluateOfferWillingness(c.Actor, c.Target))
            {
                relations.RecordInteraction(c.Actor, c.Target, 1, -3);
                Publish(events, c, "family_relations.improve_refused", family,
                    $"{family.GetDisplayName(c.Target)} did not want to spend time rebuilding family ties with {family.GetDisplayName(c.Actor)}.");
                return new(true);
            }

            relations.RecordInteraction(c.Actor, c.Target, 8, 7);
            ApplySpillover(c.Actor, c.Target, relations, households, economy, gameState);
            Publish(events, c, "family_relations.improved", family,
                $"{family.GetDisplayName(c.Actor)} spent time rebuilding family ties with {family.GetDisplayName(c.Target)}.");
            return new(true);
        }
    };

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
            relations.RecordInteraction(a, b, 1, 1, majorInteraction: false);
        }
    }

    private static void Publish(
        IGameEventBus events,
        GameActionContext c,
        string type,
        IFamilyService family,
        string text,
        bool suppressChronicle = false)
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
