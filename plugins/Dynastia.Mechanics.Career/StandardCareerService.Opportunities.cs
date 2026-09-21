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
                    && MeetsInstitutionRequirement(person, definition)
                    ? definition.GetEntryWeight(sex, year)
                        * evaluation.WeightMultiplier
                        * GetSelectionContextMultiplier(definition, person)
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
                && CareerBalanceRules.CalculateAnnualSalary(
                        selected.Career.BaseSalary,
                        level)
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

        if (!local.IsEligible
            || !MeetsInstitutionRequirement(person, definition))
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

        var offeredSalary = CareerBalanceRules.CalculateAnnualSalary(definition.BaseSalary, level);
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

        if (HasCraftOccupation(person))
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
        EnsureLifetimeEarningsInitialized(person, component);
        person.Components.Set(component);
    }

    public GeneratedCareerProfile GenerateCandidateCareer(
        GeneratedCareerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Town);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.DeterministicKey);

        var desiredLevel = Math.Clamp(context.DesiredJobLevel, 0, 3);
        var strength = Math.Clamp(context.Strength, 1, 5);
        var intellect = Math.Clamp(context.Intellect, 1, 5);
        var appeal = Math.Clamp(context.Appeal, 1, 5);
        var education = Math.Clamp(context.EducationLevel, 0, 5);
        var random = new DeterministicCareerRandom(
            $"{_gameState.DynastySurname}|{context.DeterministicKey}|candidate-career");
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
            .Where(definition => definition.IsOpenForEntry(context.Year))
            .Select(definition =>
            {
                var evaluation = _localOpportunities.Evaluate(
                    context.Town,
                    definition.LocationRequirement);

                var compatibility = GetGeneratedCandidateCareerFit(
                    definition,
                    desiredLevel,
                    strength,
                    intellect,
                    appeal,
                    education);

                return new WeightedCareerCandidate(
                    definition,
                    evaluation.IsEligible
                        && MeetsInstitutionRequirement(
                            context.Town,
                            definition,
                            context.Year)
                        ? definition.GetEntryWeight(context.Sex, context.Year)
                            * evaluation.WeightMultiplier
                            * GetSelectionContextMultiplier(definition, context)
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
            strength,
            intellect,
            appeal,
            education);
        var careerName = _presentation.ResolveCareerName(
            chosen.Id,
            chosen.Name,
            context.Year);
        var title = _presentation.ResolveCareerTitle(
            chosen.Id,
            level,
            chosen.GetTitle(level),
            context.Year);

        return new GeneratedCareerProfile(
            chosen.Id,
            careerName,
            title,
            level,
            satisfaction,
            CareerBalanceRules.CalculateAnnualSalary(
                chosen.BaseSalary,
                level));
    }

    private double GetGeneratedCandidateCareerFit(
        CareerDefinition definition,
        int desiredLevel,
        int strength,
        int intellect,
        int appeal,
        int education)
    {
        var ability = GetCareerAbility(
            definition,
            strength,
            intellect,
            appeal);
        return GetCareerFitMultiplier(
            ability,
            education,
            GetExpectedEducation(definition, desiredLevel));
    }

    private int AdjustGeneratedCandidateJobLevel(
        CareerDefinition definition,
        int desiredLevel,
        int strength,
        int intellect,
        int appeal,
        int education)
    {
        var ability = GetCareerAbility(
            definition,
            strength,
            intellect,
            appeal);
        var level = Math.Clamp(desiredLevel, 1, 3);

        if (ability < 1.75)
            level = 1;
        else if (ability < 2.50 && level > 2)
            level = 2;

        while (level > 1
            && education + 1 < GetExpectedEducation(definition, level))
        {
            level--;
        }

        return level;
    }

    public IReadOnlyDictionary<string, int> GetExperienceYearsByCareer(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        var component = GetRequired(person);
        component.ExperienceYearsByCareer ??=
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        return component.ExperienceYearsByCareer.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
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
        var applicantAbility = GetCareerAbility(person, definition);
        var applicantEducation = _education.GetEducationLevel(person);
        var experience = GetRelevantExperience(person, definition);
        var requirements = GetVacancyRequirements(definition, level);
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
            CareerBalanceRules.CalculateAnnualSalary(
                definition.BaseSalary,
                level),
            CareerAptitude.GetDisplayName(
                definition,
                person,
                _stats),
            requirements.Ability,
            requirements.Education,
            requirements.Experience,
            (int)Math.Round(applicantAbility, MidpointRounding.AwayFromZero),
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
        var ability = GetCareerAbility(person, definition);
        var component = GetRequired(person);
        component.ExperienceYearsByCareer ??=
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var education = _education.GetEducationLevel(person);
        var experience = GetRelevantExperience(person, definition);
        var requirements = GetVacancyRequirements(definition, level);

        var chance = 0.55;
        chance += (ability - requirements.Ability) * 0.12;
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
        chance += _statusResolver()?.GetCareerApplicationBonus(person) ?? 0;
        var town = _localOpportunities.GetOpportunitySnapshot(person).Town;
        chance += _communityResolver()?.GetModifiers(town, _gameState.Year).JobApplicationAdd ?? 0;
        chance += Math.Min(5, component.PeakJobLevel) * 0.02;

        chance += local.Strength switch
        {
            CareerOpportunityStrength.Town => 0.05,
            CareerOpportunityStrength.Regional => 0.03,
            _ => 0
        };

        var temperamentFit = GetTemperamentMultiplier(definition, person);
        chance += Math.Clamp((temperamentFit - 1.0) * 0.25, -0.05, 0.05);

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
                && _catalog.Find(pair.Key) is CareerDefinition relatedCareer
                && definition.CareerFamily.Equals(
                    relatedCareer.CareerFamily,
                    StringComparison.OrdinalIgnoreCase))
            .Sum(pair => pair.Value);

        var craftExperience = _craftResolver()?.GetCareerExperience(person, definition.Id);
        if (craftExperience is not null)
        {
            exact += craftExperience.ExactYears;
            related += craftExperience.RelatedYears;
        }

        return (exact, related, exact + related);
    }

    private (int Ability, int Education, int Experience)
        GetVacancyRequirements(CareerDefinition definition, int level)
    {
        var clamped = Math.Clamp(level, 1, 3);
        var ability = clamped switch
        {
            1 => 2,
            2 => 3,
            _ => 4
        };
        var experience = clamped switch
        {
            1 => 0,
            2 => 2,
            _ => 5
        };
        return (
            ability,
            GetExpectedEducation(definition, clamped),
            experience);
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
