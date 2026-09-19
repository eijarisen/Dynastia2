using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class StandardCareerService
{
    public bool TryFindBetterJob(
        IPerson person)
    {
        var current = GetRequired(person);
        if (current.IsRetired || current.JobLevel is < 1 or > 2)
            return false;

        var oldDefinition = ResolveDefinition(person, current);
        var oldSalary = GetActiveSalary(person, current);
        var opportunity = CreateEmploymentOpportunity(person, _stats);
        var offeredSalary = CareerBalanceRules.CalculateAnnualSalary(
            opportunity.Career.BaseSalary,
            current.JobLevel);

        if (oldDefinition is not null
            && opportunity.Career.Id.Equals(oldDefinition.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var successChance = opportunity.SuccessChance;

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
            || IsEmployed(person)
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

        var chance = Math.Clamp(
            opportunity.SuccessChance + chanceBonus,
            0,
            0.98);

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
            || IsEmployed(person))
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

    private CareerDefinition SelectRelocationReplacement(
        IPerson person,
        CareerDefinition? previous)
    {
        var desiredLevel = Math.Max(1, GetRequired(person).JobLevel);

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
                                * GetSelectionContextMultiplier(definition, person)
                                * GetAutomaticCareerFitMultiplier(
                                    definition,
                                    person,
                                    desiredLevel)
                            : 0;
                    });
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        if (previous is not null)
        {
            var sameFamily = TryPreferred(definition =>
                definition.CareerFamily.Equals(
                    previous.CareerFamily,
                    StringComparison.OrdinalIgnoreCase));
            if (sameFamily is not null)
                return sameFamily;
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

        return SelectCareerForEntry(person, desiredLevel);
    }

    private CareerDefinition SelectCareerForEntry(
        IPerson person,
        int desiredLevel = 1)
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
                        * GetSelectionContextMultiplier(definition, person)
                        * GetAutomaticCareerFitMultiplier(
                            definition,
                            person,
                            desiredLevel)
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

}
