using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed class AssimilationYearSystem : IYearSystem
{
    private const double AnnualAssimilationChance = 0.005;
    private const string PolishNationalityId = "polish";
    public const string PolishNamingTag = "family.polish_naming";

    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly INationalityService _nationalities;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public AssimilationYearSystem(
        IEconomyService economy,
        IFamilyService family,
        INationalityService nationalities,
        IGameRandom random,
        IGameEventBus events)
    {
        _economy = economy;
        _family = family;
        _nationalities = nationalities;
        _random = random;
        _events = events;
    }

    public string Id => "households.assimilation";
    public YearPhase Phase => YearPhase.LifeEvents;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People.Where(person =>
                     person.Tags.Has("state.alive")
                     && !SimulationState.IsInactive(person)
                     && _economy.GetHouseholdId(person) is not null))
        {
            var nationalityId = _nationalities.GetNationality(person);
            if (nationalityId.Equals(
                    PolishNationalityId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_random.NextDouble() >= AnnualAssimilationChance)
                continue;

            var previousDisplayName =
                _nationalities.GetDisplayName(nationalityId);

            _nationalities.SetNationality(
                person,
                PolishNationalityId);
            person.Tags.Add(PolishNamingTag);

            _events.Publish(
                new GameEvent
                {
                    Type = "family.assimilated_polish",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["previousNationality"] = previousDisplayName,
                        ["text"] =
                            $"{_family.GetDisplayName(person)} assimilated into Polish society."
                    }
                });
        }
    }
}
