using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

internal sealed class NonmaritalBirthYearSystem : IYearSystem
{
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IPersonalityService _personality;
    private readonly IPartnerSearchService _partnerSearch;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly ReproductionYearSystem _reproduction;
    private readonly NonmaritalBirthRules _rules;

    public NonmaritalBirthYearSystem(
        IFamilyService family,
        IStatsService stats,
        IPersonalityService personality,
        IPartnerSearchService partnerSearch,
        IGameRandom random,
        IGameEventBus events,
        ReproductionYearSystem reproduction,
        NonmaritalBirthRules rules)
    {
        _family = family;
        _stats = stats;
        _personality = personality;
        _partnerSearch = partnerSearch;
        _random = random;
        _events = events;
        _reproduction = reproduction;
        _rules = rules;
    }

    public string Id => "reproduction.nonmarital_births";

    public YearPhase Phase => YearPhase.LifeEvents;

    public IReadOnlyCollection<string> Before => ["reproduction.births"];

    public IReadOnlyCollection<string> After =>
        [
            "relationships.marriage",
            "relationships.affairs"
        ];

    public void Execute(IGameState gameState)
    {
        var eligibleSnapshot = gameState.People
            .Where(IsEligible)
            .ToList();

        foreach (var mother in eligibleSnapshot)
        {
            var fertility = GetStat(mother, "fertility");
            var chance = _rules.CalculateChance(
                mother.Age,
                fertility,
                _personality.GetPersonality(mother));

            if (_random.NextDouble() >= chance)
                continue;

            if (_random.NextDouble() < _rules.UnknownFatherChance)
            {
                ResolveUnknownFather(gameState, mother);
            }
            else
            {
                ResolveBirthAndMarriage(gameState, mother);
            }
        }
    }

    private bool IsEligible(IPerson person)
    {
        if (!person.Tags.Has("state.alive")
            || person.Tags.Has("state.imprisoned")
            || person.Tags.Has("vocation.religious.active")
            || _family.GetSex(person) != Sex.Female
            || person.Age < _rules.MinimumAge
            || person.Age > _rules.MaximumAge
            || _family.GetSpouse(person) is not null)
        {
            return false;
        }

        return GetStat(person, "fertility") > 0;
    }

    private void ResolveUnknownFather(
        IGameState gameState,
        IPerson mother)
    {
        var child = _reproduction.CreateNonmaritalChild(
            gameState,
            mother,
            father: null);

        PublishIntegrationBirth(
            gameState,
            child,
            father: null,
            mother,
            "The father is unknown.");

        _events.Publish(new GameEvent
        {
            Type = "reproduction.unknown_father_birth",
            Year = gameState.Year,
            SubjectId = mother.Id,
            RelatedPersonIds = [child.Id],
            Data = new Dictionary<string, string>
            {
                ["motherId"] = mother.Id.ToString(),
                ["childId"] = child.Id.ToString(),
                ["Mother"] = _family.GetDisplayName(mother),
                ["Child"] = _family.GetDisplayName(child),
                ["text"] =
                    $"{_family.GetDisplayName(mother)} gave birth to " +
                    $"{_family.GetDisplayName(child)}. The father is unknown."
            }
        });

        PublishTeenBirthIfNeeded(
            gameState,
            mother,
            child);
    }

