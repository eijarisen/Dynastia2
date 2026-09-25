using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

/// <summary>
/// Maintains small persisted family-plan clocks and discretionary reservations.
/// It never executes mechanics or caches future world state.
/// </summary>
internal sealed class AutonomousPlanStateCoordinator
{
    internal const string AdultEstablishmentGoal = "adult_establishment";
    internal const string FirstChildGoal = "first_child";

    private readonly IGameState _gameState;
    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly ICareerService _career;

    public AutonomousPlanStateCoordinator(
        IGameState gameState,
        IEconomyService economy,
        IFamilyService family,
        ICareerService career)
    {
        _gameState = gameState;
        _economy = economy;
        _family = family;
        _career = career;
    }

    public AutonomousHouseholdSnapshot PrepareSnapshot(
        AutonomousHouseholdSnapshot snapshot)
    {
        ReconcilePendingAndCompletedPlans(snapshot);
        EnsureAdultPlans(snapshot);
        EnsureFirstChildPlan(snapshot);

        var plans = GetPlans(snapshot);
        // Reservations are earmarks for distinct live beneficiaries. Sum them
        // (bounded by cash actually owned) so the same money is not silently
        // promised to several sibling plans at once.
        var protectedReserve = plans
            .Where(plan => plan.ConsecutiveBlockedReassessments
                < AutonomousStrategyRules.BlockedReservationReleaseAssessments)
            .Sum(plan => Math.Max(0m, plan.ReservedCash));

        return snapshot with
        {
            ProtectedPlanReserve = Math.Min(
                snapshot.Finance?.Wealth ?? 0m,
                Math.Max(0m, protectedReserve)),
            HasProtectedFamilyPlan = protectedReserve > 0m
        };
    }

    public IReadOnlyList<AutonomousActionCandidate> AttachPlanMetadata(
        AutonomousHouseholdSnapshot snapshot,
        IReadOnlyList<AutonomousActionCandidate> scored)
    {
        var plans = GetPlans(snapshot);
        var hasEmergencyOrSolvency = scored.Any(candidate =>
            candidate.PriorityBand >= AutonomousPriorityBands.HouseholdSolvency);

        foreach (var plan in plans)
        {
            var candidates = scored
                .Where(candidate => BelongsToPlan(candidate, snapshot, plan))
                .ToList();

            if (candidates.Count > 0)
            {
                plan.FirstReadyYear ??= _gameState.Year;
                plan.ConsecutiveBlockedReassessments = 0;
                plan.BlockerCode = null;
                plan.RetryYear = null;
                plan.ReservedCash = Math.Min(
                    snapshot.Finance?.Wealth ?? 0m,
                    EstimatePlanReserve(snapshot, candidates));
            }
            else if (!hasEmergencyOrSolvency)
            {
                plan.ConsecutiveBlockedReassessments++;
                plan.BlockerCode = "no_executable_affordable_step";
                plan.RetryYear = _gameState.Year + 1;
                if (plan.ConsecutiveBlockedReassessments
                    >= AutonomousStrategyRules.BlockedReservationReleaseAssessments)
                {
                    plan.ReservedCash = 0m;
                }
            }
        }

        return scored.Select(candidate => AttachCandidatePlan(candidate, snapshot, plans)).ToList();
    }

