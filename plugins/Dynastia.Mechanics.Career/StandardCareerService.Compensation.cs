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

}
