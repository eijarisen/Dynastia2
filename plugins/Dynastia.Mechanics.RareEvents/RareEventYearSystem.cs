using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem :
    IYearSystem
{
    // Household probabilities are annual per eligible household.
    private const double HouseFireChance =
        0.00035;

    private const double BurglaryChance =
        0.00060;

    private const double StormFloodChance =
        0.00025;

    private const double StructuralAccidentChance =
        0.00010;

    // Personal probabilities are annual per eligible person.
    private const double AssaultChance =
        0.00020;

    private const double MuggingChance =
        0.00015;

    private const double WorkplaceAccidentChance =
        0.00015;

    private const double TrafficAccidentChance =
        0.00012;

    private const double LightningStrikeChance =
        0.000003;

    private const double SeriousFallChance =
        0.00010;

    private const double LotteryChance =
        0.00002;

    private const double DistantInheritanceChance =
        0.00004;

    private const double FraudChance =
        0.00012;

    private const double FoundPropertyChance =
        0.00004;

    private const double WrongfulArrestChance =
        0.00001;

    private const double BaseSuicideChance =
        0.000005;

    private const double MaximumSuicideChance =
        0.0004;

    // The design calls these "small" / "substantial" chances
    // without fixing exact values.
    private const double CatastrophicFireDeathChance =
        0.05;

    private const double WorkplaceDeathChance =
        0.05;

    private const double TrafficDeathChance =
        0.05;

    private const double LightningDeathChance =
        0.35;

    private readonly IFamilyService _family;
    private readonly IHealthService _health;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;
    private readonly IJusticeService _justice;
    private readonly IHouseholdService _households;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly RecentLifeEventTracker _recent;
    private readonly RareEventDeathService _death;

    public RareEventYearSystem(
        IFamilyService family,
        IHealthService health,
        IEconomyService economy,
        ICareerService career,
        IJusticeService justice,
        IHouseholdService households,
        IGameRandom random,
        IGameEventBus events,
        RecentLifeEventTracker recent,
        RareEventDeathService death)
    {
        _family = family;
        _health = health;
        _economy = economy;
        _career = career;
        _justice = justice;
        _households = households;
        _random = random;
        _events = events;
        _recent = recent;
        _death = death;
    }

    public string Id =>
        "rare_events.annual";

    public YearPhase Phase =>
        YearPhase.Death;

    public IReadOnlyCollection<string> Before =>
        ["mortality.natural_death"];

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        ProcessHouseholdEvents(
            gameState);

        // Rebuild the living list after household events because
        // catastrophic fires can kill an occupant.
        var living =
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && !SimulationState.IsInactive(
                            person))
                .ToList();

        foreach (var person in
            living)
        {
            if (person.Tags.Has(
                    "state.dead")
                || SimulationState.IsInactive(
                    person))
            {
                continue;
            }

            ProcessPersonalEvent(
                gameState,
                person);
        }
    }

}
