using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem : IYearSystem
{
    private const double CatastrophicFireDeathChance = 0.05;
    private const double WorkplaceDeathChance = 0.05;
    private const double TrafficDeathChance = 0.05;
    private const double LightningDeathChance = 0.35;

    private readonly IFamilyService _family;
    private readonly IHealthService _health;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;
    private readonly IJusticeService _justice;
    private readonly IHouseholdService _households;
    private readonly IStatsService _stats;
    private readonly IPersonalityService _personality;
    private readonly IStressService _stress;
    private readonly IEducationService _education;
    private readonly ILocalCareerOpportunityService _localOpportunities;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly RecentLifeEventTracker _recent;
    private readonly RareEventDeathService _death;
    private readonly RareEventCatalog _catalog;
    private readonly RareEventPoolRulesCatalog _poolRules;
    private readonly RareEventSimpleEffectCatalog _simpleEffects;
    private readonly RareEventEpidemicCatalog _epidemics;
    private readonly RareEventCareerFamilyWeightCatalog _careerFamilyWeights;
    private readonly RareEventVariantCatalog _variants;
    private readonly IContextWeightCatalog _contextWeights;
    private readonly Func<IFarmingService?> _farmingResolver;
    private readonly Func<ICraftService?> _craftResolver;

    public RareEventYearSystem(
        IFamilyService family,
        IHealthService health,
        IEconomyService economy,
        ICareerService career,
        IJusticeService justice,
        IHouseholdService households,
        IStatsService stats,
        IPersonalityService personality,
        IStressService stress,
        IEducationService education,
        ILocalCareerOpportunityService localOpportunities,
        IGameRandom random,
        IGameEventBus events,
        RecentLifeEventTracker recent,
        RareEventDeathService death,
        RareEventCatalog catalog,
        RareEventPoolRulesCatalog poolRules,
        RareEventSimpleEffectCatalog simpleEffects,
        RareEventEpidemicCatalog epidemics,
        RareEventCareerFamilyWeightCatalog careerFamilyWeights,
        RareEventVariantCatalog variants,
        IContextWeightCatalog contextWeights,
        Func<IFarmingService?> farmingResolver,
        Func<ICraftService?> craftResolver)
    {
        _family = family;
        _health = health;
        _economy = economy;
        _career = career;
        _justice = justice;
        _households = households;
        _stats = stats;
        _personality = personality;
        _stress = stress;
        _education = education;
        _localOpportunities = localOpportunities;
        _random = random;
        _events = events;
        _recent = recent;
        _death = death;
        _catalog = catalog;
        _poolRules = poolRules;
        _simpleEffects = simpleEffects;
        _epidemics = epidemics;
        _careerFamilyWeights = careerFamilyWeights;
        _variants = variants;
        _contextWeights = contextWeights;
        _farmingResolver = farmingResolver;
        _craftResolver = craftResolver;
    }

    public string Id => "rare_events.annual";
    public YearPhase Phase => YearPhase.Death;
    public IReadOnlyCollection<string> Before => ["mortality.natural_death"];
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        ProcessHouseholdEvents(gameState);

        var living = gameState.People
            .Where(person => person.Tags.Has("state.alive") && !SimulationState.IsInactive(person))
            .ToList();

        foreach (var person in living)
        {
            if (!person.Tags.Has("state.alive") || SimulationState.IsInactive(person))
                continue;

            ProcessPersonalEvent(gameState, person);
            if (person.Tags.Has("state.alive"))
                ProcessSpecialEvents(gameState, person);
        }
    }
}