    public void RecordQueuedSelection(
        AutonomousHouseholdSnapshot snapshot,
        AutonomousActionCandidate selected,
        IReadOnlyList<AutonomousActionCandidate> assessedCandidates)
    {
        var plans = GetPlans(snapshot);
        var selectedPlan = FindPlan(selected, snapshot, plans);
        var safeOpportunity = !assessedCandidates.Any(candidate =>
            candidate.PriorityBand >= AutonomousPriorityBands.HouseholdSolvency);

        foreach (var plan in plans)
        {
            var hasReadyStep = assessedCandidates.Any(candidate =>
                BelongsToPlan(candidate, snapshot, plan));
            if (!hasReadyStep || !safeOpportunity)
                continue;

            if (ReferenceEquals(plan, selectedPlan))
            {
                plan.LastServedYear = _gameState.Year;
                plan.MissedSafeOpportunities = 0;
                plan.PendingActionId = selected.Action.Id;
                plan.PendingActionYear = _gameState.Year;
                plan.PendingBaselineChildCount = snapshot.ExistingChildCount;
                plan.PendingBaselineSpouseId = _family.GetSpouse(selected.Target)?.Id;
                plan.PendingBaselineEmployed = _career.GetCareer(selected.Target).IsEmployed;
            }
            else
            {
                plan.MissedSafeOpportunities++;
            }
        }
    }

    public void RecordUnconsumedRejections(
        AutonomousHouseholdSnapshot snapshot,
        IReadOnlyList<AutonomousActionCandidate> rejected)
    {
        if (rejected.Count == 0)
            return;

        var plans = GetPlans(snapshot);
        foreach (var plan in plans)
        {
            if (!rejected.Any(candidate => BelongsToPlan(candidate, snapshot, plan)))
                continue;

            plan.BlockerCode = "queue_revalidation_rejected";
            plan.RetryYear = _gameState.Year + 1;
        }
    }

    public void ReleasePlansForHousehold(HouseholdInfo household)
    {
        foreach (var person in _gameState.People)
        {
            var plan = person.Components.Get<AutonomousFamilyPlanComponent>();
            if (plan?.SourceHouseholdId != household.HouseholdId)
                continue;

            person.Components.Remove<AutonomousFamilyPlanComponent>();
        }
    }

    private void ReconcilePendingAndCompletedPlans(
        AutonomousHouseholdSnapshot snapshot)
    {
        foreach (var member in snapshot.Members)
        {
            var person = member.Person;
            var plan = person.Components.Get<AutonomousFamilyPlanComponent>();
            if (plan is null || plan.SourceHouseholdId != snapshot.Household.HouseholdId)
                continue;

            if (plan.PendingActionYear is int pendingYear && pendingYear < _gameState.Year)
            {
                if (HasObservedProgress(person, snapshot, plan))
                    plan.LastProgressYear = pendingYear;

                plan.PendingActionId = null;
                plan.PendingActionYear = null;
                plan.PendingBaselineSpouseId = null;
            }

            if (PlanCompleted(person, snapshot, plan))
                person.Components.Remove<AutonomousFamilyPlanComponent>();
        }
    }

    private void EnsureAdultPlans(AutonomousHouseholdSnapshot snapshot)
    {
        var existingChildIds = snapshot.ExistingChildren.Select(child => child.Id).ToHashSet();
        var wealth = snapshot.Finance?.Wealth ?? 0m;

        foreach (var member in snapshot.Members)
        {
            var person = member.Person;
            if (!existingChildIds.Contains(person.Id)
                || person.Id == snapshot.Head.Id
                || person.Age < 18
                || !AutonomousReproductiveEligibility.CanParticipateInFamilyLife(person))
            {
                continue;
            }

            var plan = person.Components.Get<AutonomousFamilyPlanComponent>();
            if (plan is not null && plan.GoalId == AdultEstablishmentGoal
                && plan.SourceHouseholdId == snapshot.Household.HouseholdId)
            {
                continue;
            }

            person.Components.Set(new AutonomousFamilyPlanComponent
            {
                GoalId = AdultEstablishmentGoal,
                SourceHouseholdId = snapshot.Household.HouseholdId,
                BeneficiaryId = person.Id,
                CreatedYear = _gameState.Year,
                // Before a concrete next step is generated, reserve only the
                // household's basic continuity floor. AttachPlanMetadata will
                // replace this with the actual cheapest executable step.
                ReservedCash = Math.Min(
                    wealth,
                    snapshot.ExpectedExpenses * AutonomousStrategyRules.ForecastReserveFraction)
            });
        }
    }

