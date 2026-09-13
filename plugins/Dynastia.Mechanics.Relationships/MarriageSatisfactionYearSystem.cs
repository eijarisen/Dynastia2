using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class MarriageSatisfactionYearSystem :
    IYearSystem
{
    private const double NoIssuesRecovery =
        5;

    private const double HealthIssuePenalty =
        2;

    private const double LowFertilityPenalty =
        2;

    private const double LowFemaleAppealPenalty =
        6;

    private const double LowIntellectPenalty =
        5;

    private const double UnemployedHusbandPenalty =
        6;

    private const double HouseholdStrainPenalty =
        3;

    private const double BrokePenalty =
        15;

    private const double ImprisonmentPenalty =
        10;

    private const double AutomaticDivorceThreshold =
        35;

    private readonly StandardMarriageSatisfactionService
        _satisfaction;

    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly ICareerService _career;
    private readonly IHouseholdService _households;
    private readonly IGameRandom _random;
    private readonly RelationshipBreakupService _breakups;

    public MarriageSatisfactionYearSystem(
        StandardMarriageSatisfactionService satisfaction,
        IFamilyService family,
        IStatsService stats,
        IHealthService health,
        ICareerService career,
        IHouseholdService households,
        IGameRandom random,
        RelationshipBreakupService breakups)
    {
        _satisfaction = satisfaction;
        _family = family;
        _stats = stats;
        _health = health;
        _career = career;
        _households = households;
        _random = random;
        _breakups = breakups;
    }

    public string Id =>
        "relationships.marriage_satisfaction";

    public YearPhase Phase =>
        YearPhase.LifeEvents;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        [
            "reproduction.births",
            "relationships.female_remarriage"
        ];

    public void Execute(
        IGameState gameState)
    {
        var husbands =
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && _family.GetSex(
                            person)
                            == Sex.Male)
                .ToList();

        foreach (var husband in husbands)
        {
            var wife =
                _family.GetSpouse(
                    husband);

            if (wife is null
                || !wife.Tags.Has(
                    "state.alive")
                || _family.GetSex(
                    wife)
                    != Sex.Female)
            {
                continue;
            }

            var before =
                _satisfaction.GetSatisfaction(
                    husband);

            if (before is null)
                continue;

            // A marriage created during this year's life-event pass starts
            // high and gets a grace year before annual stress is applied.
            if (before.StartYear
                >= gameState.Year)
            {
                continue;
            }

            var issues =
                new List<string>();

            var penalty =
                CalculatePenalty(
                    husband,
                    wife,
                    issues);

            var change =
                issues.Count == 0
                    ? NoIssuesRecovery
                    : -penalty;

            _satisfaction.ApplyAnnualEvaluation(
                husband,
                wife,
                change,
                issues);

            var after =
                _satisfaction.GetSatisfaction(
                    husband);

            if (after is null
                || after.Value
                    >= AutomaticDivorceThreshold)
            {
                continue;
            }

            var divorceChance =
                RelationshipPersonalityRules.AdjustAutonomousDivorceChance(
                    ResolveAutomaticDivorceChance(
                        after.Value),
                    husband,
                    wife,
                    _stats);

            if (_random.NextDouble()
                >= divorceChance)
            {
                continue;
            }

            _breakups.LowSatisfactionDivorce(
                gameState,
                husband,
                wife,
                after.Value);
        }
    }

    private double CalculatePenalty(
        IPerson husband,
        IPerson wife,
        List<string> issues)
    {
        var total =
            0.0;

        if (HasHealthIssue(
            husband))
        {
            total +=
                HealthIssuePenalty;

            issues.Add(
                "husband's health");
        }

        if (HasHealthIssue(
            wife))
        {
            total +=
                HealthIssuePenalty;

            issues.Add(
                "wife's health");
        }

        var wifeFertility =
            GetStat(
                wife,
                "fertility");

        if (wifeFertility <= 2)
        {
            total +=
                LowFertilityPenalty;

            issues.Add(
                "low fertility");
        }

        var wifeAppeal =
            GetStat(
                wife,
                "appeal");

        if (wifeAppeal <= 2)
        {
            total +=
                LowFemaleAppealPenalty;

            issues.Add(
                "low appeal");
        }

        if (GetStat(
                husband,
                "intellect")
            <= 2)
        {
            total +=
                LowIntellectPenalty;

            issues.Add(
                "husband's low intellect");
        }

        if (GetStat(
                wife,
                "intellect")
            <= 2)
        {
            total +=
                LowIntellectPenalty;

            issues.Add(
                "wife's low intellect");
        }

        var husbandCareer =
            _career.GetCareer(
                husband);

        if (!husbandCareer.IsRetired
            && husband.Age >= 18
            && husbandCareer.JobLevel <= 0)
        {
            total +=
                UnemployedHusbandPenalty;

            issues.Add(
                "husband unemployed");
        }

        if (husband.Tags.Has("state.imprisoned")
            || wife.Tags.Has("state.imprisoned"))
        {
            total += ImprisonmentPenalty;
            issues.Add("imprisonment");
        }

        var householdHead =
            _households.ResolveHouseholdHead(
                husband)
            ?? _households.ResolveHouseholdHead(
                wife);

        var household =
            householdHead is null
                ? null
                : _households.GetStatus(
                    householdHead);

        if (household?.IsLargeFamilyStrained
            == true)
        {
            total +=
                HouseholdStrainPenalty;

            issues.Add(
                "household strain");
        }

        if (household?.IsBroke
            == true)
        {
            total +=
                BrokePenalty;

            issues.Add(
                "being broke");
        }

        return total;
    }

    private bool HasHealthIssue(
        IPerson person)
    {
        var health =
            _health.GetHealth(
                person);

        return health.Percentage < 60
            || health.Conditions.Count > 0;
    }

    private int GetStat(
        IPerson person,
        string id)
    {
        return _stats
            .GetStats(
                person)
            .First(
                stat =>
                    stat.Id.Equals(
                        id,
                        StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private static double
        ResolveAutomaticDivorceChance(
            double satisfaction)
    {
        return satisfaction switch
        {
            < 10 =>
                0.50,

            < 20 =>
                0.30,

            < 30 =>
                0.15,

            < 35 =>
                0.05,

            _ =>
                0
        };
    }
}
