using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static partial class FamilyRelationActions
{
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
        Label = "Request a House",
        Description = "Request property from this relative's household. Acceptance depends on Familiarity, Sympathy and the donor's wealth/assets; a household will not surrender its only essential home.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && !targetHead.Tags.Has("control.playable")
            && economy.GetHouses(targetHead).Count > 0,
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null
                || targetHead.Tags.Has("control.playable"))
            {
                return new(false);
            }

            var targetFinance = economy.GetHousehold(targetHead);
            var actorFinance = economy.GetHousehold(c.Actor);
            var abilityFactor = targetFinance is null || actorFinance is null
                ? 0
                : FamilySupportAbilityRules.GetHouseRequestAbilityFactor(
                    targetFinance.Wealth,
                    actorFinance.Wealth,
                    GetProjectedAnnualExpenses(targetHead, economy),
                    economy.GetHouses(targetHead).Count,
                    economy.GetFarmland(targetHead).Count);

            if (abilityFactor <= 0
                || random.NextDouble() >= relations.EvaluateRequestWillingness(
                    c.Actor,
                    c.Target,
                    abilityFactor))
            {
                relations.RecordInteraction(c.Actor, c.Target, 2, -8);
                Publish(events, c, "family_relations.house_refused", family,
                    $"{family.GetDisplayName(c.Target)} declined {family.GetDisplayName(c.Actor)}'s request for a house.");
                return new(true);
            }

            var recipientHadHouse = economy.GetHouses(c.Actor).Count > 0;
            var house = economy.TakeAdditionalHouse(targetHead);
            if (house is null)
                return new(false);

            economy.AddExistingHouse(c.Actor, house with { AssignedHeirId = null });
            var origin = locations.GetLocation(c.Actor).HomeTown;
            if (!recipientHadHouse
                && !origin.Id.Equals(house.Town.Id, StringComparison.OrdinalIgnoreCase))
            {
                RelocateHousehold(gameState, c.Actor, house.Town, family, economy, career, events);
            }

            relations.RecordInteraction(c.Actor, c.Target, 8, 8);
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
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.give_house",
        Label = "Transfer a House",
        Description = "Offer one non-residence property to this relative's household. Very hostile relatives may refuse. If accepted and they own no home, they relocate to the property when it is elsewhere.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is not null
            && economy.GetHouses(c.Actor).Count >= 2,
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null || economy.GetHouses(c.Actor).Count < 2)
                return new(false);

            if (!c.Parameters.TryGetValue("propertyId", out var raw)
                || !Guid.TryParse(raw, out var propertyId))
            {
                return new(false, "No property was selected.");
            }

            var selected = economy.GetHouses(c.Actor)
                .FirstOrDefault(h => h.Id == propertyId && h.IsRented);
            if (selected is null)
                return new(false, "That property is no longer available.");

            if (random.NextDouble() >= relations.EvaluateOfferWillingness(c.Actor, c.Target))
            {
                relations.RecordInteraction(c.Actor, c.Target, 1, -2);
                Publish(events, c, "family_relations.house_gift_refused", family,
                    $"{family.GetDisplayName(c.Target)} refused {family.GetDisplayName(c.Actor)}'s offer of a house.");
                return new(true);
            }

            var recipientHadHouse = economy.GetHouses(targetHead).Count > 0;
            var house = economy.TakeHouse(c.Actor, propertyId);
            if (house is null)
                return new(false);

            economy.AddExistingHouse(targetHead, house with { AssignedHeirId = null });
            var targetTown = locations.GetLocation(targetHead).HomeTown;
            if (!recipientHadHouse
                && !targetTown.Id.Equals(house.Town.Id, StringComparison.OrdinalIgnoreCase))
            {
                RelocateHousehold(gameState, targetHead, house.Town, family, economy, career, events);
            }

            relations.RecordInteraction(c.Actor, c.Target, 8, 8);
            Publish(events, c, "family_relations.house_given", family,
                $"{family.GetDisplayName(c.Actor)} gave {family.GetDisplayName(c.Target)} a house in {house.Town.Town}.");
            return new(true);
        }
    };

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
        if (origin.Id.Equals(destination.Id, StringComparison.OrdinalIgnoreCase))
            return;

        var ids = economy.GetHouseholdMemberIds(head).ToHashSet();
        var employed = gameState.People
            .Where(p => ids.Contains(p.Id)
                && p.Tags.Has("state.alive")
                && p.Age >= 18
                && !p.Tags.Has("role.nanny")
                && !p.Tags.Has("role.family_nanny"))
            .Where(p =>
            {
                var c = career.GetCareer(p);
                return !c.IsRetired && c.IsEmployed && !c.IsSelfEmployed;
            })
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
}



