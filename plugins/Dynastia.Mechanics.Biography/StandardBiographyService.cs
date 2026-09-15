using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class StandardBiographyService :
    IBiographyService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IHouseholdService _households;
    private readonly AboutTextBuilder _about;

    private readonly Dictionary<
        Guid,
        List<BiographyEntry>>
        _entries = [];

    public StandardBiographyService(
        IGameState gameState,
        IFamilyService family,
        IStatsService stats,
        ILocationService locations,
        IHouseholdService households,
        IGameEventBus events)
    {
        _gameState = gameState;
        _family = family;
        _households = households;

        _about =
            new AboutTextBuilder(
                gameState,
                family,
                stats,
                locations);

        events.EventPublished +=
            OnEventPublished;
    }

    public string GetAbout(
        IPerson person)
    {
        return _about.Build(
            person);
    }

    public IReadOnlyList<BiographyEntry>
        GetBiography(
            IPerson person)
    {
        EnsureGeneratedAdultLifeMilestones(
            person);

        if (!_entries.TryGetValue(
            person.Id,
            out var entries))
        {
            return [];
        }

        return entries
            .Where(entry =>
                !IsMinorRelativeIllnessEntry(
                    entry))
            .Select(
                (entry, index) =>
                    new
                    {
                        Entry = entry,
                        Index = index
                    })
            .OrderByDescending(
                item =>
                    item.Entry.Year)
            .ThenByDescending(
                item =>
                    item.Index)
            .Select(
                item =>
                    item.Entry)
            .ToList();
    }

    public IReadOnlyDictionary<
        Guid,
        IReadOnlyList<BiographyEntry>>
        ExportBiographyState()
    {
        foreach (var person in
            _gameState.People)
        {
            EnsureGeneratedAdultLifeMilestones(
                person);
        }

        return _entries.ToDictionary(
            pair =>
                pair.Key,
            pair =>
                (IReadOnlyList<BiographyEntry>)
                    pair.Value
                        .Where(entry =>
                            !IsMinorRelativeIllnessEntry(
                                entry))
                        .ToList());
    }

    public void RestoreBiographyState(
        IReadOnlyDictionary<
            Guid,
            IReadOnlyList<BiographyEntry>>
            entries)
    {
        ArgumentNullException.ThrowIfNull(
            entries);

        _entries.Clear();

        foreach (var pair in
            entries)
        {
            _entries[pair.Key] =
                pair.Value
                    .Where(entry =>
                        !IsMinorRelativeIllnessEntry(
                            entry))
                    .ToList();
        }
    }

    private void SeedFounderBiography(
        GameEvent gameEvent)
    {
        var founder =
            FindPerson(
                gameEvent.SubjectId);

        if (founder is null)
            return;

        var father =
            _family.GetFather(
                founder);

        var mother =
            _family.GetMother(
                founder);

        if (founder.BirthDate
            is GameDate birthDate)
        {
            var parentText =
                father is not null
                && mother is not null
                    ? $" to {_family.GetDisplayName(father)} " +
                      $"and {_family.GetDisplayName(mother)}"
                    : string.Empty;

            AddEntry(
                founder,
                birthDate.Year,
                $"👶 Was born{parentText}.");
        }

        if (father?.DeathDate
                is GameDate fatherDeath
            && mother?.DeathDate
                is GameDate motherDeath)
        {
            var orphanYear =
                Math.Max(
                    fatherDeath.Year,
                    motherDeath.Year);

            var orphanAge =
                founder.BirthDate
                    is GameDate founderBirth
                    ? orphanYear
                      - founderBirth.Year
                    : 17;

            AddEntry(
                founder,
                orphanYear,
                $"💀 Became an orphan at age " +
                $"{orphanAge} after both parents died.");
        }

        AddEntry(
            founder,
            gameEvent.Year,
            "🧑 Became an adult.");
    }

    private int GetCompletedBiographyYear(
        GameEvent gameEvent) =>
        Math.Max(
            _gameState.StartYear,
            gameEvent.Year - 1);

    private void AddEntry(
        IPerson person,
        int year,
        string message)
    {
        if (!_entries.TryGetValue(
            person.Id,
            out var entries))
        {
            entries = [];
            _entries[person.Id] =
                entries;
        }

        entries.Add(
            new BiographyEntry(
                year,
                message));
    }

    private void AddEntryIfMissing(
        IPerson person,
        int year,
        string message)
    {
        if (_entries.TryGetValue(
                person.Id,
                out var entries)
            && entries.Any(entry =>
                entry.Year == year
                && entry.Message.Equals(
                    message,
                    StringComparison.Ordinal)))
        {
            return;
        }

        AddEntry(
            person,
            year,
            message);
    }

    private IPerson? FindPerson(
        Guid? id)
    {
        if (id is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == id.Value);
    }

    private IPerson? FindRelatedPerson(
        GameEvent gameEvent,
        int index)
    {
        if (index < 0
            || index >= gameEvent
                .RelatedPersonIds
                .Count)
        {
            return null;
        }

        return FindPerson(
            gameEvent.RelatedPersonIds[
                index]);
    }

}
