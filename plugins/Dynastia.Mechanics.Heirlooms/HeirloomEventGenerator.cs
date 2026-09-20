using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal sealed class HeirloomEventGenerator
{
    private static readonly HashSet<string> DirectHistoricalTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "historical.household_impact",
        "historical.relocation",
        "historical.external_departure"
    };

    private readonly IGameState _gameState;
    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly IHeirloomService _heirlooms;
    private readonly IGameRandom _random;
    private readonly HeirloomEventCatalog _catalog;

    public HeirloomEventGenerator(
        IGameState gameState,
        IEconomyService economy,
        IFamilyService family,
        IHeirloomService heirlooms,
        IGameRandom random,
        HeirloomEventCatalog catalog)
    {
        _gameState = gameState;
        _economy = economy;
        _family = family;
        _heirlooms = heirlooms;
        _random = random;
        _catalog = catalog;
    }

    public void Handle(GameEvent gameEvent)
    {
        if (gameEvent.Type.Equals("historical.milestone", StringComparison.OrdinalIgnoreCase))
            return;

        if (DirectHistoricalTypes.Contains(gameEvent.Type))
        {
            HandleHistorical(gameEvent);
            return;
        }

        if (_catalog.Rare.TryGetValue(gameEvent.Type, out var rareRule))
        {
            HandleRare(gameEvent, rareRule);
            return;
        }

        if (gameEvent.Type.Equals("justice.crime_uncaught", StringComparison.OrdinalIgnoreCase))
            HandleCrime(gameEvent);
    }

    private void HandleHistorical(GameEvent gameEvent)
    {
        if (gameEvent.SubjectId is not Guid subjectId
            || !gameEvent.Data.TryGetValue("eventId", out var eventId)
            || !_catalog.Historical.TryGetValue(eventId, out var rule))
        {
            return;
        }

        var person = FindPerson(subjectId);
        if (person is null)
            return;

        var householdId = _economy.GetHouseholdId(person);
        if (householdId is null)
            return;

        var ledger = GetOrCreateHouseholdLedger(person, householdId.Value);
        var triggerId = $"historical:{householdId.Value}:{eventId}";
        if (!ledger.RolledHistoricalEventIds.Add(triggerId))
            return;

        if (!_random.Chance(rule.Chance))
            return;

        var eventName = _catalog.HistoricalNames.GetValueOrDefault(eventId, eventId);
        _heirlooms.Create(
            person,
            new HeirloomCreationRequest(
                Choose(rule.TemplateIds),
                person.Id,
                "historical",
                triggerId,
                $"Preserved after {eventName} directly affected the household.",
                NewsTokens: new Dictionary<string, string>
                {
                    ["Event"] = eventName
                }));
    }

    private void HandleRare(GameEvent gameEvent, RareEventHeirloomRule rule)
    {
        if (gameEvent.SubjectId is not Guid subjectId)
            return;
        var person = FindPerson(subjectId);
        if (person is null || _economy.GetHouseholdId(person) is null)
            return;

        var triggerId = $"rare:{person.Id}:{gameEvent.Type}:{gameEvent.Year}";
        if (!TryMarkProcessed(person, triggerId))
            return;

        if (!_random.Chance(rule.Chance))
            return;

        var displayName = gameEvent.Data.TryGetValue("displayName", out var suppliedName)
            && !string.IsNullOrWhiteSpace(suppliedName)
                ? suppliedName
                : _catalog.RareNames.GetValueOrDefault(gameEvent.Type, gameEvent.Type);

        _heirlooms.Create(
            person,
            new HeirloomCreationRequest(
                Choose(rule.TemplateIds),
                person.Id,
                "rare_event",
                triggerId,
                $"Preserved after {_family.GetDisplayName(person)} experienced {displayName}."));
    }

    private void HandleCrime(GameEvent gameEvent)
    {
        if (gameEvent.SubjectId is not Guid subjectId
            || !gameEvent.Data.TryGetValue("success", out var successRaw)
            || !bool.TryParse(successRaw, out var success)
            || !success
            || !gameEvent.Data.TryGetValue("crimeId", out var crimeId)
            || !_catalog.Crimes.TryGetValue(crimeId, out var rule))
        {
            return;
        }

        var person = FindPerson(subjectId);
        if (person is null || _economy.GetHouseholdId(person) is null)
            return;

        var triggerId = $"crime:{person.Id}:{gameEvent.Year}:{crimeId}";
        if (!TryMarkProcessed(person, triggerId))
            return;

        if (!_random.Chance(rule.Chance))
            return;

        var crimeName = gameEvent.Data.TryGetValue("crime", out var name)
            && !string.IsNullOrWhiteSpace(name)
                ? name
                : crimeId.Replace('_', ' ');

        _heirlooms.Create(
            person,
            new HeirloomCreationRequest(
                Choose(rule.TemplateIds),
                person.Id,
                "crime",
                triggerId,
                $"Stolen during {crimeName} committed by {_family.GetDisplayName(person)} without arrest.",
                IsStolen: true,
                NewsTokens: new Dictionary<string, string>
                {
                    ["Crime"] = crimeName
                }));
    }

    private IPerson? FindPerson(Guid id) =>
        _gameState.People.FirstOrDefault(person => person.Id == id);

    private string Choose(IReadOnlyList<string> values) =>
        values[_random.NextInt(0, values.Count - 1)];

    private bool TryMarkProcessed(IPerson person, string triggerId)
    {
        var component = person.Components.Get<HeirloomPersonTriggerComponent>();
        if (component is null)
        {
            component = new HeirloomPersonTriggerComponent();
            person.Components.Set(component);
        }
        return component.CompletedTriggerIds.Add(triggerId);
    }

    private HeirloomHouseholdMilestoneComponent GetOrCreateHouseholdLedger(
        IPerson representative,
        Guid householdId)
    {
        var existing = _gameState.People
            .Select(person => person.Components.Get<HeirloomHouseholdMilestoneComponent>())
            .FirstOrDefault(component => component?.HouseholdId == householdId);
        if (existing is not null)
            return existing;

        var created = new HeirloomHouseholdMilestoneComponent
        {
            HouseholdId = householdId
        };
        representative.Components.Set(created);
        return created;
    }
}
