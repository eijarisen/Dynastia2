using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class StandardCareerService
{
    public IReadOnlyList<JobOpportunityInfo> GetJobOpportunities(
        IPerson person,
        int count = 5)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (count <= 0)
            return [];

        var career = GetRequired(person);
        if (career.IsRetired
            || person.Age < 18
            || !person.Tags.Has("state.alive")
            || person.Tags.Has("state.imprisoned"))
        {
            return [];
        }

        var current = GetCareer(person);
        if (current.JobLevel > 3)
            return [];

        var sex = _family.GetSex(person);
        var year = _gameState.Year;
        var deterministic = new DeterministicCareerRandom(
            $"{_gameState.DynastySurname}|{person.Id:N}|{year}|job-board");

        var available = _catalog.All
            .Where(definition => definition.IsOpenForEntry(year))
            .Select(definition =>
            {
                var evaluation = _localOpportunities.Evaluate(
                    person,
                    definition.LocationRequirement);

                var weight = evaluation.IsEligible
                    ? definition.GetEntryWeight(sex, year)
                        * evaluation.WeightMultiplier
                    : 0;

                return new WeightedCareerCandidate(
                    definition,
                    weight);
            })
            .Where(candidate => candidate.Weight > 0)
            .ToList();

        var result = new List<JobOpportunityInfo>();
        var desired = Math.Min(count, available.Count);

        for (var slot = 0;
            slot < desired && available.Count > 0;
            slot++)
        {
            var level = RollVacancyLevel(
                deterministic,
                career);

            var selected = SelectWeighted(
                available,
                deterministic);

            available.Remove(selected);

            if (current.JobLevel > 0
                && selected.Career.Id.Equals(
                    current.CareerId,
                    StringComparison.OrdinalIgnoreCase)
                && level <= current.JobLevel)
            {
                slot--;
                desired = Math.Min(count, result.Count + available.Count);
                continue;
            }

            if (current.IsEmployed
                && selected.Career.BaseSalary * level
                    <= current.AnnualIncome)
            {
                // An employed applicant is shown opportunities that could
                // plausibly be an improvement rather than obvious pay cuts.
                slot--;
                desired = Math.Min(count, result.Count + available.Count);
                continue;
            }

            result.Add(CreateJobOpportunity(
                person,
                selected.Career,
                level));
        }

        return result;
    }

    public JobApplicationResult ApplyForJob(
        IPerson person,
        string careerId,
        int jobLevel)
        => ApplyForJobInternal(
            person,
            careerId,
            jobLevel,
            fixedSuccessChance: null);

    internal JobApplicationResult ApplyForJobWithChance(
        IPerson person,
        string careerId,
        int jobLevel,
        double successChance)
        => ApplyForJobInternal(
            person,
            careerId,
            jobLevel,
            Math.Clamp(successChance, 0.05, 0.95));

    private JobApplicationResult ApplyForJobInternal(
        IPerson person,
        string careerId,
        int jobLevel,
        double? fixedSuccessChance)
    {
        ArgumentNullException.ThrowIfNull(person);

        var before = GetCareer(person);
        var definition = _catalog.Find(careerId);
        var level = Math.Clamp(jobLevel, 1, 3);

        if (definition is null
            || before.IsRetired
            || person.Age < 18
            || !person.Tags.Has("state.alive")
            || person.Tags.Has("state.imprisoned"))
        {
            return new JobApplicationResult(
                false,
                false,
                false,
                0,
                before,
                before);
        }

        var local = _localOpportunities.Evaluate(
            person,
            definition.LocationRequirement);

        if (!local.IsEligible)
        {
            return new JobApplicationResult(
                false,
                false,
                false,
                0,
                before,
                before);
        }

        var chance = fixedSuccessChance
            ?? CalculateApplicationChance(
                person,
                definition,
                level,
                local);

        if (_random.NextDouble() >= chance)
        {
            return new JobApplicationResult(
                true,
                false,
                false,
                chance,
                before,
                before);
        }

        var offeredSalary = definition.BaseSalary * level;
        if (before.IsEmployed
            && offeredSalary <= before.AnnualIncome)
        {
            return new JobApplicationResult(
                true,
                false,
                true,
                chance,
                before,
                before);
        }

        AssignCareer(
            person,
            definition.Id,
            level,
            Math.Max(3, before.JobSatisfaction));

        var after = GetCareer(person);
        return new JobApplicationResult(
            true,
            true,
            false,
            chance,
            before,
            after);
    }

    public void AssignCareer(
        IPerson person,
        string? careerId,
        int jobLevel,
        int jobSatisfaction)
    {
        ArgumentNullException.ThrowIfNull(person);

        var level = Math.Clamp(jobLevel, 0, 5);

        if (_craftResolver()?.IsSelfEmployed(person) == true)
        {
            _craftResolver()?.EndOccupation(
                person,
                level > 0 ? "formal employment" : "ended");
        }

        var definition = level > 0
            ? _catalog.Find(careerId)
            : null;

        if (level > 0 && definition is null)
        {
            throw new InvalidOperationException(
                $"Unknown career '{careerId}'.");
        }

        var component = person.Components.Get<CareerComponent>()
            ?? new CareerComponent();

        component.CareerId = level > 0
            ? definition!.Id
            : null;
        component.JobLevel = level;
        component.JobSatisfaction = Math.Clamp(jobSatisfaction, 1, 5);
        component.IsRetired = false;
        component.ExperienceYearsByCareer ??=
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        UpdatePeakCareer(component);
        person.Components.Set(component);
    }

    public GeneratedCareerProfile GenerateCandidateCareer(
        Sex sex,
        TownInfo town,
        int year,
        int jobLevel,
        int strength,
        int intellect,
        int educationLevel,
        string deterministicKey)
    {
        ArgumentNullException.ThrowIfNull(town);
        ArgumentException.ThrowIfNullOrWhiteSpace(deterministicKey);

        var desiredLevel = Math.Clamp(jobLevel, 0, 3);
        var candidateStrength = Math.Clamp(strength, 1, 5);
        var candidateIntellect = Math.Clamp(intellect, 1, 5);
        var education = Math.Clamp(educationLevel, 0, 5);
        var random = new DeterministicCareerRandom(
            $"{_gameState.DynastySurname}|{deterministicKey}|candidate-career");
        var satisfaction = random.NextInt(1, 5);

        if (desiredLevel == 0)
        {
            return new GeneratedCareerProfile(
                null,
                string.Empty,
                "Unemployed",
                0,
                satisfaction,
                0);
        }

        var candidates = _catalog.All
            .Where(definition => definition.IsOpenForEntry(year))
            .Select(definition =>
            {
                var evaluation = _localOpportunities.Evaluate(
                    town,
                    definition.LocationRequirement);

                var compatibility = GetGeneratedCandidateCareerFit(
                    definition,
                    desiredLevel,
                    candidateStrength,
                    candidateIntellect,
                    education);

                return new WeightedCareerCandidate(
                    definition,
                    evaluation.IsEligible
                        ? definition.GetEntryWeight(sex, year)
                            * evaluation.WeightMultiplier
                            * compatibility
                        : 0);
            })
            .Where(candidate => candidate.Weight > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            return new GeneratedCareerProfile(
                null,
                string.Empty,
                "Unemployed",
                0,
                satisfaction,
                0);
        }

        var chosen = SelectWeighted(candidates, random).Career;
        var level = AdjustGeneratedCandidateJobLevel(
            chosen,
            desiredLevel,
            candidateStrength,
            candidateIntellect,
            education);
        var careerName = _presentation.ResolveCareerName(
            chosen.Id,
            chosen.Name,
            year);
        var title = _presentation.ResolveCareerTitle(
            chosen.Id,
            level,
            chosen.GetTitle(level),
            year);

        return new GeneratedCareerProfile(
            chosen.Id,
            careerName,
            title,
            level,
            satisfaction,
            chosen.BaseSalary * level);
    }

    private static double GetGeneratedCandidateCareerFit(
        CareerDefinition definition,
        int desiredLevel,
        int strength,
        int intellect,
        int education)
    {
        var aptitude = CareerEntryAptitudeClassifier.Get(definition);
        var ability = aptitude == CareerEntryAptitude.Intellect
            ? intellect
            : strength;
        var abilityMultiplier = ability switch
        {
            1 => 0.30,
            2 => 0.65,
            3 => 1.00,
            4 => 1.25,
            _ => 1.45
        };

        if (aptitude != CareerEntryAptitude.Intellect)
            return abilityMultiplier;

        var educationMultiplier = desiredLevel switch
        {
            >= 3 => education switch
            {
                0 => 0.15,
                1 => 0.50,
                2 => 0.85,
                _ => 1.10
            },
            2 => education switch
            {
                0 => 0.35,
                1 => 0.75,
                _ => 1.05
            },
            _ => 0.80 + education * 0.06
        };

        return abilityMultiplier * educationMultiplier;
    }

    private static int AdjustGeneratedCandidateJobLevel(
        CareerDefinition definition,
        int desiredLevel,
        int strength,
        int intellect,
        int education)
    {
        var aptitude = CareerEntryAptitudeClassifier.Get(definition);
        var ability = aptitude == CareerEntryAptitude.Intellect
            ? intellect
            : strength;
        var level = Math.Clamp(desiredLevel, 1, 3);

        if (ability <= 1)
            level = 1;
        else if (ability == 2 && level > 2)
            level = 2;

        if (aptitude == CareerEntryAptitude.Intellect)
        {
            if (education == 0)
                level = Math.Min(level, 1);
            else if (education == 1)
                level = Math.Min(level, 2);
        }

        return level;
    }

    internal void RecordCurrentExperience(
        IPerson person)
    {
        var component = GetRequired(person);
        if (component.IsRetired
            || component.JobLevel <= 0
            || string.IsNullOrWhiteSpace(component.CareerId))
        {
            return;
        }

        component.ExperienceYearsByCareer ??=
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        component.ExperienceYearsByCareer.TryGetValue(
            component.CareerId,
            out var years);

        component.ExperienceYearsByCareer[component.CareerId] =
            years + 1;
    }

    private JobOpportunityInfo CreateJobOpportunity(
        IPerson person,
        CareerDefinition definition,
        int level)
    {
        var year = _gameState.Year;
        var aptitude = CareerEntryAptitudeClassifier.Get(definition);
        var statId = CareerEntryAptitudeClassifier.GetStatId(aptitude);
        var applicantAbility = _stats.GetStats(person)
            .First(value => value.Id.Equals(
                statId,
                StringComparison.OrdinalIgnoreCase))
            .Value;
        var applicantEducation = _education.GetEducationLevel(person);
        var experience = GetRelevantExperience(person, definition);
        var requirements = GetVacancyRequirements(level);
        var local = _localOpportunities.Evaluate(
            person,
            definition.LocationRequirement);
        var chance = CalculateApplicationChance(
            person,
            definition,
            level,
            local);

        return new JobOpportunityInfo(
            definition.Id,
            _presentation.ResolveCareerName(
                definition.Id,
                definition.Name,
                year),
            _presentation.ResolveCareerTitle(
                definition.Id,
                level,
                definition.GetTitle(level),
                year),
            level,
            definition.BaseSalary * level,
            aptitude == CareerEntryAptitude.Intellect
                ? "Intellect"
                : "Strength",
            requirements.Ability,
            requirements.Education,
            requirements.Experience,
            applicantAbility,
            applicantEducation,
            experience.Total,
            chance,
            year,
            _craftResolver()?.GetApplicationBonus(person, definition.Id) ?? 0);
    }

    private double CalculateApplicationChance(
        IPerson person,
        CareerDefinition definition,
        int level,
        CareerLocationEvaluation local)
    {
        var aptitude = CareerEntryAptitudeClassifier.Get(definition);
        var statId = CareerEntryAptitudeClassifier.GetStatId(aptitude);
        var stat = _stats.GetStats(person)
            .First(value => value.Id.Equals(
                statId,
                StringComparison.OrdinalIgnoreCase))
            .Value;
        var component = GetRequired(person);
        component.ExperienceYearsByCareer ??=
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var education = _education.GetEducationLevel(person);
        var experience = GetRelevantExperience(person, definition);
        var requirements = GetVacancyRequirements(level);

        // Vacancy requirements are the anchor. Meeting them gives a credible
        // chance, while exceeding or missing them moves the actual approval
        // percentage shown on the board.
        var chance = 0.55;
        chance += (stat - requirements.Ability) * 0.12;
        chance += (education - requirements.Education) * 0.08;
        chance += Math.Clamp(
            experience.Total - requirements.Experience,
            -5,
            5) * 0.03;

        if (experience.Exact > 0)
            chance += 0.10;
        else if (experience.Related > 0)
            chance += 0.05;

        chance += _craftResolver()?.GetApplicationBonus(person, definition.Id) ?? 0;

        chance += Math.Min(5, component.PeakJobLevel) * 0.02;

        chance += local.Strength switch
        {
            CareerOpportunityStrength.Town => 0.05,
            CareerOpportunityStrength.Regional => 0.03,
            _ => 0
        };

        chance = PersonalityInfluence.AdjustProbability(
            chance,
            person,
            sanguine: 0.10);

        return Math.Clamp(chance, 0.05, 0.95);
    }

    private (int Exact, int Related, int Total) GetRelevantExperience(
        IPerson person,
        CareerDefinition definition)
    {
        var component = GetRequired(person);
        component.ExperienceYearsByCareer ??=
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var exact = component.ExperienceYearsByCareer
            .TryGetValue(definition.Id, out var exactYears)
                ? exactYears
                : 0;

        var related = component.ExperienceYearsByCareer
            .Where(pair => !pair.Key.Equals(
                    definition.Id,
                    StringComparison.OrdinalIgnoreCase)
                && IsSameCareerFamily(
                    definition,
                    _catalog.Find(pair.Key)))
            .Sum(pair => pair.Value);

        var craftExperience = _craftResolver()?.GetCareerExperience(person, definition.Id);
        if (craftExperience is not null)
        {
            exact += craftExperience.ExactYears;
            related += craftExperience.RelatedYears;
        }

        return (exact, related, exact + related);
    }

    private static (int Ability, int Education, int Experience)
        GetVacancyRequirements(int level) =>
        Math.Clamp(level, 1, 3) switch
        {
            1 => (2, 1, 0),
            2 => (3, 3, 2),
            _ => (4, 4, 5)
        };

    private static bool IsSameCareerFamily(
        CareerDefinition first,
        CareerDefinition? second)
    {
        if (second is null)
            return false;

        if (first.RequiredOpportunityTags.Count > 0
            && second.RequiredOpportunityTags.Count > 0
            && first.RequiredOpportunityTags.Any(tag =>
                second.RequiredOpportunityTags.Contains(
                    tag,
                    StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        return first.LocationType == CareerLocationType.Specialist
            && second.LocationType == CareerLocationType.Specialist
            && CareerEntryAptitudeClassifier.Get(first)
                == CareerEntryAptitudeClassifier.Get(second);
    }

    private static int RollVacancyLevel(
        IGameRandom random,
        CareerComponent career)
    {
        var experienced = career.PeakJobLevel >= 3
            || (career.ExperienceYearsByCareer?.Values.Sum() ?? 0) >= 5;

        var roll = random.NextDouble();

        if (experienced)
        {
            if (roll < 0.45)
                return 1;
            if (roll < 0.80)
                return 2;
            return 3;
        }

        if (roll < 0.55)
            return 1;
        if (roll < 0.90)
            return 2;
        return 3;
    }

    private static WeightedCareerCandidate SelectWeighted(
        IReadOnlyList<WeightedCareerCandidate> candidates,
        IGameRandom random)
    {
        var total = candidates.Sum(candidate => candidate.Weight);
        var roll = random.NextDouble() * total;

        foreach (var candidate in candidates)
        {
            if (roll < candidate.Weight)
                return candidate;
            roll -= candidate.Weight;
        }

        return candidates[^1];
    }

    private sealed record WeightedCareerCandidate(
        CareerDefinition Career,
        double Weight);
}