internal static partial class FamilyRelationActions
{
    private static GameActionDefinition CreateAskFarmland(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family) => new()
    {
        Id = "family_relations.ask_farmland",
        Label = "Ask for Farmland",
        Description = "Request farmland from this relative's household. Acceptance depends on Familiarity, Sympathy, relative wealth and how much land the household can truly spare; its final essential parcel is protected.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && !targetHead.Tags.Has("control.playable")
            && economy.GetFarmland(targetHead).Count > 0,
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null || targetHead.Tags.Has("control.playable"))
                return new(false);

            var farmland = economy.GetFarmland(targetHead);
            if (farmland.Count == 0)
                return new(false);

            var targetFinance = economy.GetHousehold(targetHead);
            var actorFinance = economy.GetHousehold(c.Actor);
            var abilityFactor = targetFinance is null || actorFinance is null
                ? 0
                : FamilySupportAbilityRules.GetFarmlandRequestAbilityFactor(
                    targetFinance.Wealth,
                    actorFinance.Wealth,
                    GetProjectedAnnualExpenses(targetHead, economy),
                    farmland.Count,
                    economy.GetHouses(targetHead).Count);

            if (abilityFactor <= 0
                || random.NextDouble() >= relations.EvaluateRequestWillingness(
                    c.Actor,
                    c.Target,
                    abilityFactor))
            {
                relations.RecordInteraction(c.Actor, c.Target, 2, -8);
                Publish(
                    events,
                    c,
                    "farmland.request_refused",
                    family,
                    $"{family.GetDisplayName(c.Target)} declined {family.GetDisplayName(c.Actor)}'s request for farmland.");
                return new(true);
            }

            var residence = economy.GetResidenceTown(targetHead);
            var selected = farmland
                .OrderBy(asset =>
                    asset.Town.Id.Equals(
                        residence.Id,
                        StringComparison.OrdinalIgnoreCase)
                        ? 1
                        : 0)
                .ThenBy(asset => asset.AcquiredYear)
                .ThenBy(asset => asset.Id)
                .First();

            var transferred = economy.TakeFarmland(targetHead, selected.Id);
            if (transferred is null)
                return new(false);

            economy.AddExistingFarmland(
                c.Actor,
                transferred with
                {
                    AcquiredYear = c.GameState.Year,
                    AcquisitionSource = "family-request",
                    AssignedHeirId = null
                });
            relations.RecordInteraction(c.Actor, c.Target, 8, 8);

            Publish(
                events,
                c,
                "farmland.received",
                family,
                $"{family.GetDisplayName(c.Target)} gave {family.GetDisplayName(c.Actor)} a parcel of farmland near {transferred.Town.Town}.");

            return new(true);
        }
    };

    private static GameActionDefinition CreateGiveFarmland(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        IGameEventBus events,
        IFamilyService family) => new()
    {
        Id = "family_relations.give_farmland",
        Label = "Give Farmland",
        Description = "Transfer one farmland parcel located in this relative's current town. The oldest matching parcel is used automatically; the transfer is unconditional and never causes relocation.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && HasFarmlandInTown(
                c.Actor,
                economy.GetResidenceTown(targetHead),
                economy),
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null)
                return new(false);

            var targetResidence = economy.GetResidenceTown(targetHead);
            var selected = economy.GetFarmland(c.Actor)
                .Where(asset => asset.Town.Id.Equals(
                    targetResidence.Id,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(asset => asset.AcquiredYear)
                .ThenBy(asset => asset.Id)
                .FirstOrDefault();

            if (selected is null)
                return new(false, "No farmland is owned in this relative's current town.");

            var transferred = economy.TakeFarmland(c.Actor, selected.Id);
            if (transferred is null)
                return new(false, "That farmland parcel is no longer owned.");

            economy.AddExistingFarmland(
                targetHead,
                transferred with
                {
                    AcquiredYear = c.GameState.Year,
                    AcquisitionSource = "family-gift",
                    AssignedHeirId = null
                });
            relations.RecordInteraction(c.Actor, c.Target, 8, 12);

            Publish(
                events,
                c,
                "farmland.given",
                family,
                $"{family.GetDisplayName(c.Actor)} gave {family.GetDisplayName(c.Target)} a parcel of farmland near {transferred.Town.Town}.");

            return new(true);
        }
    };

    private static bool HasFarmlandInTown(
        IPerson actor,
        TownInfo town,
        IEconomyService economy) =>
        economy.GetFarmland(actor).Any(asset =>
            asset.Town.Id.Equals(
                town.Id,
                StringComparison.OrdinalIgnoreCase));
}
