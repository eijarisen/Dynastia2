using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class HouseholdsPlugin
{
    private static void RegisterPropertyActions(
        IActionRegistry actions,
        IGameState gameState,
        IFamilyService family,
        IHouseholdService households,
        IEconomyService economy,
        ILocationService locations,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "household.buy_house",
                Label = "Buy a House",
                Description =
                    "Choose any town and queue a property purchase. The local house price is paid when the action resolves next year. Buying elsewhere creates a rented investment and never moves the household.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = context =>
                {
                    if (!CanActOnSelf(context))
                        return false;

                    return economy.GetHousehold(context.Actor)
                        is not null;
                },
                Execute = context =>
                {
                    context.Parameters.TryGetValue("townId", out var townId);
                    var town = string.IsNullOrWhiteSpace(townId)
                        ? locations.GetLocation(context.Actor).HomeTown
                        : locations.FindTown(townId);
                    var household = economy.GetHousehold(context.Actor);
                    if (town is null || household is null)
                        return new GameActionResult(false);

                    var price = economy.GetHousePrice(town);
                    if (household.Wealth < price)
                        return new GameActionResult(false, "The household can no longer afford this property.");

                    economy.ChangeWealth(context.Actor, -price);
                    var house = economy.AddHouse(context.Actor, town);

                    events.Publish(new GameEvent
                    {
                        Type = "household.house_bought",
                        Year = context.GameState.Year,
                        SubjectId = context.Actor.Id,
                        Data = new Dictionary<string, string>
                        {
                            ["amount"] = price.ToString(),
                            ["town"] = town.Town,
                            ["propertyId"] = house.Id.ToString(),
                            ["text"] = $"{family.GetDisplayName(context.Actor)} bought a house in {town.Town} for {price:N0} zł."
                        }
                    });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "household.sell_house",
                Label = "Sell a House",
                Description =
                    "Choose one owned property and sell it for 80% of that town's current local house price. Selling the residence never causes relocation; the household simply rents in the same town if no local house remains.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = context =>
                    CanActOnSelf(context)
                    && economy.GetHouses(context.Actor).Count > 0,
                Execute = context =>
                {
                    context.Parameters.TryGetValue("propertyId", out var propertyIdRaw);
                    var owned = economy.GetHouses(context.Actor);
                    Guid propertyId;
                    if (!Guid.TryParse(propertyIdRaw, out propertyId))
                    {
                        // Compatibility for saves made while Sell a House still
                        // selected a property implicitly. Prefer an investment,
                        // then fall back to the residence.
                        var legacySelection = owned.LastOrDefault(house => house.IsRented)
                            ?? owned.LastOrDefault();
                        if (legacySelection is null)
                            return new GameActionResult(false);
                        propertyId = legacySelection.Id;
                    }

                    var existing = owned
                        .FirstOrDefault(house => house.Id == propertyId);
                    if (existing is null)
                        return new GameActionResult(false, "The selected property is no longer owned.");

                    var sold = economy.TakeHouse(context.Actor, propertyId);
                    if (sold is null)
                        return new GameActionResult(false);

                    var saleValue = economy.GetHouseSaleValue(sold.Town);
                    economy.ChangeWealth(context.Actor, saleValue);

                    events.Publish(new GameEvent
                    {
                        Type = "household.house_sold",
                        Year = context.GameState.Year,
                        SubjectId = context.Actor.Id,
                        Data = new Dictionary<string, string>
                        {
                            ["amount"] = saleValue.ToString(),
                            ["town"] = sold.Town.Town,
                            ["propertyId"] = sold.Id.ToString(),
                            ["text"] = $"{family.GetDisplayName(context.Actor)} sold the house in {sold.Town.Town} for {saleValue:N0} zł."
                        }
                    });

                    return new GameActionResult(true);
                }
            });

        actions.RegisterDynamicProvider(
            (actor, target) =>
            {
                // Dynamic move actions exist only for towns in which this
                // household actually owns property. Do not generate one
                // action for every town in the location database.
                var currentTown = locations.GetLocation(actor).HomeTown;
                var destinations = economy.GetHouses(actor)
                    .Select(house => house.Town)
                    .Where(town => !town.Id.Equals(
                        currentTown.Id,
                        StringComparison.OrdinalIgnoreCase))
                    .GroupBy(
                        town => town.Id,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();

                return destinations
                    .Select(town =>
                        new GameActionDefinition
                        {
                            Id = $"household.move.{town.Id}",
                            Label = $"Move to {town.Town}",
                            Description =
                                $"Move the household to its owned property in {town.DisplayName}. Current employment ends and each employed household member immediately tries to establish replacement work in the destination labour market.",
                            Mode = ActionExecutionMode.Queued,
                            QueuePhase = YearPhase.QueuedActionsEarly,
                            IsAvailable = context =>
                            {
                                if (!CanActOnSelf(context))
                                    return false;

                                var current = locations.GetLocation(context.Actor).HomeTown;
                                if (current.Id.Equals(town.Id, StringComparison.OrdinalIgnoreCase))
                                    return false;

                                return economy.GetHouses(context.Actor)
                                    .Any(house => house.Town.Id.Equals(
                                        town.Id,
                                        StringComparison.OrdinalIgnoreCase));
                            },
                            Execute = context =>
                            {
                                if (!economy.GetHouses(context.Actor)
                                    .Any(house => house.Town.Id.Equals(
                                        town.Id,
                                        StringComparison.OrdinalIgnoreCase)))
                                {
                                    return new GameActionResult(false);
                                }

                                RelocateHousehold(
                                    context.GameState,
                                    context.Actor,
                                    town,
                                    family,
                                    economy,
                                    locations,
                                    career,
                                    events);

                                return new GameActionResult(true);
                            }
                        })
                    .ToList();
            });

        // Legacy compatibility only. The player-facing action was replaced
        // by per-property inheritance designations in Family Inventory.
        // Keeping the definition lets saves with this already queued action
        // resolve once instead of becoming unloadable.
        actions.Register(
            new GameActionDefinition
            {
                Id = "household.give_house_to_son",
                Label = "Give House to Son",
                Description =
                    "Legacy queued house gift.",
                Mode = ActionExecutionMode.Immediate,
                IsAvailable = context =>
                {
                    if (!ActionCompatibilityParameters.IsRestoredQueuedAction(
                            context.Parameters))
                    {
                        return false;
                    }

                    var actor = context.Actor;
                    var target = context.Target;
                    if (!actor.Tags.Has("state.alive")
                        || !actor.Tags.Has("control.playable")
                        || !target.Tags.Has("state.alive")
                        || target.Id == actor.Id
                        || family.GetSex(target) != Sex.Male
                        || !family.GetChildren(actor).Any(child => child.Id == target.Id))
                    {
                        return false;
                    }

                    return economy.GetHouses(actor).Any(house => house.IsRented);
                },
                Execute = context =>
                {
                    var actor = context.Actor;
                    var son = context.Target;
                    var gifted = economy.TakeAdditionalHouse(actor);
                    if (gifted is null)
                        return new GameActionResult(false);

                    if (son.Age >= 18)
                    {
                        economy.EnsureHousehold(son);
                        economy.AddExistingHouse(son, gifted with { AssignedHeirId = null });

                        events.Publish(new GameEvent
                        {
                            Type = "household.house_given",
                            Year = context.GameState.Year,
                            SubjectId = actor.Id,
                            RelatedPersonIds = [son.Id],
                            Data = new Dictionary<string, string>
                            {
                                ["town"] = gifted.Town.Town,
                                ["text"] = $"{family.GetDisplayName(actor)} gave the house in {gifted.Town.Town} to their son, {family.GetDisplayName(son)}."
                            }
                        });
                    }
                    else
                    {
                        economy.AddPendingHouse(son, gifted with { AssignedHeirId = null });
                        events.Publish(new GameEvent
                        {
                            Type = "household.house_promised",
                            Year = context.GameState.Year,
                            SubjectId = actor.Id,
                            RelatedPersonIds = [son.Id],
                            Data = new Dictionary<string, string>
                            {
                                ["town"] = gifted.Town.Town,
                                ["text"] = $"{family.GetDisplayName(actor)} promised the house in {gifted.Town.Town} to their son, {family.GetDisplayName(son)}, upon adulthood."
                            }
                        });
                    }

                    return new GameActionResult(true);
                }
            });
    }

    private static void RegisterAskParentHouseActions(
        IActionRegistry actions,
        IGameState gameState,
        IFamilyService family,
        IHouseholdService households,
        IEconomyService economy,
        ILocationService locations,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events)
    {
        RegisterAskParentHouseAction(
            actions,
            "household.ask_parents_house",
            "Ask Parents for a House",
            actor =>
            {
                var father = family.GetFather(actor);
                var mother = family.GetMother(actor);
                if (father is null || mother is null || !father.Tags.Has("state.alive") || !mother.Tags.Has("state.alive"))
                    return null;

                var fatherHead = households.ResolveHouseholdHead(father);
                var motherHead = households.ResolveHouseholdHead(mother);
                return fatherHead is not null && motherHead is not null
                    && economy.GetHouseholdId(fatherHead) == economy.GetHouseholdId(motherHead)
                        ? fatherHead
                        : null;
            },
            gameState, family, households, economy, locations, career, random, events);

        RegisterAskParentHouseAction(
            actions,
            "household.ask_father_house",
            "Ask Father for a House",
            actor =>
            {
                var father = family.GetFather(actor);
                if (father is null || !father.Tags.Has("state.alive"))
                    return null;
                var fatherHead = households.ResolveHouseholdHead(father);
                var mother = family.GetMother(actor);
                var motherHead = mother is null ? null : households.ResolveHouseholdHead(mother);
                return fatherHead is not null
                    && (motherHead is null || economy.GetHouseholdId(fatherHead) != economy.GetHouseholdId(motherHead))
                        ? fatherHead
                        : null;
            },
            gameState, family, households, economy, locations, career, random, events);

        RegisterAskParentHouseAction(
            actions,
            "household.ask_mother_house",
            "Ask Mother for a House",
            actor =>
            {
                var mother = family.GetMother(actor);
                if (mother is null || !mother.Tags.Has("state.alive"))
                    return null;
                var motherHead = households.ResolveHouseholdHead(mother);
                var father = family.GetFather(actor);
                var fatherHead = father is null ? null : households.ResolveHouseholdHead(father);
                return motherHead is not null
                    && (fatherHead is null || economy.GetHouseholdId(motherHead) != economy.GetHouseholdId(fatherHead))
                        ? motherHead
                        : null;
            },
            gameState, family, households, economy, locations, career, random, events);
    }

    private static void RegisterAskParentHouseAction(
        IActionRegistry actions,
        string id,
        string label,
        Func<IPerson, IPerson?> resolveParentHead,
        IGameState gameState,
        IFamilyService family,
        IHouseholdService households,
        IEconomyService economy,
        ILocationService locations,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events)
    {
        actions.Register(new GameActionDefinition
        {
            Id = id,
            Label = label,
            Description =
                "Ask the parental household for one spare rented property. Their Morals affect willingness. Your parents choose which spare house to give you. If it is in another town, your household will move there and current careers will end.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            IsAvailable = context =>
            {
                if (!CanActOnSelf(context) || economy.GetHouses(context.Actor).Count != 0)
                    return false;

                var parentHead = resolveParentHead(context.Actor);
                return parentHead is not null
                    && parentHead.Tags.Has("state.alive")
                    && economy.GetHouses(parentHead).Count > 1
                    && economy.GetHouses(parentHead).Any(house => house.IsRented);
            },
            Execute = context =>
            {
                var parentHead = resolveParentHead(context.Actor);
                if (parentHead is null || economy.GetHouses(context.Actor).Count != 0)
                    return new GameActionResult(false);

                var spare = economy.GetHouses(parentHead).Where(house => house.IsRented).ToList();
                if (spare.Count == 0)
                    return new GameActionResult(false);

                var chance = parentHead.Tags.Has("morals.good")
                    ? 0.65
                    : parentHead.Tags.Has("morals.evil")
                        ? 0.35
                        : 0.50;

                if (random.NextDouble() >= chance)
                {
                    events.Publish(new GameEvent
                    {
                        Type = "household.parent_house_refused",
                        Year = context.GameState.Year,
                        SubjectId = context.Actor.Id,
                        RelatedPersonIds = [parentHead.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["suppressChronicle"] = "true",
                            ["text"] = $"{family.GetDisplayName(parentHead)} declined {family.GetDisplayName(context.Actor)}'s request for a house."
                        }
                    });
                    return new GameActionResult(true);
                }

                var selected = spare[random.NextInt(0, spare.Count - 1)];
                var gifted = economy.TakeHouse(parentHead, selected.Id);
                if (gifted is null)
                    return new GameActionResult(false);

                economy.AddExistingHouse(context.Actor, gifted with { AssignedHeirId = null });
                var currentTown = locations.GetLocation(context.Actor).HomeTown;
                var moved = !currentTown.Id.Equals(gifted.Town.Id, StringComparison.OrdinalIgnoreCase);

                if (moved)
                {
                    RelocateHousehold(
                        context.GameState,
                        context.Actor,
                        gifted.Town,
                        family,
                        economy,
                        locations,
                        career,
                        events);
                }

                events.Publish(new GameEvent
                {
                    Type = "household.parent_house_gift",
                    Year = context.GameState.Year,
                    SubjectId = context.Actor.Id,
                    RelatedPersonIds = [parentHead.Id],
                    Data = new Dictionary<string, string>
                    {
                        ["town"] = gifted.Town.Town,
                        ["moved"] = moved ? "true" : "false",
                        ["text"] = $"{family.GetDisplayName(parentHead)} gave {family.GetDisplayName(context.Actor)} a house in {gifted.Town.Town}."
                    }
                });

                return new GameActionResult(true);
            }
        });
    }

    private static void RelocateHousehold(
        IGameState gameState,
        IPerson head,
        TownInfo destination,
        IFamilyService family,
        IEconomyService economy,
        ILocationService locations,
        ICareerService career,
        IGameEventBus events)
    {
        var origin = locations.GetLocation(head).HomeTown;
        if (origin.Id.Equals(destination.Id, StringComparison.OrdinalIgnoreCase))
            return;

        var memberIds = economy.GetHouseholdMemberIds(head).ToHashSet();
        var employed = gameState.People
            .Where(person => memberIds.Contains(person.Id)
                && person.Tags.Has("state.alive")
                && person.Age >= 18
                && !person.Tags.Has("role.nanny")
                && !person.Tags.Has("role.family_nanny"))
            .Select(person => new { Person = person, Career = career.GetCareer(person) })
            .Where(item => !item.Career.IsRetired && item.Career.IsEmployed && !item.Career.IsSelfEmployed)
            .ToList();

        economy.SetResidenceTown(head, destination);

        foreach (var worker in employed)
        {
            var oldCareer = worker.Career;
            var foundWork = career.RelocateEmployment(worker.Person);
            var newCareer = career.GetCareer(worker.Person);
            events.Publish(new GameEvent
            {
                Type = "career.relocated",
                Year = gameState.Year,
                SubjectId = worker.Person.Id,
                Data = new Dictionary<string, string>
                {
                    ["suppressChronicle"] = "true",
                    ["oldCareerId"] = oldCareer.CareerId ?? string.Empty,
                    ["newCareerId"] = newCareer.CareerId ?? string.Empty,
                    ["text"] = foundWork
                        ? $"After moving to {destination.Town}, {family.GetDisplayName(worker.Person)} established new work as {newCareer.JobTitle}."
                        : $"After moving to {destination.Town}, {family.GetDisplayName(worker.Person)} was unable to find replacement employment."
                }
            });
        }

        events.Publish(new GameEvent
        {
            Type = "household.moved",
            Year = gameState.Year,
            SubjectId = head.Id,
            RelatedPersonIds = memberIds
                .Where(id => id != head.Id)
                .ToList(),
            Data = new Dictionary<string, string>
            {
                ["fromTown"] = origin.Town,
                ["toTown"] = destination.Town,
                ["text"] = $"The {head.Surname} household moved from {origin.Town} to {destination.Town}."
            }
        });
    }
}
