namespace Dynastia.App.Genealogy.Host;

using Dynastia.Contracts;
using Dynastia.StandardUI.Genealogy.Contracts;
using Dynastia.StandardUI.Genealogy.Models;

/// <summary>
/// Exact adapter from the current Dynastia.Contracts family/event model
/// to the read-only genealogy projection requested by the uploaded UI.
/// </summary>
public sealed class GameGenealogyDataSource :
    IGenealogyDataSource
{
    private static readonly HashSet<string>
        TopologyEventTypes =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                "game.started",
                "life.birth",
                "relationship.married",
                "relationship.partnered",
                "relationship.remarried",
                "relationship.divorce",
                "relationship.prison_divorce",
                "relationship.affair"
            };

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;

    private long _topologyVersion;
    private long _visualVersion;

    public GameGenealogyDataSource(
        IGameState gameState,
        IFamilyService family,
        IGameEventBus events)
    {
        _gameState =
            gameState;

        _family =
            family;

        events.EventPublished +=
            OnEventPublished;
    }

    public event EventHandler?
        TopologyChanged;

    public event EventHandler?
        VisualStateChanged;

    public GenealogySnapshot GetSnapshot()
    {
        var includedIds =
            GetIncludedPersonIds();

        var people =
            _gameState.People
                .Where(
                    person =>
                        includedIds.Contains(
                            person.Id))
                .Select(
                    CreateRecord)
                .ToList();

        var founder =
            _gameState.People
                .FirstOrDefault(
                    person =>
                        _family.GetGeneration(
                            person) == 1
                        && _family.IsMaleLineage(
                            person));

        return new GenealogySnapshot(
            people,
            founder?.Id,
            _topologyVersion,
            _visualVersion);
    }

    /// <summary>
    /// Save/Load restores events silently, so the host calls this once
    /// after a successful load.
    /// </summary>
    public void NotifyHostReset()
    {
        _topologyVersion++;
        _visualVersion++;

        TopologyChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private HashSet<Guid> GetIncludedPersonIds()
    {
        var result =
            new HashSet<Guid>();

        foreach (var person in
            _gameState.People)
        {
            if (!_family.IsBloodline(
                person))
            {
                continue;
            }

            result.Add(
                person.Id);

            var currentSpouse =
                _family.GetSpouse(
                    person);

            if (currentSpouse is not null)
            {
                result.Add(
                    currentSpouse.Id);
            }

            foreach (var marriage in
                _family.GetRelationshipHistory(
                    person))
            {
                result.Add(
                    marriage.SpouseId);
            }
        }

        return result;
    }

    private GenealogyPersonRecord CreateRecord(
        IPerson person)
    {
        var parents =
            new List<Guid>(
                capacity: 2);

        var father =
            _family.GetFather(
                person);

        var mother =
            _family.GetMother(
                person);

        if (father is not null)
        {
            parents.Add(
                father.Id);
        }

        if (mother is not null)
        {
            parents.Add(
                mother.Id);
        }

        var history =
            _family.GetRelationshipHistory(
                person)
                .Select(
                    marriage =>
                        new GenealogyMarriageRecord(
                            marriage.SpouseId,
                            marriage.StartYear,
                            marriage.EndYear,
                            marriage.EndReason))
                .ToList();

        return new GenealogyPersonRecord(
            person.Id,
            _family.GetDisplayName(
                person),
            _family.IsBloodline(
                person),
            _family.IsMaleLineage(
                person),
            person.Tags.Has(
                "state.alive"),
            GetBirthYear(
                person),
            person.DeathDate?.Year,
            parents,
            _family.GetSpouse(
                person)?.Id,
            history);
    }

    private int GetBirthYear(
        IPerson person)
    {
        if (person.BirthDate
            is GameDate birthDate)
        {
            return birthDate.Year;
        }

        if (person.DeathDate
            is GameDate deathDate)
        {
            return deathDate.Year
                - person.Age;
        }

        return _gameState.Year
            - person.Age;
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        _visualVersion++;

        if (TopologyEventTypes.Contains(
            gameEvent.Type))
        {
            _topologyVersion++;

            TopologyChanged?.Invoke(
                this,
                EventArgs.Empty);

            return;
        }

        VisualStateChanged?.Invoke(
            this,
            EventArgs.Empty);
    }
}
