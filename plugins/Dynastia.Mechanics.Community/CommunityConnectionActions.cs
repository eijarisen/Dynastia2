using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal static class CommunityConnectionActions
{
    private const string ContextParameter = "connectionRelations";
    private const string ConnectionIdParameter = "connectionId";

    public static void Register(
        IActionRegistry actions,
        CommunityConnectionService connections,
        IEconomyService economy,
        IFamilyService family)
    {
        actions.Register(CreateImprove(connections, family));
        actions.Register(CreateSendMoney(connections, economy, family));
        actions.Register(CreateGiveHouse(connections, economy, family));
        actions.Register(CreateGiveFarmland(connections, economy, family));
        actions.Register(CreateRequestMoney(connections, family));
        actions.Register(CreateRequestHouse(connections, family));
        actions.Register(CreateRequestFarmland(connections, family));
    }

    private static GameActionDefinition CreateImprove(
        CommunityConnectionService connections,
        IFamilyService family) => new()
    {
        Id = "community.connection.improve",
        Label = "Improve Relations",
        Description = "Spend time maintaining this acquaintance. Connections improve more slowly than close family ties.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        EvaluateAvailability = context => EvaluateBase(context, connections),
        Execute = context =>
        {
            var connection = Resolve(context, connections);
            if (connection is null)
                return new(false, "This connection is no longer active.", ActionReasonCodes.NoLongerEligible);
            connections.ImproveRelations(connection);
            Publish(context, connection, family,
                $"{family.GetDisplayName(context.Actor)} spent time strengthening the household's acquaintance with {connection.Name}.");
            return new(true);
        }
    };

    private static GameActionDefinition CreateSendMoney(
        CommunityConnectionService connections,
        IEconomyService economy,
        IFamilyService family) => new()
    {
        Id = "community.connection.send_money",
        Label = "Send Money",
        Description = "Send financial help in full-thousand increments. A meaningful gift improves the connection and may improve their estimated wealth.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        EvaluateAvailability = context =>
        {
            var baseResult = EvaluateBase(context, connections);
            if (!baseResult.Available)
                return baseResult;
            if (!TryAmount(context, out var amount))
                return connections.GetSendMoneyMaximum(context.Actor) >= 1000m
                    ? ActionEvaluationResult.Allowed()
                    : ActionEvaluationResult.Denied(ActionReasonCodes.InsufficientFunds, "At least 1,000 zł is required.");
            return connections.IsValidMoneyAmount(amount) && economy.CanAfford(context.Actor, amount)
                ? ActionEvaluationResult.Allowed()
                : ActionEvaluationResult.Denied(ActionReasonCodes.InsufficientFunds, "The selected gift can no longer be afforded.");
        },
        Execute = context =>
        {
            var connection = Resolve(context, connections);
            if (connection is null || !TryAmount(context, out var amount)
                || !connections.SendMoney(context.Actor, connection, amount))
            {
                return new(false, "The selected gift can no longer be made.", ActionReasonCodes.ResourceUnavailable);
            }
            Publish(context, connection, family,
                $"{family.GetDisplayName(context.Actor)} sent {connection.Name} {amount:N0} zł.");
            return new(true);
        }
    };

    private static GameActionDefinition CreateGiveHouse(
        CommunityConnectionService connections,
        IEconomyService economy,
        IFamilyService family) => new()
    {
        Id = "community.connection.give_house",
        Label = "Give House",
        Description = "Give one non-residence house to this connection. The real property leaves the household and becomes an abstract asset of the acquaintance.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        EvaluateAvailability = context =>
        {
            var baseResult = EvaluateBase(context, connections);
            if (!baseResult.Available)
                return baseResult;
            var allHouses = economy.GetHouses(context.Actor);
            var giftable = allHouses.Where(house => !house.IsResidence).ToArray();
            if (!TryPropertyId(context, out var propertyId))
                return allHouses.Count >= 2 && giftable.Length > 0
                    ? ActionEvaluationResult.Allowed()
                    : ActionEvaluationResult.Denied(ActionReasonCodes.ResourceUnavailable, "No spare house is available.");
            return giftable.Any(house => house.Id == propertyId)
                ? ActionEvaluationResult.Allowed()
                : ActionEvaluationResult.Denied(ActionReasonCodes.ResourceUnavailable, "That house is no longer available.");
        },
        Execute = context =>
        {
            var connection = Resolve(context, connections);
            if (connection is null || !TryPropertyId(context, out var propertyId)
                || !connections.GiveHouse(context.Actor, connection, propertyId))
            {
                return new(false, "The selected house can no longer be given.", ActionReasonCodes.ResourceUnavailable);
            }
            Publish(context, connection, family,
                $"{family.GetDisplayName(context.Actor)} gave a house to {connection.Name}.");
            return new(true);
        }
    };

    private static GameActionDefinition CreateGiveFarmland(
        CommunityConnectionService connections,
        IEconomyService economy,
        IFamilyService family) => new()
    {
        Id = "community.connection.give_farmland",
        Label = "Give Farmland",
        Description = "Give one real farmland parcel to this connection. The parcel leaves the household and becomes an abstract asset of the acquaintance.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        EvaluateAvailability = context =>
        {
            var baseResult = EvaluateBase(context, connections);
            if (!baseResult.Available)
                return baseResult;
            var farmland = economy.GetFarmland(context.Actor);
            if (!TryPropertyId(context, out var propertyId))
                return farmland.Count > 0 ? ActionEvaluationResult.Allowed() : ActionEvaluationResult.Denied(ActionReasonCodes.ResourceUnavailable, "No farmland is available.");
            return farmland.Any(asset => asset.Id == propertyId)
                ? ActionEvaluationResult.Allowed()
                : ActionEvaluationResult.Denied(ActionReasonCodes.ResourceUnavailable, "That farmland is no longer available.");
        },
        Execute = context =>
        {
            var connection = Resolve(context, connections);
            if (connection is null || !TryPropertyId(context, out var propertyId)
                || !connections.GiveFarmland(context.Actor, connection, propertyId))
            {
                return new(false, "The selected farmland can no longer be given.", ActionReasonCodes.ResourceUnavailable);
            }
            Publish(context, connection, family,
                $"{family.GetDisplayName(context.Actor)} gave farmland to {connection.Name}.");
            return new(true);
        }
    };

    private static GameActionDefinition CreateRequestMoney(
        CommunityConnectionService connections,
        IFamilyService family) => new()
    {
        Id = "community.connection.request_money",
        Label = "Request Money",
        Description = "Ask this acquaintance for money. The request damages the relationship before the outcome, and refusal damages it further. Acceptance is deliberately rare.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        EvaluateAvailability = context =>
        {
            var baseResult = EvaluateBase(context, connections);
            if (!baseResult.Available)
                return baseResult;
            var connection = Resolve(context, connections);
            if (connection is null)
                return ActionEvaluationResult.Denied(ActionReasonCodes.NoLongerEligible, "This household connection is no longer available.");
            if (!connections.CanRequestMoney(context.Actor, connection, out var eligibilityReason))
                return ActionEvaluationResult.Denied(ActionReasonCodes.NoLongerEligible, eligibilityReason);
            var maximum = connections.GetEstimatedMoneyRequestMaximum(context.Actor, ReadConnectionId(context));
            if (!TryAmount(context, out var amount))
                return maximum >= 1000m ? ActionEvaluationResult.Allowed() : ActionEvaluationResult.Denied(ActionReasonCodes.ResourceUnavailable, "This connection cannot provide meaningful financial help.");
            return connections.IsValidMoneyAmount(amount) && amount <= maximum
                ? ActionEvaluationResult.Allowed()
                : ActionEvaluationResult.Denied(ActionReasonCodes.InvalidParameter, "Choose a valid amount within this connection's estimated means.");
        },
        Execute = context =>
        {
            var connection = Resolve(context, connections);
            if (connection is null || !TryAmount(context, out var amount)
                || !connections.RequestMoney(context.Actor, connection, amount, out _))
            {
                return new(false, "This request is no longer available.", ActionReasonCodes.ResourceUnavailable);
            }
            return new(true);
        }
    };

    private static GameActionDefinition CreateRequestHouse(
        CommunityConnectionService connections,
        IFamilyService family) => new()
    {
        Id = "community.connection.request_house",
        Label = "Request House",
        Description = "Ask for an abstract spare house. The relationship is strained immediately and acceptance starts from only 3% before modifiers.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        EvaluateAvailability = context =>
        {
            var baseResult = EvaluateBase(context, connections);
            if (!baseResult.Available)
                return baseResult;
            var connection = Resolve(context, connections);
            if (connection is null)
                return ActionEvaluationResult.Denied(ActionReasonCodes.NoLongerEligible, "This household connection is no longer available.");
            if (!connections.CanRequestMajorAsset(context.Actor, connection, out var eligibilityReason))
                return ActionEvaluationResult.Denied(ActionReasonCodes.NoLongerEligible, eligibilityReason);
            return connection.HasSpareHouse
                ? ActionEvaluationResult.Allowed()
                : ActionEvaluationResult.Denied(ActionReasonCodes.ResourceUnavailable, "This connection has no spare house.");
        },
        Execute = context =>
        {
            var connection = Resolve(context, connections);
            return connection is not null && connections.RequestHouse(context.Actor, connection, out _)
                ? new(true)
                : new(false, "This request is no longer available.", ActionReasonCodes.ResourceUnavailable);
        }
    };

    private static GameActionDefinition CreateRequestFarmland(
        CommunityConnectionService connections,
        IFamilyService family) => new()
    {
        Id = "community.connection.request_farmland",
        Label = "Request Farmland",
        Description = "Ask for an abstract spare farmland parcel. The relationship is strained immediately and acceptance starts from only 4% before modifiers.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        EvaluateAvailability = context =>
        {
            var baseResult = EvaluateBase(context, connections);
            if (!baseResult.Available)
                return baseResult;
            var connection = Resolve(context, connections);
            if (connection is null)
                return ActionEvaluationResult.Denied(ActionReasonCodes.NoLongerEligible, "This household connection is no longer available.");
            if (!connections.CanRequestMajorAsset(context.Actor, connection, out var eligibilityReason))
                return ActionEvaluationResult.Denied(ActionReasonCodes.NoLongerEligible, eligibilityReason);
            return connection.HasSpareFarmland
                ? ActionEvaluationResult.Allowed()
                : ActionEvaluationResult.Denied(ActionReasonCodes.ResourceUnavailable, "This connection has no spare farmland.");
        },
        Execute = context =>
        {
            var connection = Resolve(context, connections);
            return connection is not null && connections.RequestFarmland(context.Actor, connection, out _)
                ? new(true)
                : new(false, "This request is no longer available.", ActionReasonCodes.ResourceUnavailable);
        }
    };

    private static ActionEvaluationResult EvaluateBase(
        GameActionContext context,
        CommunityConnectionService connections)
    {
        if (!context.ActorHasControl
            || context.Actor.Id != context.Target.Id
            || !context.Actor.Tags.Has("state.alive")
            || context.Actor.Age < 18
            || !context.Parameters.TryGetValue(ContextParameter, out var relationsContext)
            || !relationsContext.Equals("true", StringComparison.OrdinalIgnoreCase)
            || !TryConnectionId(context, out var connectionId)
            || connections.FindForActor(context.Actor, connectionId) is not { IsActive: true })
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "This household connection is no longer available.");
        }

        return ActionEvaluationResult.Allowed();
    }

    private static CommunityConnectionState? Resolve(
        GameActionContext context,
        CommunityConnectionService connections) =>
        TryConnectionId(context, out var connectionId)
            ? connections.FindForActor(context.Actor, connectionId) is { IsActive: true } connection
                ? connection
                : null
            : null;

    private static Guid ReadConnectionId(GameActionContext context) =>
        TryConnectionId(context, out var id) ? id : Guid.Empty;

    private static bool TryConnectionId(GameActionContext context, out Guid id)
    {
        id = Guid.Empty;
        return context.Parameters.TryGetValue(ConnectionIdParameter, out var raw)
            && Guid.TryParse(raw, out id);
    }

    private static bool TryAmount(GameActionContext context, out decimal amount)
    {
        amount = 0m;
        return context.Parameters.TryGetValue("amount", out var raw)
            && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private static bool TryPropertyId(GameActionContext context, out Guid id)
    {
        id = Guid.Empty;
        return context.Parameters.TryGetValue("propertyId", out var raw)
            && Guid.TryParse(raw, out id);
    }

    private static void Publish(
        GameActionContext context,
        CommunityConnectionState connection,
        IFamilyService family,
        string text)
    {
        context.EventBus.Publish(new GameEvent
        {
            Type = "connection.interaction",
            Year = context.GameState.Year,
            SubjectId = context.Actor.Id,
            Data = new Dictionary<string, string>
            {
                ["connectionId"] = connection.Id.ToString("D"),
                ["connectionName"] = connection.Name,
                ["text"] = text,
                ["familyNews"] = "true"
            }
        });
    }
}
