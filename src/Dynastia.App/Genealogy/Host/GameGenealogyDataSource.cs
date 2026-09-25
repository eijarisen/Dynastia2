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
                "relationship.low_satisfaction_divorce"
            };

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IHealthService? _health;
    private readonly IEducationService? _education;
    private readonly ICareerService? _career;
    private readonly IFarmingService? _farming;
    private readonly IJusticeService? _justice;
    private readonly IStatsService? _stats;
    private readonly ILocationService? _locations;
    private readonly IMarriageSatisfactionService?
        _marriageSatisfaction;
    private readonly IStressService? _stress;
    private readonly IChildHappinessService? _childHappiness;
    private readonly IStatusService? _status;
    private readonly IThoughtService? _thoughts;
    private readonly IAppearanceService? _appearance;
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
        IFarmingService? farming = null,
        IJusticeService? justice = null,
        IStatsService? stats = null,
        ILocationService? locations = null,
        IMarriageSatisfactionService?
            marriageSatisfaction = null,
        IThoughtService? thoughts = null,
        IAppearanceService? appearance = null,
        ISuccessionService? succession = null,
        IStressService? stress = null,
        IChildHappinessService? childHappiness = null,
        IStatusService? status = null)
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

        _farming =
            farming;

        _justice =
            justice;

        _stats =
            stats;

        _locations =
            locations;

        _marriageSatisfaction =
            marriageSatisfaction;

        _stress =
            stress;

        _childHappiness =
            childHappiness;

        _status =
            status;

        _thoughts =
            thoughts;

        _appearance =
            appearance;

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
                person,
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
                    _thoughts,
                    _appearance);

        var portrait = _appearance is not null
            ? _appearance.GetPortrait(
                person,
                useDeadOverride: false)
            : avatar;

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

            if (health.Conditions.Count > 0)
            {
                healthTooltip += " • " + string.Join(
                    ", ",
                    health.Conditions.Select(condition => condition.Name));
            }
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

            var displayedCareerLevel =
                career.IsSelfEmployed
                    ? 0
                    : career.JobLevel > 0
                        ? career.JobLevel
                        : career.IsRetired
                          && career.PeakJobLevel > 0
                            ? career.PeakJobLevel
                            : 0;

            var isFarmWorker =
                _farming?.IsWorkingFarmWorker(person, person) == true;

            occupation =
                isFarmWorker
                    ? FarmingPresentationDefaults.WorkerOccupationLabel
                    : career.IsSelfEmployed
                        ? career.JobTitle
                        : displayedCareerLevel > 0
                            ? $"{career.JobTitle} ({displayedCareerLevel})"
                            : career.JobTitle;

            occupationTooltip =
                isFarmWorker
                    ? occupation
                    : career.IsSelfEmployed
                        ? $"{occupation} — " +
                          $"{career.AnnualIncome:N0} zł/year"
                        : career.IsRetired
                            ? career.AnnualIncome > 0
                                ? $"{occupation} — " +
                                  $"{career.AnnualIncome:N0} zł/year pension"
                                : occupation
                            : career.IsEmployed
                                ? $"{occupation} — " +
                                  $"{career.AnnualIncome:N0} zł/year"
                                : occupation;

            satisfactionTooltip =
                career.IsEmployed
                && (!career.IsRetired || career.IsSelfEmployed)
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

        var stressSnapshot =
            isAlive
                ? _stress?.GetStress(person)
                : null;

        if (stressSnapshot is not null)
        {
            livingTooltipSections.Add(
                $"Stress: {stressSnapshot.Total:0.#}/100");
        }

        if (person.Age >= 6 && _education is not null)
        {
            livingTooltipSections.Add(
                $"Education: {educationTooltip}");
        }

        if (person.Age < 18)
        {
            var happiness =
                _childHappiness?.GetHappiness(person);
            if (happiness is not null)
            {
                livingTooltipSections.Add(
                    $"Happiness: {happiness.Label}");
            }
        }

        if (person.Age >= 18)
        {
            livingTooltipSections.Add(
                $"Career: {satisfactionTooltip}");

            livingTooltipSections.Add(
                $"Marriage: {marriageTooltip}");

            var status =
                _status?.GetStatus(person);
            if (status is not null)
            {
                livingTooltipSections.Add(
                    $"Renown: {status.RenownLabel}");
                livingTooltipSections.Add(
                    $"Reputation: {status.ReputationLabel}");
            }
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
                        $"Occupation: {lastOccupationTooltip}"
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
            portrait,
            _family.IsBloodline(
                person),
            _family.IsMaleLineage(
                person),
            _family.GetSex(
                person) == Sex.Female,
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
        if (career.IsSelfEmployed)
        {
            return career.JobTitle;
        }

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