    private void EnsureFirstChildPlan(AutonomousHouseholdSnapshot snapshot)
    {
        var matching = snapshot.Members
            .Select(member => (member.Person, Plan: member.Person.Components.Get<AutonomousFamilyPlanComponent>()))
            .Where(item => item.Plan is not null
                && item.Plan.GoalId == FirstChildGoal
                && item.Plan.SourceHouseholdId == snapshot.Household.HouseholdId)
            .OrderBy(item => item.Plan!.CreatedYear)
            .ToList();

        if (snapshot.ExistingChildCount > 0
            || !snapshot.NeedsFamilyExpansion
            || !snapshot.HasRealisticReproductivePath)
        {
            foreach (var item in matching)
                item.Person.Components.Remove<AutonomousFamilyPlanComponent>();
            return;
        }

        var headPlan = snapshot.Head.Components.Get<AutonomousFamilyPlanComponent>();
        if (headPlan is { GoalId: FirstChildGoal }
            && headPlan.SourceHouseholdId == snapshot.Household.HouseholdId)
        {
            return;
        }

        var inherited = matching.FirstOrDefault();
        if (inherited.Plan is not null)
        {
            inherited.Person.Components.Remove<AutonomousFamilyPlanComponent>();
            inherited.Plan.BeneficiaryId = snapshot.Head.Id;
            snapshot.Head.Components.Set(inherited.Plan);
            return;
        }

        snapshot.Head.Components.Set(new AutonomousFamilyPlanComponent
        {
            GoalId = FirstChildGoal,
            SourceHouseholdId = snapshot.Household.HouseholdId,
            BeneficiaryId = snapshot.Head.Id,
            CreatedYear = _gameState.Year,
            ReservedCash = Math.Min(
                snapshot.Finance?.Wealth ?? 0m,
                snapshot.ExpectedExpenses * AutonomousStrategyRules.ForecastReserveFraction)
        });
    }

    private bool HasObservedProgress(
        IPerson beneficiary,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousFamilyPlanComponent plan)
    {
        if (plan.GoalId == FirstChildGoal)
        {
            if (snapshot.ExistingChildCount > plan.PendingBaselineChildCount)
                return true;

            var spouseId = _family.GetSpouse(beneficiary)?.Id;
            if (spouseId is not null && spouseId != plan.PendingBaselineSpouseId)
                return true;

            return false;
        }

        var householdId = _economy.GetHouseholdId(beneficiary);
        if (householdId is not null && householdId != plan.SourceHouseholdId)
            return true;

        if (_family.GetSpouse(beneficiary) is { } spouse
            && spouse.Tags.Has("state.alive")
            && spouse.Id != plan.PendingBaselineSpouseId)
        {
            return true;
        }

        var employed = _career.GetCareer(beneficiary).IsEmployed;
        return employed && !plan.PendingBaselineEmployed;
    }

    private bool PlanCompleted(
        IPerson beneficiary,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousFamilyPlanComponent plan)
    {
        if (plan.GoalId == FirstChildGoal)
            return snapshot.ExistingChildCount > 0 || !snapshot.NeedsFamilyExpansion;

        if (plan.GoalId != AdultEstablishmentGoal)
            return true;

        var householdId = _economy.GetHouseholdId(beneficiary);
        return householdId is not null && householdId != plan.SourceHouseholdId;
    }

    private IReadOnlyList<AutonomousFamilyPlanComponent> GetPlans(
        AutonomousHouseholdSnapshot snapshot) =>
        snapshot.Members
            .Select(member => member.Person.Components.Get<AutonomousFamilyPlanComponent>())
            .Where(plan => plan is not null
                && plan.SourceHouseholdId == snapshot.Household.HouseholdId)
            .Cast<AutonomousFamilyPlanComponent>()
            .Distinct()
            .ToList();

