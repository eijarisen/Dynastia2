using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class StandardCareerService
{
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

        _craftResolver()?.EndOccupation(
            person,
            "retirement");
    }

    public decimal GetLevelOneSalary(
        string careerId)
    {
        var definition = _catalog.Find(careerId);
        return definition?.BaseSalary ?? 0m;
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
                * _retirementRules
                    .GetRule(
                        _gameState.Year)
                    .PensionRate;
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

    private string? ResolvePeakJobTitle(
        CareerComponent career)
    {
        CareerDefinition? definition = null;
        var level = career.PeakJobLevel;

        if (level > 0
            && !string.IsNullOrWhiteSpace(
                career.PeakCareerId))
        {
            definition = _catalog.Find(
                career.PeakCareerId);
        }

        // Compatibility fallback for older retired saves that predate
        // peak-career tracking but still retain the career and last salary.
        if (definition is null)
        {
            definition = _catalog.Find(
                career.CareerId);

            if (career.IsRetired
                && definition is not null
                && definition.BaseSalary > 0
                && career.LastIncome > 0)
            {
                level = Math.Clamp(
                    (int)Math.Round(
                        career.LastIncome
                        / definition.BaseSalary),
                    1,
                    5);
            }
        }

        if (definition is null
            || level <= 0)
        {
            return null;
        }

        var baseTitle = definition.GetTitle(
            level);

        return _presentation.ResolveCareerTitle(
            definition.Id,
            level,
            baseTitle,
            _gameState.Year);
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
        var year = _gameState.Year;

        // Existing special-status precedence is intentionally preserved.
        if (person.Tags.Has(
            "role.nanny"))
        {
            return _presentation.ResolveStatus(
                "role.nanny",
                "Nanny",
                year);
        }

        if (person.Tags.Has(
            "state.imprisoned"))
        {
            return "Imprisoned";
        }

        if (career.IsRetired)
            return "Retired";

        if (person.Tags.Has(
            "role.family_nanny"))
        {
            return _presentation.ResolveStatus(
                "role.family_nanny",
                "Family Nanny",
                year);
        }

        if (_family.GetSex(
                person)
                == Sex.Female
            && career.JobLevel == 0
            && _family.GetChildren(
                person)
                .Count > 0)
        {
            return _presentation.ResolveStatus(
                "status.housewife",
                "Housewife",
                year);
        }

        if (person.Age < 6)
        {
            return _presentation.ResolveStatus(
                "status.preschool",
                "Preschool",
                year);
        }

        if (person.Age < 18)
        {
            return _presentation.ResolveStatus(
                "status.student",
                "Student",
                year);
        }

        if (career.JobLevel <= 0)
        {
            return _presentation.ResolveStatus(
                "status.unemployed",
                "Unemployed",
                year);
        }

        if (definition is not null)
        {
            var baseTitle = definition.GetTitle(
                career.JobLevel);

            return _presentation.ResolveCareerTitle(
                definition.Id,
                career.JobLevel,
                baseTitle,
                year);
        }

        return career.JobLevel switch
        {
            1 => "Laborer",
            2 => "Clerk",
            3 => "Manager",
            4 => "Director",
            5 => "Magnate",
            _ => _presentation.ResolveStatus(
                "status.unemployed",
                "Unemployed",
                year)
        };
    }

    private string? ResolveStatusId(
        IPerson person,
        CareerComponent career)
    {
        if (person.Tags.Has(
            "role.nanny"))
        {
            return "role.nanny";
        }

        if (person.Tags.Has(
                "state.imprisoned")
            || career.IsRetired)
        {
            return null;
        }

        if (person.Tags.Has(
            "role.family_nanny"))
        {
            return "role.family_nanny";
        }

        if (_family.GetSex(
                person)
                == Sex.Female
            && career.JobLevel == 0
            && _family.GetChildren(
                person)
                .Count > 0)
        {
            return "status.housewife";
        }

        if (person.Age < 6)
            return "status.preschool";

        if (person.Age < 18)
            return "status.student";

        return career.JobLevel <= 0
            ? "status.unemployed"
            : null;
    }

}
