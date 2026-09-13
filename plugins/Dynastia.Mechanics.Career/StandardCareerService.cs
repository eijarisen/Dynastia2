using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class StandardCareerService :
    ICareerService
{
    public const decimal DefaultBaseIncomePerLevel =
        500m;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IGameRandom _random;
    private readonly CareerCatalog _catalog;
    private readonly ILocalCareerOpportunityService
        _localOpportunities;
    private readonly IStatsService _stats;

    internal StandardCareerService(
        IGameState gameState,
        IFamilyService family,
        IGameRandom random,
        CareerCatalog catalog,
        ILocalCareerOpportunityService localOpportunities,
        IStatsService stats)
    {
        _gameState = gameState;
        _family = family;
        _random = random;
        _catalog = catalog;
        _localOpportunities =
            localOpportunities;

        _stats =
            stats;
    }

    public void EnsureCareer(
        IPerson person)
    {
        if (person.Components.Has<
            CareerComponent>())
        {
            EnsureValidAssignment(
                person,
                person.Components.Get<
                    CareerComponent>()
                ?? throw new InvalidOperationException(
                    "Career component is unavailable."));

            return;
        }

        var jobLevel =
            person.Age >= 18
                && !person.Tags.Has(
                    "role.nanny")
                ? _random.NextInt(
                    0,
                    3)
                : 0;

        var component =
            new CareerComponent
            {
                JobLevel =
                    jobLevel,

                JobSatisfaction =
                    _random.NextInt(
                        1,
                        5),

                LastIncome =
                    0,

                IsRetired =
                    false
            };

        if (jobLevel > 0)
        {
            component.CareerId =
                SelectCareerForEntry(
                    person)
                .Id;

            UpdatePeakCareer(
                component);
        }

        person.Components.Set(
            component);
    }

    public CareerSnapshot GetCareer(
        IPerson person)
    {
        var career =
            GetRequired(
                person);

        var definition =
            ResolveDefinition(
                person,
                career);

        return new CareerSnapshot(
            career.JobLevel,
            ResolveJobTitle(
                person,
                career,
                definition),
            career.JobSatisfaction,
            ResolveJobSatisfactionText(
                career.JobSatisfaction),
            career.LastIncome,
            GetAnnualIncome(
                person),
            career.IsRetired,
            definition?.Id,
            definition?.Name,
            definition?.BaseSalary
                ?? 0,
            career.PeakJobLevel,
            career.PeakCareerId,
            ResolvePeakJobTitle(
                career));
    }

    public void InitializeCareer(
        IPerson person,
        int jobLevel,
        int jobSatisfaction)
    {
        var level =
            Math.Clamp(
                jobLevel,
                0,
                5);

        var component =
            new CareerComponent
            {
                JobLevel =
                    level,

                JobSatisfaction =
                    Math.Clamp(
                        jobSatisfaction,
                        1,
                        5),

                LastIncome =
                    0,

                IsRetired =
                    false,

                CareerId =
                    level > 0
                        ? SelectCareerForEntry(
                            person)
                            .Id
                        : null
            };

        UpdatePeakCareer(
            component);

        person.Components.Set(
            component);
    }

    public void SetJobLevel(
        IPerson person,
        int jobLevel)
    {
        var career =
            GetRequired(
                person);

        var targetLevel =
            Math.Clamp(
                jobLevel,
                0,
                5);

        if (targetLevel <= 0)
        {
            career.JobLevel =
                0;

            if (!career.IsRetired)
            {
                // Leaving employment deliberately clears the career.
                // A future successful job search therefore draws again
                // from the then-current employment pool.
                career.CareerId =
                    null;
            }

            return;
        }

        var definition =
            _catalog.Find(
                career.CareerId);

        if (career.JobLevel <= 0
            || definition is null)
        {
            career.CareerId =
                SelectCareerForEntry(
                    person)
                    .Id;
        }

        career.JobLevel =
            targetLevel;

        UpdatePeakCareer(
            career);
    }

    public void ChangeJobSatisfaction(
        IPerson person,
        int amount)
    {
        var career =
            GetRequired(
                person);

        var adjustedAmount =
            amount;

        if (amount != 0)
        {
            var direction =
                Math.Sign(
                    amount);

            if ((
                    person.Tags.Has(
                        "personality.melancholic")
                    || person.Tags.Has(
                        "personality.choleric")
                )
                && _random.NextDouble()
                    < 0.20)
            {
                adjustedAmount +=
                    direction;
            }
            else if (person.Tags.Has(
                    "personality.phlegmatic")
                && _random.NextDouble()
                    < 0.20)
            {
                adjustedAmount -=
                    direction;
            }
        }

        career.JobSatisfaction =
            Math.Clamp(
                career.JobSatisfaction
                + adjustedAmount,
                1,
                5);
    }

    public void Retire(
        IPerson person)
    {
        var career =
            GetRequired(
                person);

        if (career.IsRetired)
            return;

        career.LastIncome =
            GetActiveSalary(
                person,
                career);

        UpdatePeakCareer(
            career);

        career.JobLevel =
            0;

        career.IsRetired =
            true;
    }

    public bool TryFindBetterJob(
        IPerson person)
    {
        var current = GetRequired(person);
        if (current.IsRetired || current.JobLevel is < 1 or > 2)
            return false;

        var oldDefinition = ResolveDefinition(person, current);
        var oldSalary = GetActiveSalary(person, current);
        var opportunity = CreateEmploymentOpportunity(person, _stats);
        var offeredSalary = opportunity.Career.BaseSalary * current.JobLevel;

        if (oldDefinition is not null
            && opportunity.Career.Id.Equals(oldDefinition.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var successChance =
            PersonalityInfluence.AdjustProbability(
                opportunity.SuccessChance,
                person,
                sanguine: 0.10);

        if (offeredSalary <= oldSalary
            || _random.NextDouble() >= successChance)
        {
            return false;
        }

        current.CareerId = opportunity.Career.Id;
        UpdatePeakCareer(current);
        return true;
    }

    public bool TryFindEmployment(
        IPerson person,
        double chanceBonus = 0)
    {
        var current = GetRequired(person);
        if (current.IsRetired
            || current.JobLevel != 0
            || person.Age < 18
            || !person.Tags.Has("state.alive")
            || person.Tags.Has("state.imprisoned"))
        {
            return false;
        }

        EmploymentOpportunity opportunity;
        try
        {
            opportunity = CreateEmploymentOpportunity(person, _stats);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        var chance = PersonalityInfluence.AdjustProbability(
            Math.Clamp(opportunity.SuccessChance + chanceBonus, 0, 0.98),
            person,
            sanguine: 0.10);

        if (_random.NextDouble() >= chance)
            return false;

        AcceptEmploymentOpportunity(person, opportunity);
        return true;
    }

    public bool RelocateEmployment(
        IPerson person)
    {
        var career = GetRequired(person);
        if (career.IsRetired || career.JobLevel <= 0)
            return false;

        var previousDefinition = ResolveDefinition(person, career);
        var previousLevel = career.JobLevel;

        if (_random.NextDouble() < 0.10)
        {
            career.JobLevel = 0;
            career.CareerId = null;
            career.JobSatisfaction = 3;
            return false;
        }

        CareerDefinition replacement;
        try
        {
            replacement = SelectRelocationReplacement(
                person,
                previousDefinition);
        }
        catch (InvalidOperationException)
        {
            // A destination can legitimately have no career compatible
            // with this worker in the current historical period.
            career.JobLevel = 0;
            career.CareerId = null;
            career.JobSatisfaction = 3;
            return false;
        }

        var targetLevel = previousLevel;
        if (_random.NextDouble() >= 0.70)
            targetLevel = Math.Max(1, previousLevel - 1);

        career.CareerId = replacement.Id;
        career.JobLevel = targetLevel;
        career.JobSatisfaction = 3;
        UpdatePeakCareer(career);
        return true;
    }

    public decimal GetAnnualIncome(
        IPerson person)
    {
        var career =
            GetRequired(
                person);

        if (career.IsRetired)
        {
            return career.LastIncome
                * 0.10m;
        }

        return GetActiveSalary(
            person,
            career);
    }

    internal EmploymentOpportunity
        CreateEmploymentOpportunity(
            IPerson person,
            IStatsService stats)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        ArgumentNullException.ThrowIfNull(
            stats);

        var definition =
            SelectCareerForEntry(
                person);

        var aptitude =
            CareerEntryAptitudeClassifier.Get(
                definition);

        var statId =
            CareerEntryAptitudeClassifier.GetStatId(
                aptitude);

        var statValue =
            stats.GetStats(
                person)
            .First(
                stat =>
                    stat.Id.Equals(
                        statId,
                        StringComparison.OrdinalIgnoreCase))
            .Value;

        var locationEvaluation =
            _localOpportunities.Evaluate(
                person,
                definition.LocationRequirement);

        // Job hunting is still uncertain, but a person well suited to the
        // careers actually available in the local labour market should not
        // routinely spend three or four years looking for entry-level work.
        var successChance =
            CareerBalanceRules.GetEmploymentSearchChance(
                statValue,
                locationEvaluation.Strength);

        return new EmploymentOpportunity(
            definition,
            aptitude,
            statId,
            statValue,
            successChance);
    }

    internal void AcceptEmploymentOpportunity(
        IPerson person,
        EmploymentOpportunity opportunity)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        ArgumentNullException.ThrowIfNull(
            opportunity);

        var career =
            GetRequired(
                person);

        if (career.IsRetired)
        {
            throw new InvalidOperationException(
                "A retired character cannot accept new employment.");
        }

        career.CareerId =
            opportunity.Career.Id;

        career.JobLevel =
            1;

        UpdatePeakCareer(
            career);
    }

    internal FamilyConnectionOpportunity?
        FindBestFamilyConnection(
            IPerson person)
    {
        var candidates =
            new[]
            {
                _family.GetFather(
                    person),
                _family.GetMother(
                    person)
            }
            .Where(
                parent =>
                    parent is not null
                    && parent.Tags.Has(
                        "state.alive"))
            .Cast<IPerson>()
            .Select(
                parent =>
                    new
                    {
                        Parent =
                            parent,

                        Career =
                            GetCareer(
                                parent)
                    })
            .Where(
                candidate =>
                    candidate.Career.PeakJobLevel >= 3
                    && !string.IsNullOrWhiteSpace(
                        candidate.Career.PeakCareerId))
            .Select(
                candidate =>
                    new
                    {
                        candidate.Parent,
                        candidate.Career,

                        Definition =
                            _catalog.Find(
                                candidate.Career.PeakCareerId)
                    })
            .Where(
                candidate =>
                    candidate.Definition is not null
                    && candidate.Definition.IsOpenForEntry(
                        _gameState.Year)
                    && IsLocallyAvailable(
                        person,
                        candidate.Definition))
            .OrderByDescending(
                candidate =>
                    candidate.Career.PeakJobLevel)
            .ThenBy(
                candidate =>
                    candidate.Parent.Id)
            .FirstOrDefault();

        if (candidates is null
            || candidates.Definition is null)
        {
            return null;
        }

        var targetLevel =
            Math.Clamp(
                candidates.Career.PeakJobLevel - 2,
                1,
                3);

        var acceptanceChance =
            candidates.Career.PeakJobLevel switch
            {
                >= 5 => 0.90,
                4 => 0.70,
                _ => 0.50
            };

        return new FamilyConnectionOpportunity(
            candidates.Parent,
            candidates.Definition,
            candidates.Career.PeakJobLevel,
            targetLevel,
            acceptanceChance);
    }

    internal void AcceptFamilyConnection(
        IPerson person,
        FamilyConnectionOpportunity opportunity)
    {
        var career =
            GetRequired(
                person);

        if (career.IsRetired
            || career.JobLevel > 0)
        {
            throw new InvalidOperationException(
                "Family connections can only place an unemployed non-retired person.");
        }

        career.CareerId =
            opportunity.Career.Id;

        career.JobLevel =
            opportunity.JobLevel;

        UpdatePeakCareer(
            career);
    }

    internal CareerObsolescencePressure
        GetObsolescencePressure(
            IPerson person,
            int gameYear)
    {
        var career =
            GetRequired(
                person);

        if (career.JobLevel <= 0
            || career.IsRetired)
        {
            return CareerObsolescencePressure.None;
        }

        var definition =
            ResolveDefinition(
                person,
                career);

        return definition?
            .GetObsolescencePressure(
                gameYear)
            ?? CareerObsolescencePressure.None;
    }

    internal CareerDefinition? GetDefinition(
        IPerson person)
    {
        var career =
            GetRequired(
                person);

        return ResolveDefinition(
            person,
            career);
    }


    private string? ResolvePeakJobTitle(
        CareerComponent career)
    {
        if (career.PeakJobLevel > 0
            && !string.IsNullOrWhiteSpace(
                career.PeakCareerId))
        {
            return _catalog
                .Find(
                    career.PeakCareerId)
                ?.GetTitle(
                    career.PeakJobLevel);
        }

        // Compatibility fallback for older retired saves that predate
        // peak-career tracking but still retain the career and last salary.
        var definition =
            _catalog.Find(
                career.CareerId);

        if (career.IsRetired
            && definition is not null
            && definition.BaseSalary > 0
            && career.LastIncome > 0)
        {
            var inferredLevel =
                Math.Clamp(
                    (int)Math.Round(
                        career.LastIncome
                        / definition.BaseSalary),
                    1,
                    5);

            return definition.GetTitle(
                inferredLevel);
        }

        return null;
    }

    private decimal GetActiveSalary(
        IPerson person,
        CareerComponent career)
    {
        if (career.JobLevel <= 0)
            return 0;

        var definition =
            ResolveDefinition(
                person,
                career);

        var baseSalary =
            definition?.BaseSalary
            ?? DefaultBaseIncomePerLevel;

        return baseSalary
            * career.JobLevel;
    }

    private string ResolveJobTitle(
        IPerson person,
        CareerComponent career,
        CareerDefinition? definition)
    {
        // Existing special-status precedence is intentionally preserved.
        if (person.Tags.Has(
            "role.nanny"))
        {
            return "Nanny";
        }

        if (person.Tags.Has(
            "state.imprisoned"))
        {
            return "Imprisoned";
        }

        if (career.IsRetired)
            return "Retired";

        if (_family.GetSex(
                person)
                == Sex.Female
            && career.JobLevel == 0
            && _family.GetChildren(
                person)
                .Count > 0)
        {
            return "Housewife";
        }

        if (person.Age < 6)
            return "Preschool";

        if (person.Age < 18)
            return "Student";

        if (career.JobLevel <= 0)
            return "Unemployed";

        return definition?
            .GetTitle(
                career.JobLevel)
            ?? career.JobLevel switch
            {
                1 => "Laborer",
                2 => "Clerk",
                3 => "Manager",
                4 => "Director",
                5 => "Magnate",
                _ => "Unemployed"
            };
    }

    private CareerDefinition? ResolveDefinition(
        IPerson person,
        CareerComponent career)
    {
        EnsureValidAssignment(
            person,
            career);

        return _catalog.Find(
            career.CareerId);
    }

    private void EnsureValidAssignment(
        IPerson person,
        CareerComponent career)
    {
        if (career.JobLevel <= 0)
        {
            if (!career.IsRetired)
            {
                career.CareerId =
                    null;
            }

            return;
        }

        if (_catalog.Find(
            career.CareerId)
            is not null)
        {
            // A valid existing career is retained even if obsolete.
            UpdatePeakCareer(
                career);

            return;
        }

        // Old saves have an employed JobLevel but no CareerId.
        // Preserve the level and assign a career that was open to
        // entrants in the loaded game's effective technological year.
        career.CareerId =
            SelectCareerForEntry(
                person)
                .Id;

        UpdatePeakCareer(
            career);
    }

    private static void UpdatePeakCareer(
        CareerComponent career)
    {
        if (career.JobLevel <= 0
            || string.IsNullOrWhiteSpace(
                career.CareerId))
        {
            return;
        }

        if (career.JobLevel
            > career.PeakJobLevel)
        {
            career.PeakJobLevel =
                career.JobLevel;

            career.PeakCareerId =
                career.CareerId;

            return;
        }

        if (career.JobLevel
                == career.PeakJobLevel
            && string.IsNullOrWhiteSpace(
                career.PeakCareerId))
        {
            career.PeakCareerId =
                career.CareerId;
        }
    }

    private CareerDefinition SelectRelocationReplacement(
        IPerson person,
        CareerDefinition? previous)
    {
        if (previous is not null
            && previous.IsOpenForEntry(_gameState.Year)
            && IsLocallyAvailable(person, previous))
        {
            return previous;
        }

        CareerDefinition? TryPreferred(
            Func<CareerDefinition, bool> predicate)
        {
            try
            {
                return _catalog.SelectForEntry(
                    _family.GetSex(person),
                    _gameState.Year,
                    _random,
                    definition =>
                    {
                        if (!predicate(definition))
                            return 0;

                        var evaluation = _localOpportunities.Evaluate(
                            person,
                            definition.LocationRequirement);
                        return evaluation.IsEligible
                            ? evaluation.WeightMultiplier
                            : 0;
                    });
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        if (previous is not null
            && previous.RequiredOpportunityTags.Count > 0)
        {
            var tags = previous.RequiredOpportunityTags.ToHashSet(
                StringComparer.OrdinalIgnoreCase);
            var sameIndustry = TryPreferred(definition =>
                definition.RequiredOpportunityTags.Any(tags.Contains));
            if (sameIndustry is not null)
                return sameIndustry;
        }

        if (previous is not null)
        {
            var aptitude = CareerEntryAptitudeClassifier.Get(previous);
            var samePrimarySkill = TryPreferred(definition =>
                CareerEntryAptitudeClassifier.Get(definition) == aptitude);
            if (samePrimarySkill is not null)
                return samePrimarySkill;
        }

        return SelectCareerForEntry(person);
    }

    private CareerDefinition SelectCareerForEntry(
        IPerson person)
    {
        return _catalog.SelectForEntry(
            _family.GetSex(
                person),
            _gameState.Year,
            _random,
            definition =>
            {
                var evaluation =
                    _localOpportunities.Evaluate(
                        person,
                        definition.LocationRequirement);

                return evaluation.IsEligible
                    ? evaluation.WeightMultiplier
                    : 0;
            });
    }

    private bool IsLocallyAvailable(
        IPerson person,
        CareerDefinition definition)
    {
        return _localOpportunities
            .Evaluate(
                person,
                definition.LocationRequirement)
            .IsEligible;
    }

    private static string
        ResolveJobSatisfactionText(
            int value)
    {
        return value switch
        {
            1 => "Miserable",
            2 => "Unhappy",
            3 => "Content",
            4 => "Satisfied",
            5 => "Thriving",
            _ => string.Empty
        };
    }

    private CareerComponent GetRequired(
        IPerson person)
    {
        EnsureCareer(
            person);

        return person.Components.Get<
            CareerComponent>()
            ?? throw new InvalidOperationException(
                "Career component could not be created.");
    }
}
