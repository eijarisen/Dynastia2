using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static partial class FamilyRelationActions
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
