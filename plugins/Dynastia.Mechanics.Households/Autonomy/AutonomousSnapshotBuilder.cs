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

        // The canonical forecast already includes registered finance projection
        // providers (including scheduled debt). Historical spending is not an
        // eternal floor and debt must not be counted twice.
        var expectedExpenses = forecast?.ProjectedExpenses ?? 0m;

        var wealth = finance?.Wealth ?? 0m;
        var financialState = AutonomousStrategyRules.GetFinancialState(
            wealth,
            projectedIncome,
            expectedExpenses);

        var spouse = _family.GetSpouse(head);
        if (spouse is not null && !spouse.Tags.Has("state.alive"))
            spouse = null;

        var biologicalOrRecognizedChildren = _family.GetChildren(head)
            .Concat(spouse is null ? Array.Empty<IPerson>() : _family.GetChildren(spouse));
        var adoption = _context.GetService<IAdoptionService>();
        var parentIds = new HashSet<Guid> { head.Id };
        if (spouse is not null)
            parentIds.Add(spouse.Id);
        var adoptedChildren = adoption is null
            ? Array.Empty<IPerson>()
            : _gameState.People
                .Where(person =>
                {
                    var placement = adoption.GetPlacement(person);
                    return placement.Kind == AdoptionPlacementKind.AdoptiveHousehold
                        && placement.GuardianId is Guid guardianId
                        && parentIds.Contains(guardianId);
                })
                .ToArray();
        var livingChildren = biologicalOrRecognizedChildren
            .Concat(adoptedChildren)
            .Where(child => child.Tags.Has("state.alive") && !child.Tags.Has("state.dead"))
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

        // Count every dependent resident, including stepchildren, hosted adopted
        // children and supported grandchildren. Existing-child commitments are
        // tracked separately and never shrink because a child is ill, infertile,
        // adult, married or outside the male line.
        var dependentChildren = members.Count(member => member.IsDependent);
        var existingChildIds = livingChildren.Select(child => child.Id).ToHashSet();
        var reproductivePath = HasRealisticReproductivePath(head, spouse, StatsFor);
        var directBiologicalChildIds = _family.GetChildren(head)
            .Concat(spouse is null ? Array.Empty<IPerson>() : _family.GetChildren(spouse))
            .Select(child => child.Id)
            .ToHashSet();
        var hasEstablishedDescendantFamily = descendants.Any(descendant =>
            descendant.Tags.Has("state.alive")
            && (!directBiologicalChildIds.Contains(descendant.Id)
                || _family.GetSpouse(descendant) is { } descendantSpouse
                    && descendantSpouse.Tags.Has("state.alive")
                || _family.GetChildren(descendant).Any(child => child.Tags.Has("state.alive"))));
        var hasAdultFamilyFormationNeed = members.Any(member =>
            existingChildIds.Contains(member.Person.Id)
            && member.Person.Id != head.Id
            && member.Person.Age >= 18
            && AutonomousReproductiveEligibility.CanParticipateInFamilyLife(member.Person));
        var needsFamilyExpansion = livingChildren.Count < AutonomousStrategyRules.DeliberateExpansionSoftStop
            && !hasEstablishedDescendantFamily;
        var hasMaterialUnmetDependentNeed = members.Any(member =>
                member.IsDependent
                && (member.IsSeriousHealthRisk || member.Health.Percentage < 70))
            || status?.HasUnfundedBasicNeeds == true
            || status?.IsLargeFamilyStrained == true
            || status?.IsOvercrowded == true;

        // Use Economy's current local living-cost rule rather than estimating a
        // child from historic totals. The planner stress-tests two annual cycles
        // and protects a three-month essential reserve; actual cash is unchanged.
        var residenceTown = _economy.GetResidenceTown(head);
        var additionalChildCost = _economy.GetLivingCostPerPerson(residenceTown);
        var projectedExpensesWithChild = expectedExpenses + additionalChildCost;
        var hasSustainableExpansionBudget = AutonomousStrategyRules.HasSustainableExpansionBudget(
            wealth,
            projectedIncome,
            projectedExpensesWithChild);
        var canTryForChild = CanActivelyTryForChild(
            head,
            spouse,
            livingChildren.Count,
            dependentChildren,
            financialState,
            status,
            reproductivePath && needsFamilyExpansion && !hasAdultFamilyFormationNeed,
            hasMaterialUnmetDependentNeed,
            hasSustainableExpansionBudget);

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
            CalculateReproductiveUrgency(head, spouse, livingChildren.Count,
                needsFamilyExpansion),
            relatedHouseholds)
        {
            ExistingChildren = livingChildren,
            HasMaterialUnmetDependentNeed = hasMaterialUnmetDependentNeed,
            HasAdultFamilyFormationNeed = hasAdultFamilyFormationNeed,
            HasEstablishedDescendantFamily = hasEstablishedDescendantFamily,
            NeedsFamilyExpansion = needsFamilyExpansion,
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
        int existingChildren,
        int dependentChildren,
        AutonomousFinancialState financialState,
        HouseholdStatusSnapshot? status,
        bool reproductivePath,
        bool hasMaterialUnmetDependentNeed,
        bool hasSustainableBudget)
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
            existingChildren,
            financialState,
            hasMaterialUnmetDependentNeed,
            status?.IsLargeFamilyStrained == true,
            status?.IsOvercrowded == true,
            dependentChildren,
            status?.EffectiveChildCapacity ?? int.MaxValue,
            reproductivePath,
            hasSustainableBudget);
    }

    private double CalculateReproductiveUrgency(
        IPerson head,
        IPerson? spouse,
        int existingChildren,
        bool needsExpansion)
    {
        if (!needsExpansion)
            return 0;

        IPerson? female = null;
        if (_family.GetSex(head) == Sex.Female)
            female = head;
        else if (spouse is not null && _family.GetSex(spouse) == Sex.Female)
            female = spouse;

        var baseUrgency = existingChildren == 0 ? 1.0 : 0.55;
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
