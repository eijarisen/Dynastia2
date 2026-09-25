using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public sealed class RareEventsPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
        T Require<T>(string name) where T : class =>
            context.GetService<T>() ?? throw new InvalidOperationException($"{name} is unavailable.");

        var gameState = Require<IGameState>("Game state");
        var people = context.GetService<IPersonLookup>()
            ?? gameState as IPersonLookup;
        var data = Require<IGameDataService>("Game data service");
        var family = Require<IFamilyService>("Family service");
        var health = Require<IHealthService>("Health service");
        var economy = Require<IEconomyService>("Economy service");
        var career = Require<ICareerService>("Career service");
        var justice = Require<IJusticeService>("Justice service");
        var households = Require<IHouseholdService>("Household service");
        var stats = Require<IStatsService>("Stats service");
        var personality = Require<IPersonalityService>("Personality service");
        var stress = Require<IStressService>("Stress service");
        var education = Require<IEducationService>("Education service");
        var localOpportunities = Require<ILocalCareerOpportunityService>("Local opportunity service");
        var random = Require<IGameRandom>("Random service");
        var calendar = Require<IGameCalendar>("Calendar service");
        var events = Require<IGameEventBus>("Event bus");
        var systems = Require<IYearSystemRegistry>("Year system registry");
        var contextWeights = Require<IContextWeightService>("Context-weight service");

        var catalog = RareEventCatalog.Load(data);
        var poolRules = RareEventPoolRulesCatalog.Load(data);
        var simpleEffects = RareEventSimpleEffectCatalog.Load(data, catalog);
        var epidemics = RareEventEpidemicCatalog.Load(data);
        var knownCareerFamilies = career.GetKnownCareerFamilies().ToHashSet(StringComparer.OrdinalIgnoreCase);
        RareEventDataValidation.Validate(data, catalog, epidemics, knownCareerFamilies);
        var careerFamilyWeights = RareEventCareerFamilyWeightCatalog.Load(data, catalog, knownCareerFamilies);
        var variants = RareEventVariantCatalog.Load(data, catalog);
        var contextCatalog = contextWeights.LoadCatalog(
            "RareEvents/rare_event_context_weights.csv",
            catalog.Events.Select(item => item.EventId));

        var recent = new RecentLifeEventTracker(gameState, people, family, events);
        var death = new RareEventDeathService(family, health, economy, random, calendar, events);

        systems.Register(new RecentLifeEventCleanupYearSystem(recent));
        systems.Register(new RareEventYearSystem(
            family,
            health,
            economy,
            career,
            justice,
            households,
            stats,
            personality,
            stress,
            education,
            localOpportunities,
            random,
            events,
            recent,
            death,
            catalog,
            poolRules,
            simpleEffects,
            epidemics,
            careerFamilyWeights,
            variants,
            contextCatalog,
            () => context.GetService<IFarmingService>(),
            () => context.GetService<ICraftService>()));

        context.Log($"Rare life events registered: {catalog.Events.Count} catalog events with fixed pool gates.");
    }
}
