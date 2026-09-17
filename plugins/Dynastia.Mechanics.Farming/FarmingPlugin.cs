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
        var data = context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException("Game data service is unavailable.");

        var service = new StandardFarmingService(
            gameState,
            economy,
            career,
            random,
            events,
            FarmingEraSchedule.Load(data));

        context.AddService<IFarmingService>(service);
        householdIncome.Register(service);

        RegisterActions(
            actions,
            economy,
            service,
            events);

        context.Log("Farming mechanics registered.");
    }

    private static void RegisterActions(
        IActionRegistry actions,
        IEconomyService economy,
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
                                ["farmlandId"] = parcel.Id.ToString(),
                                ["text"] =
                                    $"The household purchased farmland near {town.Town}."
                            }
                        });

                    return new GameActionResult(true);
                }
            });

        actions.Register(
            new GameActionDefinition
            {
                Id = "farming.sell_farmland",
                Label = $"Sell Farmland ({farming.SalePrice:N0} zł)",
                Description =
                    $"Queue the sale of one farmland parcel for {farming.SalePrice:N0} zł. Local land is sold first; otherwise the oldest owned parcel is sold.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = context =>
                    context.Actor.Id == context.Target.Id
                    && economy.GetFarmland(context.Actor).Count > 0,
                Execute = context =>
                {
                    var residence = economy.GetResidenceTown(context.Actor);
                    var parcel = economy.GetFarmland(context.Actor)
                        .OrderBy(asset =>
                            asset.Town.Id.Equals(
                                residence.Id,
                                StringComparison.OrdinalIgnoreCase)
                                ? 0
                                : 1)
                        .ThenBy(asset => asset.AcquiredYear)
                        .ThenBy(asset => asset.Id)
                        .FirstOrDefault();

                    if (parcel is null)
                        return new GameActionResult(false, "No farmland remains to sell.");

                    var sold = economy.TakeFarmland(context.Actor, parcel.Id);
                    if (sold is null)
                        return new GameActionResult(false, "The selected farmland is no longer owned.");

                    economy.ChangeWealth(context.Actor, farming.SalePrice);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "farmland.sold",
                            Year = context.GameState.Year,
                            SubjectId = context.Actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["amount"] = farming.SalePrice.ToString(),
                                ["town"] = sold.Town.Town,
                                ["farmlandId"] = sold.Id.ToString(),
                                ["text"] =
                                    $"The household sold farmland near {sold.Town.Town} for {farming.SalePrice:N0} zł."
                            }
                        });

                    return new GameActionResult(true);
                }
            });
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
