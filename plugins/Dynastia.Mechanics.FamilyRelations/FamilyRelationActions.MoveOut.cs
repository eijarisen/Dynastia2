using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static partial class FamilyRelationActions
{
    private static GameActionDefinition CreateAskMoveOut(
        IFamilyService family,
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        IPersonalityService? personality,
        IGameRandom random,
        IGameEventBus events)
    {
        return new GameActionDefinition
        {
            Id = "household.ask_move_out",
            Label = "Ask to Move Out",
            Description =
                "Ask the selected resident adult son or eligible male-line relative to establish his own household. Providing a spare house guarantees acceptance; asking him to rent may be refused and harms the relationship.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            EvaluateAvailability = context =>
                EvaluateMoveOutAvailability(
                    context,
                    family,
                    relations,
                    households,
                    economy),
            Execute = context =>
            {
                var actor = context.Actor;
                var target = context.Target;

                if (!IsEligibleMoveOutTarget(
                        actor,
                        target,
                        context.ActorHasControl,
                        family,
                        households,
                        economy)
                    || relations.GetRelation(actor, target) is null)
                {
                    return new GameActionResult(
                        false,
                        "The selected family member is no longer eligible to move out.",
                        ActionReasonCodes.NoLongerEligible);
                }

                var spareHouses = economy
                    .GetHouses(actor)
                    .Where(house => !house.IsResidence)
                    .ToList();

                Guid? selectedPropertyId = null;
                var queuedWithProperty = context.Parameters.TryGetValue(
                    "propertyId",
                    out var propertyValue);

                if (queuedWithProperty)
                {
                    if (!Guid.TryParse(
                            propertyValue,
                            out var parsedPropertyId)
                        || !spareHouses.Any(house =>
                            house.Id == parsedPropertyId))
                    {
                        return new GameActionResult(
                            false,
                            "The selected spare house is no longer available.",
                            ActionReasonCodes.AssetNoLongerOwned);
                    }

                    selectedPropertyId = parsedPropertyId;
                }
                else if (spareHouses.Count > 0)
                {
                    if (spareHouses.Count == 1)
                    {
                        selectedPropertyId = spareHouses[0].Id;
                    }
                    else
                    {
                        return new GameActionResult(
                            false,
                            "A spare house must be selected for this move.");
                    }
                }
                else
                {
                    // The social cost belongs to making the request, not to
                    // the outcome. Parent/child or sibling kinship modifiers
                    // inside the relation service continue to apply.
                    relations.ModifyRelation(
                        actor,
                        target,
                        -10,
                        majorInteraction: true);

                    var profile = personality?.GetPersonality(target);
                    var refusalChance = MoveOutRules.CalculateRefusalChance(
                        profile?.Temperament,
                        profile?.Morals);

                    if (random.NextDouble() < refusalChance)
                    {
                        events.Publish(new GameEvent
                        {
                            Type = "household.move_out_refused",
                            Year = context.GameState.Year,
                            SubjectId = actor.Id,
                            RelatedPersonIds = [target.Id],
                            Data = new Dictionary<string, string>
                            {
                                ["refusalChance"] = refusalChance.ToString(
                                    "0.00",
                                    System.Globalization.CultureInfo.InvariantCulture),
                                ["familyNews"] = "true",
                                ["text"] =
                                    $"{family.GetDisplayName(actor)} asked {family.GetDisplayName(target)} to move out and rent a home of his own, but {family.GetDisplayName(target)} refused."
                            }
                        });

                        return new GameActionResult(true);
                    }
                }

                var move = households.EstablishIndependentResidentBranch(
                    actor,
                    target,
                    selectedPropertyId);

                if (!move.Success)
                {
                    return new GameActionResult(
                        false,
                        move.Reason ?? "The household move could not be completed.");
                }

                var destination = move.DestinationTown?.Town
                    ?? economy.GetResidenceTown(target).Town;

                var relatedIds = move.MovedMemberIds
                    .Where(id => id != target.Id)
                    .Prepend(target.Id)
                    .Distinct()
                    .ToList();

                events.Publish(new GameEvent
                {
                    Type = "household.son_moved_out",
                    Year = context.GameState.Year,
                    SubjectId = actor.Id,
                    RelatedPersonIds = relatedIds,
                    Data = new Dictionary<string, string>
                    {
                        ["targetSonId"] = target.Id.ToString(),
                        ["propertyId"] = move.TransferredHouse?.Id.ToString() ?? string.Empty,
                        ["town"] = destination,
                        ["providedHouse"] = move.ProvidedHouse ? "true" : "false",
                        ["rented"] = move.ProvidedHouse ? "false" : "true",
                        ["familyNews"] = "true",
                        ["text"] = move.ProvidedHouse
                            ? $"{family.GetDisplayName(actor)} asked {family.GetDisplayName(target)} to establish his own household. {family.GetDisplayName(target)} accepted the house in {destination} and moved there with his family."
                            : $"{family.GetDisplayName(actor)} asked {family.GetDisplayName(target)} to move out. {family.GetDisplayName(target)} agreed and established a rented household in {destination}."
                    }
                });

                return new GameActionResult(true);
            }
        };
    }

    private static ActionEvaluationResult EvaluateMoveOutAvailability(
        GameActionContext context,
        IFamilyService family,
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy)
    {
        var householdId = economy.GetHouseholdId(context.Actor);

        if (!IsEligibleMoveOutTarget(
                context.Actor,
                context.Target,
                context.ActorHasControl,
                family,
                households,
                economy)
            || relations.GetRelation(
                context.Actor,
                context.Target) is null)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "The selected family member is no longer eligible to move out.",
                householdId);
        }

        if (!context.Parameters.TryGetValue(
                "propertyId",
                out var propertyValue))
        {
            return ActionEvaluationResult.Allowed(householdId);
        }

        if (!Guid.TryParse(propertyValue, out var propertyId))
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.InvalidParameter,
                "The selected spare house is invalid.",
                householdId);
        }

        var available = economy.GetHouses(context.Actor)
            .Any(house =>
                house.Id == propertyId
                && !house.IsResidence);

        if (!available)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.AssetNoLongerOwned,
                "The selected spare house is no longer available.",
                householdId,
                presentationMetadata: new Dictionary<string, string>
                {
                    ["propertyId"] = propertyId.ToString()
                });
        }

        return ActionEvaluationResult.Allowed(
            householdId,
            presentationMetadata: new Dictionary<string, string>
            {
                ["propertyId"] = propertyId.ToString()
            });
    }

    private static bool IsEligibleMoveOutTarget(
        IPerson actor,
        IPerson target,
        bool actorHasControl,
        IFamilyService family,
        IHouseholdService households,
        IEconomyService economy)
    {
        if (!actor.Tags.Has("state.alive")
            || !actorHasControl
            || actor.Tags.Has("state.imprisoned")
            || !economy.HasHousehold(actor)
            || households.ResolveHouseholdHead(actor)?.Id != actor.Id
            || !target.Tags.Has("state.alive")
            || target.Tags.Has("state.imprisoned")
            || target.Id == actor.Id
            || target.Age < 18
            || family.GetSex(target) != Sex.Male
            || !family.IsBloodline(target)
            || !family.IsMaleLineage(target)
            || economy.HasHousehold(target)
            || households.ResolveHouseholdHead(target)?.Id != actor.Id)
        {
            return false;
        }

        var directSon = family.GetChildren(actor)
            .Any(child => child.Id == target.Id);

        if (directSon)
            return true;

        var father = family.GetFather(target);
        return father is null
            || !father.Tags.Has("state.alive")
            || households.ResolveHouseholdHead(father)?.Id != actor.Id;
    }
}
