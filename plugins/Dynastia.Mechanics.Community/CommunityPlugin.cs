using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

public sealed class CommunityPlugin : IGamePlugin
{
    private const string PolicyIdParameter = "communityPolicyId";
    private const string ProposalYearParameter = "communityProposalYear";
    private const string TownIdParameter = "communityTownId";

    public void Initialize(IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
        var gameState = Require<IGameState>(context, "Game state");
        var data = Require<IGameDataService>(context, "Game data service");
        var opportunities = Require<ILocalCareerOpportunityService>(context, "Local opportunity service");
        var institutions = Require<ITownInstitutionService>(context, "Town institution service");
        var prosperity = Require<ITownProsperityService>(context, "Town prosperity service");
        var nationalities = Require<INationalityService>(context, "Nationality service");
        var names = Require<IHistoricalNameService>(context, "Historical name service");
        var locations = Require<ILocationService>(context, "Location service");
        var economy = Require<IEconomyService>(context, "Economy service");
        var households = Require<IHouseholdService>(context, "Household service");
        var family = Require<IFamilyService>(context, "Family service");
        var status = Require<IStatusService>(context, "Status service");
        var education = Require<IEducationService>(context, "Education service");
        var stats = Require<IStatsService>(context, "Stats service");
        var career = Require<ICareerService>(context, "Career service");
        var crafts = Require<ICraftService>(context, "Craft service");
        var economicStrength = Require<ILocalEconomicStrengthService>(context, "Local economic-strength service");
        var random = Require<IGameRandom>(context, "Game random service");
        var events = Require<IGameEventBus>(context, "Game event bus");
        var actions = Require<IActionRegistry>(context, "Action registry");
        var systems = Require<IYearSystemRegistry>(context, "Year system registry");

        var catalog = CommunityPolicyCatalog.Load(data);
        var rules = CommunityPolicyRules.Load(data);
        var civicCatalog = CivicOfficeCatalog.Load(data);
        var civicRules = CivicOfficeRules.Load(data);
        var connectionRules = CommunityConnectionRules.Load(data);
        var community = new CommunityPolicyService(
            gameState,
            opportunities,
            institutions,
            prosperity,
            nationalities,
            names,
            catalog,
            rules);
        var connections = new CommunityConnectionService(
            gameState,
            community,
            catalog,
            connectionRules,
            economy,
            status,
            locations,
            nationalities,
            names,
            family,
            random,
            events);
        var civic = new CivicOfficeService(
            gameState,
            locations,
            economy,
            family,
            nationalities,
            names,
            status,
            education,
            stats,
            career,
            crafts,
            prosperity,
            economicStrength,
            random,
            events,
            civicCatalog,
            civicRules,
            () => context.GetService<ICriminalOccupationService>());

        context.AddService<ICommunityPolicyService>(community);
        context.AddService<IHouseholdConnectionService>(connections);
        context.AddService<ICivicOfficeService>(civic);
        RegisterLobbyAction(
            actions,
            community,
            locations,
            economy,
            family,
            status,
            education,
            stats,
            rules,
            civic,
            connections);
        systems.Register(new CommunityPolicyYearSystem(
            community,
            households,
            economy,
            status,
            locations,
            random,
            events,
            civic));
        systems.Register(new CivicOfficeYearSystem(civic));
        systems.Register(new CommunityConnectionYearSystem(connections));
        RegisterOfficeDutiesAction(actions, civic, family);
        CommunityConnectionActions.Register(actions, connections, economy, family);

        context.GetService<IStateReconciliationLifecycle>()?.Register(
            "community.civic_office_tags",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterQueuedAction
            ],
            _ => civic.ReconcileTags(),
            order: 245);

