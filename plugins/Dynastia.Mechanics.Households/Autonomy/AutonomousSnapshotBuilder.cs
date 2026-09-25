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

        // Read each person's planning stats and health once. Descendants living
        // in other households can secure the line just as resident children can.
        var statsByPerson = members.ToDictionary(member => member.Person.Id, member => member.Stats);
        var healthByPerson = members.ToDictionary(member => member.Person.Id, member => member.Health);
        IReadOnlyDictionary<string, int> StatsFor(IPerson person)
        {
            if (!statsByPerson.TryGetValue(person.Id, out var values))
                statsByPerson[person.Id] = values = GetStats(person);
            return values;
        }
        HealthSnapshot HealthFor(IPerson person)
        {
            if (!healthByPerson.TryGetValue(person.Id, out var health))
                healthByPerson[person.Id] = health = _health.GetHealth(person);
            return health;
        }

        var descendants = GetBiologicalDescendants(head, spouse);
        var bloodlineDescendants = descendants.Where(_family.IsBloodline).ToList();
        // The canonical lineage tag is assigned by Reproduction only to a son
        // of a male-line father. Sex or surname alone never establishes it.
        var maleLineDescendants = bloodlineDescendants
            .Where(person => _family.GetSex(person) == Sex.Male && _family.IsMaleLineage(person))
            .ToList();
        var viableBloodline = bloodlineDescendants
            .Where(person => HasViableContinuation(person, StatsFor, HealthFor))
            .Select(person => person.Id)
            .ToHashSet();
        var viableMaleLineCount = maleLineDescendants.Count(person => viableBloodline.Contains(person.Id));
        var hasSecuredMaleLine = viableMaleLineCount >= AutonomousStrategyRules.ContinuityBuffer;
        var hasSecuredBloodline = viableBloodline.Count >= AutonomousStrategyRules.ContinuityBuffer;
        var canExtendMaleLine = _family.GetSex(head) == Sex.Male && _family.IsMaleLineage(head);
        var canExtendBloodline = _family.IsBloodline(head)
            || spouse is not null && _family.IsBloodline(spouse);
        var needsMaleLine = canExtendMaleLine && !hasSecuredMaleLine;
        var needsBloodline = canExtendBloodline && !hasSecuredBloodline;

        // Count all dependent household children, including stepchildren and
        // supported grandchildren, when deciding whether another birth fits.
        var dependentChildren = members.Count(member => member.IsDependent);
        var reproductivePath = HasRealisticReproductivePath(head, spouse, StatsFor);
        var viableDescendants = needsMaleLine ? viableMaleLineCount : viableBloodline.Count;
        // Economy charges living costs per resident. Use its forecast so the
        // household's local prices, lifestyle and efficiency are retained.
        var livingCosts = forecast?.ExpenseBreakdown.FirstOrDefault(line =>
            line.Label.Equals("living costs", StringComparison.OrdinalIgnoreCase))?.Amount;
        var additionalChildCost = Math.Ceiling(
            Math.Max(0m, livingCosts ?? expectedExpenses) / Math.Max(1, members.Count));
        var canSupportAdditionalChild = projectedIncome >= expectedExpenses + additionalChildCost;
        var canTryForChild = CanActivelyTryForChild(
            head,
            spouse,
            viableDescendants,
            dependentChildren,
            financialState,
            status,
            reproductivePath && (needsMaleLine || needsBloodline) && canSupportAdditionalChild);

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
            CalculateReproductiveUrgency(head, spouse, viableDescendants,
                needsMaleLine || needsBloodline),
            relatedHouseholds)
        {
            LivingMaleLineDescendants = maleLineDescendants,
            LivingBloodlineDescendants = bloodlineDescendants,
            ViableMaleLineDescendantCount = viableMaleLineCount,
            ViableBloodlineDescendantCount = viableBloodline.Count,
            HasSecuredMaleLine = hasSecuredMaleLine,
            HasSecuredBloodline = hasSecuredBloodline,
            NeedsMaleLineContinuity = needsMaleLine,
            NeedsBloodlineContinuity = needsBloodline
        };
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
            immediate)
        {
            IsMaleLineage = _family.GetSex(person) == Sex.Male && _family.IsMaleLineage(person),
            IsBloodline = _family.IsBloodline(person)
        };
    }

    private IReadOnlyDictionary<string, int> GetStats(IPerson person) =>
        _stats.GetStats(person)
            .ToDictionary(stat => stat.Id, stat => stat.Value,
                StringComparer.OrdinalIgnoreCase);

    private bool HasRealisticReproductivePath(
        IPerson head,
        IPerson? spouse,
        Func<IPerson, IReadOnlyDictionary<string, int>> statsFor)
    {
        if (spouse is null)
            return _reproductiveEligibility.CanSearchForReproductiveSpouse(
                head, GetStat(statsFor(head), "fertility"));

        if (!AutonomousReproductiveEligibility.CanParticipateInFamilyLife(head)
            || !AutonomousReproductiveEligibility.CanParticipateInFamilyLife(spouse)
            || head.Age < 18
            || spouse.Age < 18
            || _family.GetSpouse(head)?.Id != spouse.Id
            || _family.GetSpouse(spouse)?.Id != head.Id)
        {
            return false;
        }

        var householdId = _economy.GetHouseholdId(head);
        if (householdId is null || _economy.GetHouseholdId(spouse) != householdId)
            return false;

        var first = head;
        var second = spouse;
        var firstSex = _family.GetSex(first);
        var secondSex = _family.GetSex(second);

        if (firstSex == secondSex)
            return false;

        var female = firstSex == Sex.Female ? first : second;
        var male = firstSex == Sex.Male ? first : second;

        if (female.Age > 45)
        {
            return false;
        }

        return GetStat(statsFor(female), "fertility") > 0
            && GetStat(statsFor(male), "fertility") > 0;
    }

    private bool CanActivelyTryForChild(
        IPerson head,
        IPerson? spouse,
        int viableDescendants,
        int dependentChildren,
        AutonomousFinancialState financialState,
        HouseholdStatusSnapshot? status,
        bool reproductivePath)
    {
        if (spouse is null
            || _family.GetSex(head) != Sex.Male
            || _family.GetSex(spouse) != Sex.Female
            || status?.IsOvercrowded == true
            || status?.HasUnfundedBasicNeeds == true
            || status is not null
                && status.ResidentCount + 1 > status.OvercrowdingThreshold)
        {
            return false;
        }

        return AutonomousStrategyRules.CanActivelyTryForChild(
            viableDescendants,
            financialState,
            status?.IsLargeFamilyStrained == true,
            dependentChildren,
            status?.EffectiveChildCapacity ?? int.MaxValue,
            reproductivePath);
    }

    private double CalculateReproductiveUrgency(
        IPerson head,
        IPerson? spouse,
        int viableDescendants,
        bool needsContinuity)
    {
        if (!needsContinuity)
            return 0;

        IPerson? female = null;
        if (_family.GetSex(head) == Sex.Female)
            female = head;
        else if (spouse is not null && _family.GetSex(spouse) == Sex.Female)
            female = spouse;

        var baseUrgency = viableDescendants == 0 ? 1.0 : 0.55;
        if (female is null)
            return baseUrgency;

        if (female.Age >= 40)
            baseUrgency += 0.55;
        else if (female.Age >= 35)
            baseUrgency += 0.30;

        return Math.Min(1.5, baseUrgency);
    }

    private IReadOnlyList<IPerson> GetBiologicalDescendants(IPerson head, IPerson? spouse)
    {
        var seen = new HashSet<Guid> { head.Id };
        var pending = new Queue<IPerson>();
        pending.Enqueue(head);
        if (spouse is not null && seen.Add(spouse.Id))
            pending.Enqueue(spouse);
        var living = new List<IPerson>();

        while (pending.TryDequeue(out var parent))
        {
            foreach (var child in _family.GetChildren(parent))
            {
                if ((_family.GetFather(child)?.Id != parent.Id
                        && _family.GetMother(child)?.Id != parent.Id)
                    || !seen.Add(child.Id))
                {
                    continue;
                }

                // A deceased child may have living children of their own.
                pending.Enqueue(child);
                if (child.Tags.Has("state.alive") && !child.Tags.Has("state.dead"))
                    living.Add(child);
            }
        }

        return living;
    }

    private bool HasViableContinuation(
        IPerson person,
        Func<IPerson, IReadOnlyDictionary<string, int>> statsFor,
        Func<IPerson, HealthSnapshot> healthFor)
    {
        if (!AutonomousReproductiveEligibility.CanParticipateInFamilyLife(person)
            || IsSeriousHealthRisk(healthFor(person)))
        {
            return false;
        }

        // Children are future heirs, not current marriage candidates. Do not
        // apply adult age-gap or sexuality rules before their coming of age.
        if (person.Age < 18)
            return GetStat(statsFor(person), "fertility") > 0;

        // A surviving elderly relative is not a dependable replacement for a
        // younger generation, even while the person still counts as alive.
        if (person.Age >= 60
            || _family.GetSex(person) == Sex.Female && person.Age > 40)
        {
            return false;
        }

        var spouse = _family.GetSpouse(person);
        if (spouse is not null && spouse.Tags.Has("state.alive"))
            return HasRealisticReproductivePath(person, spouse, statsFor);

        if (_family.GetSex(person) == Sex.Male)
            return _reproductiveEligibility.CanSearchForReproductiveSpouse(
                person, GetStat(statsFor(person), "fertility"));

        return GetStat(statsFor(person), "fertility") > 0
            && !person.Tags.Has("sexuality.homosexual");
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
