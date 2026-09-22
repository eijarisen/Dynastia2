using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

public sealed class HeirloomsPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = Require<IGameState>(context, "Game state");
        var economy = Require<IEconomyService>(context, "Economy service");
        var family = Require<IFamilyService>(context, "Family service");
        var random = Require<IGameRandom>(context, "Game random");
        var events = Require<IGameEventBus>(context, "Game event bus");
        var actions = Require<IActionRegistry>(context, "Action registry");
        var justice = context.GetService<IJusticeService>();
        var data = Require<IGameDataService>(context, "Game data service");

        var catalog = HeirloomCatalog.Load(data);
        var eventCatalog = HeirloomEventCatalog.Load(data, catalog);
        var service = new StandardHeirloomService(
            gameState,
            economy,
            family,
            random,
            events,
            catalog);
        var eventGenerator = new HeirloomEventGenerator(
            gameState,
            economy,
            family,
            service,
            random,
            eventCatalog);

        context.AddService<IHeirloomService>(service);
        RegisterSaleAction(actions, economy, service, justice, events);

        var education = context.GetService<IEducationService>();
        var career = context.GetService<ICareerService>();
        var crafts = context.GetService<ICraftService>();
        var systems = context.GetService<IYearSystemRegistry>();

        ArtisticWorkCatalog? artisticWorks = null;
        if (crafts is not null)
        {
            artisticWorks = ArtisticWorkCatalog.Load(
                data,
                catalog,
                crafts.Catalog);

            context.GetService<IHouseholdIncomeProviderRegistry>()?.Register(
                new ArtisticRoyaltyIncomeProvider(
                    gameState,
                    service,
                    artisticWorks.RoyaltyAfterDeathYears));

            systems?.Register(
                new ArtisticWorkYearSystem(
                    family,
                    crafts,
                    service,
                    random,
                    events,
                    artisticWorks));
        }

        HeirloomAchievementGenerator? achievementGenerator = null;
        if (education is not null
            && career is not null
            && crafts is not null
            && systems is not null)
        {
            var achievementCatalog = HeirloomAchievementCatalog.Load(
                data,
                catalog,
                career.GetKnownCareerFamilies(),
                crafts.Catalog);

            achievementGenerator = new HeirloomAchievementGenerator(
                gameState,
                economy,
                family,
                education,
                career,
                service,
                random,
                achievementCatalog,
                artisticWorks);

            systems.Register(
                new HeirloomWealthMilestoneYearSystem(
                    economy,
                    service,
                    random,
                    achievementCatalog));
        }

        var hobbies = context.GetService<IHobbyService>();
        if (hobbies is not null && systems is not null)
        {
            systems.Register(
                new HeirloomHobbyYearSystem(
                    economy,
                    family,
                    hobbies,
                    service,
                    random,
                    eventCatalog));
        }

        events.EventPublished += (_, gameEvent) =>
        {
            if (gameEvent.Type.Equals("life.death", StringComparison.OrdinalIgnoreCase)
                && gameEvent.SubjectId is Guid deceasedId)
            {
                service.ClearInheritanceAssignments(deceasedId);
            }

            achievementGenerator?.Handle(gameEvent);
            eventGenerator.Handle(gameEvent);
        };

        context.Log(achievementGenerator is null
            ? "Heirloom mechanics registered with event, hobby and artistic-work generation."
            : "Heirloom mechanics registered with achievement, wealth, event, hobby, artistic-work and royalty generation.");
    }

    private static void RegisterSaleAction(
        IActionRegistry actions,
        IEconomyService economy,
        IHeirloomService heirlooms,
        IJusticeService? justice,
        IGameEventBus events)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = "heirloom.sell",
                Label = "Sell Heirloom",
                Description = "Select one family heirloom and queue its sale for 80% of its appraised value.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                EvaluateAvailability = context =>
                {
                    if (!context.ActorHasControl
                        || context.Actor.Id != context.Target.Id)
                    {
                        return ActionEvaluationResult.Denied(
                            ActionReasonCodes.NoLongerEligible,
                            "Only the active household heir can sell family property.");
                    }

                    var owned = heirlooms.GetHeirlooms(context.Actor);
                    if (context.Parameters.TryGetValue("heirloomId", out var raw)
                        && Guid.TryParse(raw, out var selectedId))
                    {
                        if (!owned.Any(item => item.Id == selectedId))
                        {
                            return ActionEvaluationResult.Denied(
                                ActionReasonCodes.AssetNoLongerOwned,
                                "The selected heirloom is no longer owned.");
                        }
                    }
                    else if (owned.Count == 0)
                    {
                        return ActionEvaluationResult.Denied(
                            ActionReasonCodes.NoLongerEligible,
                            "The household owns no heirlooms.");
                    }

                    return ActionEvaluationResult.Allowed(
                        economy.GetHouseholdId(context.Actor));
                },
                Execute = context =>
                {
                    if (!context.Parameters.TryGetValue("heirloomId", out var raw)
                        || !Guid.TryParse(raw, out var heirloomId))
                    {
                        return new GameActionResult(
                            false,
                            "No heirloom was selected.",
                            ActionReasonCodes.NoLongerEligible);
                    }

                    var existing = heirlooms.GetHeirlooms(context.Actor)
                        .FirstOrDefault(item => item.Id == heirloomId);
                    if (existing is null)
                    {
                        return new GameActionResult(
                            false,
                            "The selected heirloom is no longer owned.",
                            ActionReasonCodes.AssetNoLongerOwned);
                    }

                    var sold = heirlooms.Take(context.Actor, heirloomId);
                    if (sold is null)
                    {
                        return new GameActionResult(
                            false,
                            "The selected heirloom is no longer owned.",
                            ActionReasonCodes.AssetNoLongerOwned);
                    }

                    if (sold.IsStolen && justice is not null)
                    {
                        var detectionChance = justice.GetStolenHeirloomSaleDetectionChance(context.Actor);
                        if (context.Random.NextDouble() < detectionChance)
                        {
                            var originalSentence = context.Random.NextInt(2, 5);
                            var finalSentence = justice.ConvictKnownOffense(
                                context.Actor,
                                originalSentence,
                                "selling_stolen_property",
                                "selling stolen property",
                                $"Caught trying to sell stolen heirloom: {sold.DisplayName}.");

                            events.Publish(
                                new GameEvent
                                {
                                    Type = "justice.stolen_heirloom_sale_caught",
                                    Year = context.GameState.Year,
                                    SubjectId = context.Actor.Id,
                                    Data = new Dictionary<string, string>
                                    {
                                        ["heirloomId"] = sold.Id.ToString(),
                                        ["item"] = sold.DisplayName,
                                        ["detectionChance"] = detectionChance.ToString(CultureInfo.InvariantCulture),
                                        ["sentence"] = finalSentence.ToString(CultureInfo.InvariantCulture),
                                        ["familyNews"] = "true",
                                        ["text"] = $"{context.Actor.Name} was caught trying to sell {sold.DisplayName}; the item was confiscated."
                                    }
                                });

                            return new GameActionResult(true);
                        }
                    }

                    var saleValue = heirlooms.GetSaleValue(sold);
                    economy.ChangeWealth(context.Actor, saleValue);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "heirloom.sold",
                            Year = context.GameState.Year,
                            SubjectId = context.Actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["heirloomId"] = sold.Id.ToString(),
                                ["item"] = sold.DisplayName,
                                ["saleValue"] = saleValue.ToString(CultureInfo.InvariantCulture),
                                ["familyNews"] = "true",
                                ["text"] = $"The household sold {sold.DisplayName} for {saleValue:N0} zł."
                            }
                        });

                    return new GameActionResult(true);
                }
            });
    }

    private static T Require<T>(IGamePluginContext context, string name)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{name} is unavailable.");
}