    private void ResolveBirthAndMarriage(
        IGameState gameState,
        IPerson mother)
    {
        var candidate = _partnerSearch.GetCandidatesFor(
                mother,
                Sex.Male,
                "nonmarital_father",
                count: 1,
                minimumSeekerAge: _rules.MinimumAge,
                minimumPartnerAge: _rules.FatherMinimumAge,
                maximumPartnerAge:
                    mother.Age + _rules.FatherMaximumAgeOffsetFromMother)
            .SingleOrDefault();

        if (candidate is null)
        {
            // Candidate generation is expected to succeed for eligible women.
            // If external data makes it impossible, preserve the resolved birth
            // rather than silently discarding the event.
            ResolveUnknownFather(gameState, mother);
            return;
        }

        var father = _partnerSearch.MaterializeCandidate(
            candidate,
            currentAge: candidate.Age,
            householdSpouse: mother,
            seedHouseholdResources: true);

        mother.MaidenName ??= mother.Surname;
        _family.SetSpouses(
            mother,
            father,
            gameState.Year);
        mother.Surname = father.Surname;

        _events.Publish(new GameEvent
        {
            Type = "relationship.married",
            Year = gameState.Year,
            SubjectId = mother.Id,
            RelatedPersonIds = [father.Id],
            Data = new Dictionary<string, string>
            {
                ["spouseId"] = father.Id.ToString(),
                ["preserveGeneratedProfile"] = "true",
                ["suppressChronicle"] = "true",
                ["text"] =
                    $"{_family.GetDisplayName(mother)} married " +
                    $"{_family.GetDisplayName(father)}."
            }
        });

        var child = _reproduction.CreateNonmaritalChild(
            gameState,
            mother,
            father);

        PublishIntegrationBirth(
            gameState,
            child,
            father,
            mother,
            string.Empty);

        _events.Publish(new GameEvent
        {
            Type = "reproduction.birth_and_marriage",
            Year = gameState.Year,
            SubjectId = mother.Id,
            RelatedPersonIds = [child.Id, father.Id],
            Data = new Dictionary<string, string>
            {
                ["motherId"] = mother.Id.ToString(),
                ["husbandId"] = father.Id.ToString(),
                ["childId"] = child.Id.ToString(),
                ["Mother"] = _family.GetDisplayName(mother),
                ["Father"] = _family.GetDisplayName(father),
                ["Child"] = _family.GetDisplayName(child),
                ["text"] =
                    $"{_family.GetDisplayName(mother)} gave birth to " +
                    $"{_family.GetDisplayName(child)} and married " +
                    $"{_family.GetDisplayName(father)} in the same year."
            }
        });

        PublishTeenBirthIfNeeded(
            gameState,
            mother,
            child);

        if (mother.Age <= 17)
        {
            _events.Publish(new GameEvent
            {
                Type = "reproduction.teen_marriage",
                Year = gameState.Year,
                SubjectId = mother.Id,
                RelatedPersonIds = [father.Id],
                Data = new Dictionary<string, string>
                {
                    ["motherId"] = mother.Id.ToString(),
                    ["husbandId"] = father.Id.ToString(),
                    ["Age"] = mother.Age.ToString(),
                    ["Mother"] = _family.GetDisplayName(mother),
                    ["Father"] = _family.GetDisplayName(father),
                    ["text"] =
                        $"At age {mother.Age}, {_family.GetDisplayName(mother)} " +
                        $"married {_family.GetDisplayName(father)}."
                }
            });
        }
    }

    private void PublishTeenBirthIfNeeded(
        IGameState gameState,
        IPerson mother,
        IPerson child)
    {
        if (mother.Age > 17)
            return;

        _events.Publish(new GameEvent
        {
            Type = "reproduction.teen_birth",
            Year = gameState.Year,
            SubjectId = mother.Id,
            RelatedPersonIds = [child.Id],
            Data = new Dictionary<string, string>
            {
                ["motherId"] = mother.Id.ToString(),
                ["childId"] = child.Id.ToString(),
                ["Age"] = mother.Age.ToString(),
                ["Mother"] = _family.GetDisplayName(mother),
                ["Child"] = _family.GetDisplayName(child),
                ["text"] =
                    $"At age {mother.Age}, {_family.GetDisplayName(mother)} " +
                    $"gave birth to {_family.GetDisplayName(child)}."
            }
        });
    }

    private void PublishIntegrationBirth(
        IGameState gameState,
        IPerson child,
        IPerson? father,
        IPerson mother,
        string suffix)
    {
        var fatherCount = father is null
            ? 0
            : _family.GetChildren(father).Count;
        var motherCount = _family.GetChildren(mother).Count;
        var text = father is null
            ? $"{_family.GetDisplayName(child)} was born to " +
              $"{_family.GetDisplayName(mother)}. {suffix}".Trim()
            : $"{_family.GetDisplayName(child)} was born to " +
              $"{_family.GetDisplayName(father)} and " +
              $"{_family.GetDisplayName(mother)}.";

        _events.Publish(new GameEvent
        {
            Type = "life.birth",
            Year = gameState.Year,
            SubjectId = child.Id,
            RelatedPersonIds =
                father is null
                    ? [Guid.Empty, mother.Id]
                    : [father.Id, mother.Id],
            Data = new Dictionary<string, string>
            {
                ["fatherId"] = father?.Id.ToString() ?? string.Empty,
                ["motherId"] = mother.Id.ToString(),
                ["fatherCount"] = fatherCount.ToString(),
                ["motherCount"] = motherCount.ToString(),
                ["sharedCount"] = "1",
                ["multipleBirthCount"] = "1",
                ["suppressChronicle"] = "true",
                ["text"] = text
            }
        });
    }

    private int GetStat(IPerson person, string statId) =>
        _stats.GetStats(person)
            .First(stat => stat.Id.Equals(
                statId,
                StringComparison.OrdinalIgnoreCase))
            .Value;
}
