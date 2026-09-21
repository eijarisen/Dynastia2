using Dynastia.Contracts;

namespace Dynastia.Mechanics.Farming;

public sealed class FarmingPlugin : IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>()
            ?? throw new InvalidOperationException("Game state is unavailable.");
        var economy = context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException("Economy service is unavailable.");
        var family = context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException("Family service is unavailable.");
        var career = context.GetService<ICareerService>()
            ?? throw new InvalidOperationException("Career service is unavailable.");
        var random = context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException("Random service is unavailable.");
        var events = context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException("Game event bus is unavailable.");
        var actions = context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException("Action registry is unavailable.");
        var householdIncome = context.GetService<IHouseholdIncomeProviderRegistry>()
            ?? throw new InvalidOperationException("Household income registry is unavailable.");
        var prosperity = context.GetService<ITownProsperityService>()
            ?? throw new InvalidOperationException("Town prosperity service is unavailable.");
        var economicStrength = context.GetService<ILocalEconomicStrengthService>()
            ?? throw new InvalidOperationException("Local economic-strength service is unavailable.");
        var workCapacity = context.GetService<IWorkCapacityService>()
            ?? throw new InvalidOperationException("Work-capacity service is unavailable.");
        var data = context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException("Game data service is unavailable.");

        var service = new StandardFarmingService(
            gameState,
            economy,
            career,
            prosperity,
            economicStrength,
            workCapacity,
            random,
            events,
            FarmingEraSchedule.Load(data),
            FarmingFlavorCatalog.Load(data));

        context.AddService<IFarmingService>(service);
        householdIncome.Register(service);

        RegisterActions(
            actions,
            economy,
            family,
            service,
            events);

        context.Log("Farming mechanics registered.");
    }

    private static void RegisterActions(
        IActionRegistry actions,
        IEconomyService economy,
        IFamilyService family,
        IFarmingService farming,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "farming.buy_farmland",
                Label = $"Buy Farmland ({farming.PurchasePrice:N0} zł)",
                Description =
                    $"Queue the purchase of one farmland parcel in the household's current town for {farming.PurchasePrice:N0} zł. The household must own a house in that town.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = context =>
                    context.Actor.Id == context.Target.Id
                    && economy.GetHousehold(context.Actor) is not null
                    && economy.CanAfford(context.Actor, farming.PurchasePrice)
                    && OwnsHouseInResidenceTown(economy, context.Actor),
                Execute = context =>
                {
                    var finance = economy.GetHousehold(context.Actor);
                    if (finance is null || !economy.CanAfford(context.Actor, farming.PurchasePrice))
                    {
                        return new GameActionResult(
                            false,
                            "The household can no longer afford farmland.");
                    }

                    if (!OwnsHouseInResidenceTown(economy, context.Actor))
                    {
                        return new GameActionResult(
                            false,
                            "The household must own a house in its current town before buying farmland there.");
                    }

                    var town = economy.GetResidenceTown(context.Actor);
                    economy.ChangeWealth(context.Actor, -farming.PurchasePrice);
                    var parcel = economy.AddFarmland(
                        context.Actor,
                        town,
                        context.GameState.Year,
                        "purchase");
                    var flavored = farming.AssignNewFarmlandType(
                        context.Actor,
                        parcel.Id) ?? parcel;

                    events.Publish(
                        new GameEvent
                        {
                            Type = "farmland.bought",
                            Year = context.GameState.Year,
                            SubjectId = context.Actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["amount"] = farming.PurchasePrice.ToString(),
                                ["town"] = town.Town,
                                ["farmlandId"] = flavored.Id.ToString(),
                                ["farmTypeId"] = flavored.FarmTypeId,
                                ["text"] =
                                    $"{family.GetDisplayName(context.Actor)} purchased a {flavored.FarmTypeDisplayName} near {town.Town}."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "farming.sell_farmland",
                Label = $"Sell Farmland ({farming.SalePrice:N0}+ zł)",
                Description =
                    $"Queue the sale of a selected farmland parcel for {farming.SalePrice:N0} zł, plus {farming.LivestockSalePrice:N0} zł when it has Livestock.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = context =>
                    context.Actor.Id == context.Target.Id
                    && ResolveFarmlandForAction(context, economy, allowLegacyFallback: true) is not null,
                Execute = context =>
                {
                    var parcel = ResolveFarmlandForAction(
                        context,
                        economy,
                        allowLegacyFallback: true);
                    if (parcel is null)
                        return new GameActionResult(false, "The selected farmland is no longer owned.");

                    var flavored = farming.EnsureFarmlandFlavor(
                        context.Actor,
                        parcel.Id) ?? parcel;
                    var saleValue = farming.GetFarmlandSaleValue(flavored);
                    var sold = economy.TakeFarmland(context.Actor, parcel.Id);
                    if (sold is null)
                        return new GameActionResult(false, "The selected farmland is no longer owned.");

                    economy.ChangeWealth(context.Actor, saleValue);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "farmland.sold",
                            Year = context.GameState.Year,
                            SubjectId = context.Actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["amount"] = saleValue.ToString(),
                                ["town"] = flavored.Town.Town,
                                ["farmlandId"] = flavored.Id.ToString(),
                                ["farmTypeId"] = flavored.FarmTypeId,
                                ["livestockTypeId"] = flavored.LivestockTypeId ?? string.Empty,
                                ["text"] =
                                    $"The household sold its {flavored.FarmTypeDisplayName} near {flavored.Town.Town} for {saleValue:N0} zł."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "farming.add_livestock",
                Label = $"Add Livestock ({farming.LivestockPurchasePrice:N0} zł)",
                Description =
                    $"Queue one Livestock upgrade for a selected farmland parcel in the current town for {farming.LivestockPurchasePrice:N0} zł. The animal type is chosen automatically from the local historical pool.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = context =>
                    context.Actor.Id == context.Target.Id
                    && economy.CanAfford(context.Actor, farming.LivestockPurchasePrice)
                    && HasEligibleLivestockParcel(context, economy),
                Execute = context =>
                {
                    if (!economy.CanAfford(context.Actor, farming.LivestockPurchasePrice))
                    {
                        return new GameActionResult(
                            false,
                            "The household can no longer afford Livestock.");
                    }

                    if (!context.Parameters.TryGetValue("farmlandId", out var rawId)
                        || !Guid.TryParse(rawId, out var farmlandId))
                    {
                        return new GameActionResult(false, "No farmland parcel was selected.");
                    }

                    var residence = economy.GetResidenceTown(context.Actor);
                    var parcel = economy.GetFarmland(context.Actor)
                        .FirstOrDefault(asset => asset.Id == farmlandId);
                    if (parcel is null)
                        return new GameActionResult(false, "The selected farmland is no longer owned.");
                    if (!parcel.Town.Id.Equals(residence.Id, StringComparison.OrdinalIgnoreCase))
                        return new GameActionResult(false, "Livestock can only be added to farmland in the household's current town.");
                    if (!string.IsNullOrWhiteSpace(parcel.LivestockTypeId))
                        return new GameActionResult(false, "This farmland parcel already has Livestock.");

                    var options = farming.GetAvailableLivestockOptions(
                        parcel.Town,
                        context.GameState.Year);
                    if (options.Count == 0)
                        return new GameActionResult(false, "No locally plausible Livestock is available this year.");

                    economy.ChangeWealth(context.Actor, -farming.LivestockPurchasePrice);
                    var updated = farming.AddLivestock(
                        context.Actor,
                        parcel.Id,
                        context.GameState.Year);
                    if (updated is null)
                    {
                        economy.ChangeWealth(context.Actor, farming.LivestockPurchasePrice);
                        return new GameActionResult(false, "Livestock could not be added to the selected farmland.");
                    }

                    events.Publish(
                        new GameEvent
                        {
                            Type = "farming.livestock_added",
                            Year = context.GameState.Year,
                            SubjectId = context.Actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["amount"] = farming.LivestockPurchasePrice.ToString(),
                                ["town"] = updated.Town.Town,
                                ["farmlandId"] = updated.Id.ToString(),
                                ["farmTypeId"] = updated.FarmTypeId,
                                ["livestockTypeId"] = updated.LivestockTypeId ?? string.Empty,
                                ["text"] =
                                    $"{updated.LivestockEmoji} The family added {updated.LivestockDisplayName} to its {updated.FarmTypeDisplayName} near {updated.Town.Town}."
                            }
                        });

                    return new GameActionResult(true);
                }
            });
    }

    private static bool HasEligibleLivestockParcel(
        GameActionContext context,
        IEconomyService economy)
    {
        var residence = economy.GetResidenceTown(context.Actor);
        var farmland = economy.GetFarmland(context.Actor);

        if (context.Parameters.TryGetValue("farmlandId", out var rawId))
        {
            if (!Guid.TryParse(rawId, out var farmlandId))
                return false;

            return farmland.Any(parcel =>
                parcel.Id == farmlandId
                && parcel.Town.Id.Equals(residence.Id, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(parcel.LivestockTypeId));
        }

        return farmland.Any(parcel =>
            parcel.Town.Id.Equals(residence.Id, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(parcel.LivestockTypeId));
    }

    private static FarmlandAssetInfo? ResolveFarmlandForAction(
        GameActionContext context,
        IEconomyService economy,
        bool allowLegacyFallback)
    {
        var farmland = economy.GetFarmland(context.Actor);
        if (context.Parameters.TryGetValue("farmlandId", out var rawId))
        {
            return Guid.TryParse(rawId, out var farmlandId)
                ? farmland.FirstOrDefault(asset => asset.Id == farmlandId)
                : null;
        }

        if (!allowLegacyFallback)
            return null;

        var residence = economy.GetResidenceTown(context.Actor);
        return farmland
            .OrderBy(asset =>
                asset.Town.Id.Equals(
                    residence.Id,
                    StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
            .ThenBy(asset => asset.AcquiredYear)
            .ThenBy(asset => asset.Id)
            .FirstOrDefault();
    }

    private static bool OwnsHouseInResidenceTown(
        IEconomyService economy,
        IPerson actor)
    {
        var residence = economy.GetResidenceTown(actor);
        return economy.GetHouses(actor).Any(house =>
            house.Town.Id.Equals(
                residence.Id,
                StringComparison.OrdinalIgnoreCase));
    }
}
