using Dynastia.Contracts;
using static Dynastia.Mechanics.Households.AutonomousScoringHelpers;

namespace Dynastia.Mechanics.Households;

/// <summary>Builds the existing planning snapshot in game-state enumeration order.</summary>
internal sealed class AutonomousSnapshotBuilder
{
    private readonly IGamePluginContext _context;
    private readonly IGameState _gameState;
    private readonly IHouseholdService _households;
    private readonly IEconomyService _economy;
    private readonly IHealthService _health;
    private readonly ICareerService _career;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly AutonomousReproductiveEligibility _reproductiveEligibility;

    public AutonomousSnapshotBuilder(
        IGamePluginContext context,
        IGameState gameState,
        IHouseholdService households,
        IEconomyService economy,
        IHealthService health,
        ICareerService career,
        IFamilyService family,
        IStatsService stats,
        AutonomousReproductiveEligibility reproductiveEligibility)
    {
        _context = context;
        _gameState = gameState;
        _households = households;
        _economy = economy;
        _health = health;
        _career = career;
        _family = family;
        _stats = stats;
        _reproductiveEligibility = reproductiveEligibility;
    }

    public AutonomousHouseholdSnapshot BuildSnapshot(
        HouseholdInfo household)
    {
        var head = FindPerson(household.HeadId)
            ?? throw new InvalidOperationException(
                $"Household {household.HouseholdId} has no head.");

        var memberIds = household.MemberIds.ToHashSet();
        memberIds.Add(head.Id);

        var members = _gameState.People
            .Where(person => memberIds.Contains(person.Id)
                && person.Tags.Has("state.alive"))
            .DistinctBy(person => person.Id)
            .Select(BuildMemberSnapshot)
            .ToList();

        if (!members.Any(member => member.Person.Id == head.Id))
            members.Insert(0, BuildMemberSnapshot(head));

        var finance = _economy.GetHousehold(head);
        var status = _households.GetStatus(head);
        var loans = _context.GetService<ILoanService>();

        var forecast = _economy.GetAnnualForecast(head);
        var projectedIncome = forecast?.ProjectedIncome ?? 0m;

        var debtPayments = loans?.GetDebts(head)
            .Sum(loan => loan.AnnualPayment) ?? 0m;

        var expectedExpenses = forecast?.ProjectedExpenses ?? 0m;
        if (finance is not null
            && finance.LastExpenses > expectedExpenses)
        {
            // Preserve the existing conservative autonomous-household policy:
            // a historically higher ordinary expense year remains the planning
            // floor, while current debt payments stay explicit. The underlying
            // expense formulas themselves now come only from Economy.
            expectedExpenses = finance.LastExpenses + debtPayments;
        }

        var wealth = finance?.Wealth ?? 0m;
        var financialState = AutonomousStrategyRules.GetFinancialState(
            wealth,
            projectedIncome,
            expectedExpenses);

        var spouse = _family.GetSpouse(head);
        if (spouse is not null && !spouse.Tags.Has("state.alive"))
            spouse = null;

        var livingChildren = _family.GetChildren(head)
            .Where(child => child.Tags.Has("state.alive"))
            .DistinctBy(child => child.Id)
            .ToList();

        var dependentChildren = livingChildren.Count(child => child.Age < 18);
        var reproductivePath = HasRealisticReproductivePath(head, spouse);
        var canTryForChild = CanActivelyTryForChild(
            head,
            spouse,
            livingChildren.Count,
            dependentChildren,
            financialState,
            status,
            reproductivePath);

        var marriageSatisfaction =
            spouse is null
                ? null
                : _context
                    .GetService<IMarriageSatisfactionService>()?
                    .GetSatisfactionBetween(head, spouse)?
                    .Value;

        var relatedHouseholds =
            _context.GetService<IFamilyRelationService>()?
                .GetRelatedHouseholds(head)
            ?? Array.Empty<RelatedFamilyHouseholdInfo>();

        var hasImmediateMedicalDanger = members.Any(member => member.IsImmediateHealthRisk);
        var hasSeriousMedicalDanger = members.Any(member => member.IsSeriousHealthRisk);

        return new AutonomousHouseholdSnapshot(
            household,
            head,
            finance,
            status,
            members,
            spouse,
            livingChildren,
            financialState,
            projectedIncome,
            expectedExpenses,
            debtPayments,
            hasImmediateMedicalDanger,
            hasSeriousMedicalDanger,
            finance?.Houses.Any(house => house.IsRented) == true,
            finance?.Houses.Any(house => house.IsResidence) == true,
            reproductivePath,
            canTryForChild,
            livingChildren.Count,
            dependentChildren,
            marriageSatisfaction,
            CalculateReproductiveUrgency(head, spouse, livingChildren.Count),
            relatedHouseholds);
    }

