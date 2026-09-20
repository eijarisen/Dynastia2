using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class MarriageSatisfactionYearSystem : IYearSystem
{
    private const double LowFertilityPenalty = 1;
    private const double LowIntellectPenalty = 0.75;
    private const double HouseholdStrainPenalty = 2;

    private readonly StandardMarriageSatisfactionService _satisfaction;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly ICareerService _career;
    private readonly IHouseholdService _households;
    private readonly IEconomyService _economy;
    private readonly IPersonalityService _personality;
    private readonly Func<IFamilyRelationService?> _familyRelationsResolver;

    public MarriageSatisfactionYearSystem(
        StandardMarriageSatisfactionService satisfaction,
        IFamilyService family,
        IStatsService stats,
        IHealthService health,
        ICareerService career,
        IHouseholdService households,
        IEconomyService economy,
        IPersonalityService personality,
        Func<IFamilyRelationService?> familyRelationsResolver)
    {
        _satisfaction = satisfaction;
        _family = family;
        _stats = stats;
        _health = health;
        _career = career;
        _households = households;
        _economy = economy;
        _personality = personality;
        _familyRelationsResolver = familyRelationsResolver;
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
            var penalty = CalculatePenalty(gameState, husband, wife, issues);

            // Ordinary life repairs only about one point per year. Ongoing
            // incompatibility and household/family stress therefore accumulate
            // unless the couple actively reconciles or their circumstances improve.
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
        IGameState gameState,
        IPerson husband,
        IPerson wife,
        List<string> issues)
    {
        var total = 0.0;

        var personalityPenalty =
            MarriageBalanceRules.GetPersonalityIncompatibilityPenalty(
                _personality.GetPersonality(husband),
                _personality.GetPersonality(wife));
        if (personalityPenalty > 0)
        {
            total += personalityPenalty;
            issues.Add("personality incompatibility");
        }

        var attractionPenalty =
            MarriageBalanceRules.GetLowAttractionPenalty(
                GetStat(husband, "appeal"),
                GetStat(wife, "appeal"));
        if (attractionPenalty > 0)
        {
            total += attractionPenalty;
            issues.Add("low attraction");
        }

        var husbandHealthPenalty =
            MarriageBalanceRules.GetSeriousIllnessPenalty(
                _health.GetHealth(husband));
        if (husbandHealthPenalty > 0)
        {
            total += husbandHealthPenalty;
            issues.Add("husband's serious illness");
        }

        var wifeHealthPenalty =
            MarriageBalanceRules.GetSeriousIllnessPenalty(
                _health.GetHealth(wife));
        if (wifeHealthPenalty > 0)
        {
            total += wifeHealthPenalty;
            issues.Add("wife's serious illness");
        }

        if (GetStat(wife, "fertility") <= 2)
        {
            total += LowFertilityPenalty;
            issues.Add("low fertility");
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
        var wifeCareer = _career.GetCareer(wife);
        var husbandImprisoned = husband.Tags.Has("state.imprisoned");
        var wifeImprisoned = wife.Tags.Has("state.imprisoned");

        // Imprisonment already subsumes the employment disruption it causes;
        // do not double-charge unemployment for an imprisoned spouse.
        if (!husbandImprisoned
            && !husbandCareer.IsRetired
            && husband.Age >= 18
            && !husbandCareer.IsEmployed)
        {
            total += MarriageBalanceRules.UnemployedHusbandPenalty;
            issues.Add("husband unemployed");
        }

        // A wife who previously had a career and has now lost employment adds
        // a smaller second unemployment pressure. A lifelong homemaker is not
        // treated as unemployed by this rule.
        if (!wifeImprisoned
            && !wifeCareer.IsRetired
            && wife.Age >= 18
            && !wifeCareer.IsEmployed
            && wifeCareer.PeakJobLevel > 0)
        {
            total += MarriageBalanceRules.UnemployedWorkingSpousePenalty;
            issues.Add("wife unemployed");
        }

        if (husbandImprisoned || wifeImprisoned)
        {
            total += MarriageBalanceRules.ImprisonmentPenalty;
            issues.Add(husbandImprisoned
                ? "husband imprisoned"
                : "wife imprisoned");
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
            var hasWorkingSpouse =
                husbandCareer.IsEmployed
                || wifeCareer.IsEmployed;

            total +=
                MarriageBalanceRules.GetFinancialPressurePenalty(
                    isBroke: true,
                    hasWorkingSpouse: hasWorkingSpouse);

            issues.Add("financial pressure");
        }

        var familyRelationsPenalty =
            GetPoorFamilyRelationsPenalty(
                gameState,
                husband,
                wife);
        if (familyRelationsPenalty > 0)
        {
            total += familyRelationsPenalty;
            issues.Add("poor family relations");
        }

        return total;
    }

    private double GetPoorFamilyRelationsPenalty(
        IGameState gameState,
        IPerson husband,
        IPerson wife)
    {
        var relations = _familyRelationsResolver();
        if (relations is null)
            return 0;

        var householdMemberIds =
            _economy.GetHouseholdMemberIds(husband)
                .Concat(_economy.GetHouseholdMemberIds(wife))
                .Where(id => id != husband.Id && id != wife.Id)
                .Distinct()
                .ToList();

        if (householdMemberIds.Count == 0)
            return 0;

        var people = gameState.People
            .ToDictionary(person => person.Id);
        var sympathy = new List<double>();

        foreach (var memberId in householdMemberIds)
        {
            if (!people.TryGetValue(memberId, out var member)
                || !member.Tags.Has("state.alive"))
            {
                continue;
            }

            var husbandRelation = relations.GetRelation(husband, member);
            if (husbandRelation is not null)
                sympathy.Add(husbandRelation.Sympathy);

            var wifeRelation = relations.GetRelation(wife, member);
            if (wifeRelation is not null)
                sympathy.Add(wifeRelation.Sympathy);
        }

        return MarriageBalanceRules.GetPoorFamilyRelationsPenalty(sympathy);
    }

    private int GetStat(IPerson person, string id) =>
        _stats.GetStats(person)
            .First(stat => stat.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .Value;
}