    private AutonomousActionCandidate AttachCandidatePlan(
        AutonomousActionCandidate candidate,
        AutonomousHouseholdSnapshot snapshot,
        IReadOnlyList<AutonomousFamilyPlanComponent> plans)
    {
        var plan = FindPlan(candidate, snapshot, plans);
        if (plan is null)
            return candidate;

        var beneficiary = _gameState.People.FirstOrDefault(person => person.Id == plan.BeneficiaryId);
        return candidate with
        {
            PlanGoalId = plan.GoalId,
            PlanBeneficiaryId = plan.BeneficiaryId,
            PlanFirstReadyYear = plan.FirstReadyYear,
            PlanLastServedYear = plan.LastServedYear,
            PlanMissedSafeOpportunities = plan.MissedSafeOpportunities,
            PlanIsOverdue = plan.MissedSafeOpportunities
                >= AutonomousStrategyRules.OverdueAfterMissedSafeOpportunities,
            PlanDeadlineUrgency = CalculateDeadlineUrgency(plan, beneficiary, snapshot)
        };
    }

    private AutonomousFamilyPlanComponent? FindPlan(
        AutonomousActionCandidate candidate,
        AutonomousHouseholdSnapshot snapshot,
        IReadOnlyList<AutonomousFamilyPlanComponent> plans) =>
        plans.FirstOrDefault(plan => BelongsToPlan(candidate, snapshot, plan));

    private bool BelongsToPlan(
        AutonomousActionCandidate candidate,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousFamilyPlanComponent plan)
    {
        if (candidate.PriorityBand != AutonomousPriorityBands.SustainableFamilyContinuity)
            return false;

        if (plan.GoalId == AdultEstablishmentGoal)
            return candidate.Target.Id == plan.BeneficiaryId;

        if (plan.GoalId == FirstChildGoal)
        {
            return snapshot.ExistingChildCount == 0
                && (candidate.Target.Id == snapshot.Head.Id
                    || candidate.Target.Id == snapshot.Spouse?.Id);
        }

        return false;
    }

    private static decimal EstimatePlanReserve(
        AutonomousHouseholdSnapshot snapshot,
        IReadOnlyList<AutonomousActionCandidate> candidates)
    {
        var directCost = candidates
            .Select(EstimateActionCost)
            .DefaultIfEmpty(0m)
            .Min();
        var essentialReserve = snapshot.ExpectedExpenses
            * AutonomousStrategyRules.ForecastReserveFraction;
        return Math.Max(essentialReserve, directCost + essentialReserve);
    }

    private static decimal EstimateActionCost(AutonomousActionCandidate candidate)
    {
        var cost = candidate.Action.DisplayCost ?? 0m;
        foreach (var key in new[] { "houseAskingPrice", "farmlandAskingPrice", "amount" })
        {
            if (candidate.Parameters.TryGetValue(key, out var text)
                && decimal.TryParse(text, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                cost = Math.Max(cost, parsed);
            }
        }
        return Math.Max(0m, cost);
    }

    private int CalculateDeadlineUrgency(
        AutonomousFamilyPlanComponent plan,
        IPerson? beneficiary,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (plan.GoalId == FirstChildGoal)
        {
            var female = _family.GetSex(snapshot.Head) == Sex.Female
                ? snapshot.Head
                : snapshot.Spouse is not null && _family.GetSex(snapshot.Spouse) == Sex.Female
                    ? snapshot.Spouse
                    : null;
            return female is null ? 0 : Math.Max(0, female.Age - 34);
        }

        if (beneficiary is null || _family.GetSex(beneficiary) != Sex.Female)
            return 0;

        // Arranged-partner eligibility itself has no hard upper bound here.
        // This is therefore a modest urgency only for the attainable childbearing
        // window after establishment, never a permanent override.
        return Math.Max(0, beneficiary.Age - 34);
    }
}