        context.Log($"Community policies registered: {catalog.Policies.Count} policy definitions.");
    }

    private static void RegisterLobbyAction(
        IActionRegistry actions,
        CommunityPolicyService community,
        ILocationService locations,
        IEconomyService economy,
        IFamilyService family,
        IStatusService status,
        IEducationService education,
        IStatsService stats,
        CommunityPolicyRules rules,
        CivicOfficeService civic,
        CommunityConnectionService connections)
    {
        actions.Register(new GameActionDefinition
        {
            Id = "community.lobby_policy",
            Presentation = new()
            {
                Emoji = "🗣️",
                Categories = [ActionPresentationCategories.Personal]
            },
            Label = "Lobby for Policy",
            Description = "Publicly support one current local proposal. Lobbying improves local standing and civic participation, but never guarantees passage.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            EvaluateAvailability = actionContext =>
            {
                if (actionContext.Actor.Id != actionContext.Target.Id
                    || !actionContext.Actor.Tags.Has("state.alive")
                    || actionContext.Actor.Age < 18
                    || !actionContext.ActorHasControl)
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "Community lobbying is performed by the active adult household controller.");
                }

                var householdId = economy.GetHouseholdId(actionContext.Actor);
                if (householdId is null)
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.ResourceUnavailable,
                        "An active household is required.");
                }

                if (!TryReadParameters(
                        actionContext.Parameters,
                        out var townId,
                        out var proposalYear,
                        out var policyId))
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.InvalidParameter,
                        "Choose one of this year's community proposals.");
                }

                var town = locations.FindTownAtYear(townId, proposalYear);
                if (town is null)
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "That proposal's town is no longer available.");
                }

                if (!actionContext.ExecutionPhase.HasValue)
                {
                    var residence = economy.GetResidenceTown(actionContext.Actor);
                    if (!residence.Id.Equals(townId, StringComparison.OrdinalIgnoreCase)
                        || proposalYear != actionContext.GameState.Year)
                    {
                        return ActionEvaluationResult.Denied(
                            ActionReasonCodes.NoLongerEligible,
                            "Only current proposals in the household's own town can be lobbied.");
                    }
                }
                else if (proposalYear != actionContext.GameState.Year - 1)
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "This proposal has expired.");
                }

                if (community.FindProposal(town, proposalYear, policyId) is null)
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "This proposal is no longer part of the town's proposal set.");
                }

                if (community.HasLobby(householdId.Value, townId, proposalYear))
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "This household has already lobbied a proposal for that year.");
                }

                var bonus = community.CalculateLobbyBonus(
                    actionContext.Actor,
                    status,
                    education,
                    stats);
                return ActionEvaluationResult.Allowed(
                    householdId,
                    presentationMetadata: new Dictionary<string, string>
                    {
                        ["supportBonus"] = bonus.ToString("0.###", CultureInfo.InvariantCulture)
                    });
            },
            Execute = actionContext =>
            {
                if (!TryReadParameters(
                        actionContext.Parameters,
                        out var townId,
                        out var proposalYear,
                        out var policyId))
                {
                    return new GameActionResult(false, "The queued proposal is invalid.", ActionReasonCodes.InvalidParameter);
                }

                var householdId = economy.GetHouseholdId(actionContext.Actor);
                var town = locations.FindTownAtYear(townId, proposalYear);
                var proposal = town is null
                    ? null
                    : community.FindProposal(town, proposalYear, policyId);
                if (householdId is null || proposal is null)
                {
                    return new GameActionResult(false, "The queued proposal is no longer available.", ActionReasonCodes.NoLongerEligible);
                }

                var bonus = community.CalculateLobbyBonus(
                    actionContext.Actor,
                    status,
                    education,
                    stats);
                var connectionId = Guid.TryParse(proposal.Proposer.Id, out var parsedConnectionId)
                    ? parsedConnectionId
                    : Guid.Empty;
                var connectionExisted = connectionId != Guid.Empty
                    && connections.Has(householdId.Value, connectionId);
                community.RecordLobby(
                    actionContext.Actor,
                    householdId.Value,
                    proposal,
                    bonus);
                if (connectionId != Guid.Empty)
                {
                    connections.InitializeLobbyConnection(
                        actionContext.Actor,
                        connectionId,
                        publishCreated: !connectionExisted);
                }
                civic.MarkOfficeAction(actionContext.Actor);
                status.ApplyPersistentDelta(
                    actionContext.Actor,
                    rules.LobbyRenownGain,
                    rules.LobbyReputationGain,
                    "community.lobby");

                actionContext.EventBus.Publish(new GameEvent
                {
                    Type = "community.lobby",
                    Year = actionContext.GameState.Year,
                    SubjectId = actionContext.Actor.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["townId"] = proposal.TownId,
                        ["policyId"] = proposal.PolicyId,
                        ["policyName"] = proposal.DisplayName,
                        ["connectionName"] = proposal.Proposer.Name,
                        ["supportBonus"] = bonus.ToString("0.###", CultureInfo.InvariantCulture),
                        ["text"] = $"{family.GetDisplayName(actionContext.Actor)} publicly supported {proposal.DisplayName}, proposed by {proposal.Proposer.Name}."
                    }
                });

                return new GameActionResult(true);
            }
        });
    }

    private static void RegisterOfficeDutiesAction(
        IActionRegistry actions,
        CivicOfficeService civic,
        IFamilyService family)
    {
        actions.Register(new GameActionDefinition
        {
            Id = "community.perform_office_duties",
            Presentation = new()
            {
                Emoji = "🏛️",
                Categories = [ActionPresentationCategories.Personal]
            },
            Label = "Perform Office Duties",
            Description = "Devote the year to the responsibilities of Town Head. This prevents civic neglect and slightly improves local standing and approval.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            EvaluateAvailability = actionContext =>
            {
                if (actionContext.Actor.Id != actionContext.Target.Id
                    || !actionContext.ActorHasControl
                    || !actionContext.Actor.Tags.Has("state.alive")
                    || actionContext.Actor.Tags.Has("state.imprisoned")
                    || !civic.IsTownHead(actionContext.Actor))
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "Only the current simulated Town Head can perform office duties.");
                }

                return ActionEvaluationResult.Allowed();
            },
            Execute = actionContext =>
            {
                if (!civic.IsTownHead(actionContext.Actor)
                    || actionContext.Actor.Tags.Has("state.imprisoned"))
                {
                    return new GameActionResult(
                        false,
                        "The civic office is no longer held by this person.",
                        ActionReasonCodes.NoLongerEligible);
                }

                civic.PerformDuties(actionContext.Actor);
                actionContext.EventBus.Publish(new GameEvent
                {
                    Type = "community.office_duties",
                    Year = actionContext.GameState.Year,
                    SubjectId = actionContext.Actor.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["text"] = $"{family.GetDisplayName(actionContext.Actor)} devoted the year to civic office duties."
                    }
                });
                return new GameActionResult(true);
            }
        });
    }

    private static bool TryReadParameters(
        IReadOnlyDictionary<string, string> parameters,
        out string townId,
        out int proposalYear,
        out string policyId)
    {
        townId = string.Empty;
        policyId = string.Empty;
        proposalYear = 0;
        if (!parameters.TryGetValue(TownIdParameter, out var rawTown)
            || string.IsNullOrWhiteSpace(rawTown)
            || !parameters.TryGetValue(PolicyIdParameter, out var rawPolicy)
            || string.IsNullOrWhiteSpace(rawPolicy)
            || !parameters.TryGetValue(ProposalYearParameter, out var rawYear)
            || !int.TryParse(rawYear, NumberStyles.Integer, CultureInfo.InvariantCulture, out proposalYear))
        {
            return false;
        }

        townId = rawTown.Trim();
        policyId = rawPolicy.Trim();
        return true;
    }

    private static T Require<T>(IGamePluginContext context, string name)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{name} is unavailable.");
}
