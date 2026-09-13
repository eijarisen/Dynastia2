using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class MarriageSatisfactionYearSystem : IYearSystem
{
    private const double HealthIssuePenalty = 1.5;
    private const double LowFertilityPenalty = 1;
    private const double LowFemaleAppealPenalty = 0.5;
    private const double LowIntellectPenalty = 0.75;
    private const double UnemployedHusbandPenalty = 4;
    private const double HouseholdStrainPenalty = 2;
    private const double ImprisonmentPenalty = 8;

    private readonly StandardMarriageSatisfactionService _satisfaction;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly ICareerService _career;
    private readonly IHouseholdService _households;

    public MarriageSatisfactionYearSystem(
        StandardMarriageSatisfactionService satisfaction,
        IFamilyService family,
        IStatsService stats,
        IHealthService health,
        ICareerService career,
        IHouseholdService households)
    {
        _satisfaction = satisfaction;
        _family = family;
        _stats = stats;
        _health = health;
        _career = career;
        _households = households;
    }

    public string Id => "relationships.marriage_satisfaction";
    public YearPhase Phase => YearPhase.MarriageEvaluation;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        var husbands = gameState.People
            .Where(person =>
                person.Tags.Has("state.alive")
                && _family.GetSex(person) == Sex.Male)
            .ToList();

        foreach (var husband in husbands)
        {
            var wife = _family.GetSpouse(husband);
            if (wife is null
                || !wife.Tags.Has("state.alive")
                || _family.GetSex(wife) != Sex.Female)
            {
                continue;
            }

            var before = _satisfaction.GetSatisfaction(husband);
            if (before is null || before.StartYear >= gameState.Year)
                continue;

            var issues = new List<string>();
            var penalty = CalculatePenalty(husband, wife, issues);

            // Ordinary married life has a stabilizing baseline. Minor or
            // permanent disadvantages should not make every marriage decay
            // inexorably; only pressure that exceeds this recovery produces
            // a net annual decline.
            var change =
                MarriageBalanceRules.GetAnnualSatisfactionChange(
                    penalty);

            _satisfaction.ApplyAnnualEvaluation(
                husband,
                wife,
                change,
                issues);
        }
    }

    private double CalculatePenalty(
        IPerson husband,
        IPerson wife,
        List<string> issues)
    {
        var total = 0.0;

        if (HasHealthIssue(husband))
        {
            total += HealthIssuePenalty;
            issues.Add("husband's health");
        }

        if (HasHealthIssue(wife))
        {
            total += HealthIssuePenalty;
            issues.Add("wife's health");
        }

        if (GetStat(wife, "fertility") <= 2)
        {
            total += LowFertilityPenalty;
            issues.Add("low fertility");
        }

        if (GetStat(wife, "appeal") <= 2)
        {
            total += LowFemaleAppealPenalty;
            issues.Add("low appeal");
        }

        if (GetStat(husband, "intellect") <= 2)
        {
            total += LowIntellectPenalty;
            issues.Add("husband's low intellect");
        }

        if (GetStat(wife, "intellect") <= 2)
        {
            total += LowIntellectPenalty;
            issues.Add("wife's low intellect");
        }

        var husbandCareer = _career.GetCareer(husband);
        if (!husbandCareer.IsRetired
            && husband.Age >= 18
            && husbandCareer.JobLevel <= 0)
        {
            total += UnemployedHusbandPenalty;
            issues.Add("husband unemployed");
        }

        if (husband.Tags.Has("state.imprisoned")
            || wife.Tags.Has("state.imprisoned"))
        {
            total += ImprisonmentPenalty;
            issues.Add("imprisonment");
        }

        var householdHead =
            _households.ResolveHouseholdHead(husband)
            ?? _households.ResolveHouseholdHead(wife);

        var household = householdHead is null
            ? null
            : _households.GetStatus(householdHead);

        if (household?.IsLargeFamilyStrained == true)
        {
            total += HouseholdStrainPenalty;
            issues.Add("household strain");
        }

        if (household?.IsBroke == true)
        {
            var wifeCareer =
                _career.GetCareer(wife);

            var hasWorkingSpouse =
                husbandCareer.JobLevel > 0
                || wifeCareer.JobLevel > 0;

            total +=
                MarriageBalanceRules.GetFinancialPressurePenalty(
                    isBroke: true,
                    hasWorkingSpouse: hasWorkingSpouse);

            issues.Add("financial pressure");
        }

        return total;
    }

    private bool HasHealthIssue(IPerson person)
    {
        var health = _health.GetHealth(person);
        return health.Percentage < 60 || health.Conditions.Count > 0;
    }

    private int GetStat(IPerson person, string id) =>
        _stats.GetStats(person)
            .First(stat => stat.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .Value;
}
