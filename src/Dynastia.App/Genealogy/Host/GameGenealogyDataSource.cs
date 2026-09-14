namespace Dynastia.App.Genealogy.Host;

using Dynastia.App.ViewModels;
using Dynastia.Contracts;
using Dynastia.StandardUI.Genealogy.Contracts;
using Dynastia.StandardUI.Genealogy.Models;

/// <summary>
/// Adapter from the current game state to the read-only genealogy projection.
/// The visual fields deliberately mirror the Households person-card content.
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
                "relationship.low_satisfaction_divorce",
                "relationship.affair"
            };

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IHealthService? _health;
    private readonly IEducationService? _education;
    private readonly ICareerService? _career;
    private readonly IJusticeService? _justice;
    private readonly IStatsService? _stats;
    private readonly ILocationService? _locations;
    private readonly IMarriageSatisfactionService?
        _marriageSatisfaction;
    private readonly IThoughtService? _thoughts;
    private readonly ISuccessionService? _succession;

    private long _topologyVersion;
    private long _visualVersion;

    public GameGenealogyDataSource(
        IGameState gameState,
        IFamilyService family,
        IGameEventBus events,
        IHealthService? health = null,
        IEducationService? education = null,
        ICareerService? career = null,
        IJusticeService? justice = null,
        IStatsService? stats = null,
        ILocationService? locations = null,
        IMarriageSatisfactionService?
            marriageSatisfaction = null,
        IThoughtService? thoughts = null,
        ISuccessionService? succession = null)
    {
        _gameState =
            gameState;

        _family =
            family;

        _health =
            health;

        _education =
            education;

        _career =
            career;

        _justice =
            justice;

        _stats =
            stats;

        _locations =
            locations;

        _marriageSatisfaction =
            marriageSatisfaction;

        _thoughts =
            thoughts;

        _succession =
            succession;

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

        var foundingFather =
            founder is null
                ? null
                : _family.GetFather(
                    founder);

        var treeRoot =
            foundingFather is not null
            && _family.IsBloodline(
                foundingFather)
                ? foundingFather
                : founder;

        return new GenealogySnapshot(
            people,
            treeRoot?.Id,
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

        var isAlive =
            person.Tags.Has(
                "state.alive")
            && !person.Tags.Has(
                "state.dead");

        var birthYear =
            GetBirthYear(
                person);

        var deathYear =
            person.DeathDate?.Year;

        var firstName =
            person.Name;

        var surname =
            _family.FormatSurname(
                person.Surname,
                _family.GetSex(
                    person));

        var avatar =
            PersonEmojiResolver
                .GetPersonEmoji(
                    person,
                    _family,
                    _health,
                    _career,
                    _justice,
                    _stats,
                    _thoughts);

        double? healthValue =
            null;

        var healthTooltip =
            isAlive
                ? "Unknown"
                : "Deceased";

        if (_health is not null
            && isAlive)
        {
            var health =
                _health.GetHealth(
                    person);

            healthValue =
                health.Percentage;

            healthTooltip =
                $"{Math.Round(health.Current)}/" +
                $"{Math.Round(health.Maximum)}";
        }

        var educationTooltip =
            _education is null
                ? "Unknown"
                : $"Level " +
                  $"{_education.GetEducationLevel(person)}";

        var occupation =
            string.Empty;

        var occupationTooltip =
            "Unknown";

        var satisfactionTooltip =
            "Unknown";

        if (_career is not null)
        {
            var career =
                _career.GetCareer(
                    person);

            occupation =
                career.JobTitle;

            occupationTooltip =
                career.IsRetired
                    ? career.AnnualIncome > 0
                        ? $"{career.JobTitle} — " +
                          $"{career.AnnualIncome:N0} zł/year pension"
                        : career.JobTitle
                    : career.JobLevel > 0
                        ? $"{career.JobTitle} — " +
                          $"{career.AnnualIncome:N0} zł/year"
                        : career.JobTitle;

            satisfactionTooltip =
                career.JobLevel > 0
                && !career.IsRetired
                    ? career.JobSatisfactionText
                    : "N/A";
        }

        if (_justice is not null)
        {
            var justice =
                _justice.GetStatus(
                    person);

            if (justice.IsImprisoned)
            {
                occupation =
                    justice.IsLifeSentence
                        ? "Imprisoned · Life"
                        : justice.RemainingYears == 1
                            ? "Imprisoned · 1 year left"
                            : $"Imprisoned · " +
                              $"{justice.RemainingYears} " +
                              "years left";

                occupationTooltip =
                    occupation;
            }
        }

        var town =
            string.Empty;

        if (_locations is not null)
        {
            var location =
                _locations.GetLocation(
                    person);

            town =
                isAlive
                    ? location.HomeTown.Town
                    : (
                        location.DeathTown
                        ?? location.HomeTown
                    ).Town;
        }

        var spouse =
            _family.GetSpouse(
                person);

        var spouseTooltip =
            spouse is null
                ? "None"
                : _family.GetDisplayName(
                    spouse);

        var marriage =
            _marriageSatisfaction?
                .GetSatisfaction(
                    person);

        var marriageTooltip =
            marriage is null
                ? "N/A"
                : marriage.Label;

        var thought =
            isAlive
                ? _thoughts?.GetCurrentThought(
                    person)
                : null;

        var livingTooltipSections =
            new List<string>();

        var quotedThought =
            ThoughtUiFormatter.QuoteAndWrap(
                thought?.Text);

        if (!string.IsNullOrWhiteSpace(
            quotedThought))
        {
            livingTooltipSections.Add(
                quotedThought
                + Environment.NewLine);
        }

        livingTooltipSections.Add(
            $"Health: {healthTooltip}");

        if (person.Age >= 18)
        {
            livingTooltipSections.Add(
                $"Career: {satisfactionTooltip}");

            livingTooltipSections.Add(
                $"Marriage: {marriageTooltip}");
        }

        var tooltipFather =
            _family.GetFather(
                person);

        var tooltipMother =
            _family.GetMother(
                person);

        var generatedBackground =
            _family.GetGeneratedFamilyBackground(
                person);

        var fatherName =
            tooltipFather is not null
                ? _family.GetDisplayName(tooltipFather)
                : generatedBackground?.FatherName
                  ?? "Unknown";

        var motherName =
            tooltipMother is not null
                ? _family.GetDisplayName(tooltipMother)
                : generatedBackground?.MotherName
                  ?? "Unknown";

        var parentsTooltip =
            fatherName == "Unknown"
            && motherName == "Unknown"
                ? "Unknown"
                : $"{fatherName}, {motherName}";

        var children =
            _family.GetChildren(
                person);

        var childrenTooltip =
            children.Count == 0
                ? "None"
                : string.Join(
                    ", ",
                    children.Select(
                        _family.GetDisplayName));

        var lastOccupationTooltip =
            _career is null
                ? occupationTooltip
                : ResolveLastOccupation(
                    _career.GetCareer(person));

        var tooltip =
            isAlive
                ? string.Join(
                    Environment.NewLine,
                    livingTooltipSections)
                : string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        $"Education: {educationTooltip}",
                        $"Parents: {parentsTooltip}",
                        $"Spouse: {spouseTooltip}",
                        $"Children: {childrenTooltip}",
                        $"Last occupation: {lastOccupationTooltip}"
                    });

        var lifeSpan =
            isAlive
                ? $"{birthYear} • Age {person.Age}"
                : deathYear is int died
                    ? $"{birthYear}-{died} • Age {person.Age}"
                    : $"{birthYear} • Age {person.Age}";

        return new GenealogyPersonRecord(
            person.Id,
            _family.GetDisplayName(
                person),
            firstName,
            surname,
            avatar,
            _family.IsBloodline(
                person),
            _family.IsMaleLineage(
                person),
            isAlive,
            birthYear,
            deathYear,
            healthValue,
            lifeSpan,
            town,
            occupation,
            isAlive
                && !string.IsNullOrWhiteSpace(
                    occupation),
            _succession?.ActiveControllerId
                == person.Id,
            tooltip,
            parents,
            spouse?.Id,
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
    private static string ResolveLastOccupation(
        CareerSnapshot career)
    {
        if (career.JobLevel > 0)
        {
            return $"{career.JobTitle} ({career.JobLevel})";
        }

        if (!string.IsNullOrWhiteSpace(
                career.PeakJobTitle))
        {
            return career.PeakJobLevel > 0
                ? $"{career.PeakJobTitle} ({career.PeakJobLevel})"
                : career.PeakJobTitle;
        }

        if (career.StatusId?.Equals(
                "status.housewife",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return career.JobTitle;
        }

        return "None";
    }

}