    private AutonomousMemberSnapshot BuildMemberSnapshot(
        IPerson person)
    {
        var health = _health.GetHealth(person);
        var career = person.Age >= 18 ? _career.GetCareer(person) : null;
        var stats = GetStats(person);
        var serious = IsSeriousHealthRisk(health);
        var immediate = IsImmediateHealthRisk(health);

        return new AutonomousMemberSnapshot(
            person,
            health,
            career,
            stats,
            person.Age < 18,
            person.Age < 18,
            serious,
            immediate);
    }

    private IReadOnlyDictionary<string, int> GetStats(IPerson person) =>
        _stats.GetStats(person)
            .ToDictionary(stat => stat.Id, stat => stat.Value,
                StringComparer.OrdinalIgnoreCase);

    private bool HasRealisticReproductivePath(
        IPerson head,
        IPerson? spouse)
    {
        if (spouse is null)
            return _reproductiveEligibility.CanSearchForReproductiveSpouse(head);

        var first = head;
        var second = spouse;
        var firstSex = _family.GetSex(first);
        var secondSex = _family.GetSex(second);

        if (firstSex == secondSex)
            return false;

        var female = firstSex == Sex.Female ? first : second;
        var male = firstSex == Sex.Male ? first : second;

        if (female.Age < 18 || female.Age > 45
            || female.Tags.Has("state.imprisoned"))
        {
            return false;
        }

        return GetStat(GetStats(female), "fertility") > 0
            && GetStat(GetStats(male), "fertility") > 0;
    }

    private bool CanActivelyTryForChild(
        IPerson head,
        IPerson? spouse,
        int livingChildren,
        int dependentChildren,
        AutonomousFinancialState financialState,
        HouseholdStatusSnapshot? status,
        bool reproductivePath)
    {
        if (spouse is null
            || _family.GetSex(head) != Sex.Male
            || _family.GetSex(spouse) != Sex.Female)
        {
            return false;
        }

        return AutonomousStrategyRules.CanActivelyTryForChild(
            livingChildren,
            financialState,
            status?.IsLargeFamilyStrained == true,
            dependentChildren,
            status?.EffectiveChildCapacity ?? int.MaxValue,
            reproductivePath);
    }

    private double CalculateReproductiveUrgency(
        IPerson head,
        IPerson? spouse,
        int livingChildren)
    {
        if (livingChildren >= 2)
            return 0;

        IPerson? female = null;
        if (_family.GetSex(head) == Sex.Female)
            female = head;
        else if (spouse is not null && _family.GetSex(spouse) == Sex.Female)
            female = spouse;

        var baseUrgency = livingChildren == 0 ? 1.0 : 0.55;
        if (female is null)
            return baseUrgency;

        if (female.Age >= 40)
            baseUrgency += 0.55;
        else if (female.Age >= 35)
            baseUrgency += 0.30;

        return Math.Min(1.5, baseUrgency);
    }

    private static bool IsSeriousHealthRisk(HealthSnapshot health) =>
        health.Percentage <= 55
        || health.Conditions.Any(condition =>
            condition.Type.Equals("terminal", StringComparison.OrdinalIgnoreCase)
            || condition.HealthImpact <= -10);

    private static bool IsImmediateHealthRisk(HealthSnapshot health) =>
        health.Percentage <= 40
        || health.Conditions.Any(condition =>
            condition.Type.Equals("terminal", StringComparison.OrdinalIgnoreCase)
                && health.Percentage <= 60
            || condition.HealthImpact <= -15
                && health.Percentage <= 45);

    private IPerson? FindPerson(Guid id) =>
        _gameState.People.FirstOrDefault(person => person.Id == id);
}
