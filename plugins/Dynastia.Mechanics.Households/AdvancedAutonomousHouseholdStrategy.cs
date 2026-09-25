using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

/// <summary>Coordinates autonomous planning; personality, final choice and queueing stay centralized.</summary>
internal sealed class AdvancedAutonomousHouseholdStrategy : IAutonomousHouseholdStrategy
{
    private readonly IActionRegistry _actions;
    private readonly IGameRandom _random;
    private readonly AutonomousSnapshotBuilder _snapshots;
    private readonly AutonomousActionCandidateBuilder _candidates;
    private readonly IReadOnlyList<IAutonomousActionScorer> _scorers;
    private readonly AutonomousGameScoreEstimator? _gameScore;

    public AdvancedAutonomousHouseholdStrategy(
        IActionRegistry actions,
        IGameRandom random,
        AutonomousSnapshotBuilder snapshots,
        AutonomousActionCandidateBuilder candidates,
        IReadOnlyList<IAutonomousActionScorer> scorers,
        AutonomousGameScoreEstimator? gameScore = null)
    {
        _actions = actions;
        _random = random;
        _snapshots = snapshots;
        _candidates = candidates;
        _scorers = scorers.ToArray();
        _gameScore = gameScore;
    }

    public AutonomousHouseholdSnapshot BuildSnapshot(
        HouseholdInfo household) =>
        _snapshots.BuildSnapshot(household);

    public IReadOnlyList<AutonomousActionCandidate> GetAvailableActions(
        AutonomousHouseholdSnapshot snapshot) =>
        _candidates.GetAvailableActions(snapshot);

    public AutonomousActionCandidate? ScoreAction(
        AutonomousActionCandidate action,
        AutonomousHouseholdSnapshot snapshot)
    {
        IAutonomousActionScorer? owner = null;
        foreach (var scorer in _scorers)
        {
            if (!scorer.Handles(action.Action.Id))
                continue;

            // Check all owners before scoring. An overlap must never silently
            // change priority, service calls or random consumption.
            if (owner is not null)
            {
                throw new InvalidOperationException(
                    $"Autonomous action '{action.Action.Id}' is claimed by both "
                    + $"{owner.GetType().Name} and {scorer.GetType().Name}.");
            }

            owner = scorer;
        }

        var scored = owner?.Score(action, snapshot);
        if (scored is null || scored.Score <= 0 || scored.PriorityBand <= 0)
            return null;

        var adjusted = ApplyPersonality(scored, snapshot.Head);
        return adjusted with
        {
            Score = Math.Clamp(adjusted.Score, 1, 150),
            LineagePriority = GetLineagePriority(adjusted, snapshot),
            ExpectedGameScore = adjusted.PriorityBand <= AutonomousPriorityBands.LongTermImprovement
                ? _gameScore?.Estimate(adjusted, snapshot) ?? 0 : 0
        };
    }

    public AutonomousActionCandidate? ChooseAction(
        IReadOnlyList<AutonomousActionCandidate> scoredActions)
    {
        if (scoredActions.Count == 0)
            return null;

        var highestBand = scoredActions.Max(action => action.PriorityBand);
        var eligible = scoredActions
            .Where(action => action.PriorityBand == highestBand)
            .ToList();

        if (highestBand == AutonomousPriorityBands.SustainableFamilyContinuity)
            eligible = ApplyFamilyPlanFairness(eligible);

        var inBand = eligible
            .OrderByDescending(action => action.Score)
            .ThenBy(action => action.Action.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(action => action.Target.Id)
            .ToList();
        if (inBand.Count == 0)
            return null;

        var bestScore = inBand[0].Score;

        // Emergency survival choices are ordered by concrete harm reduction.
        // Do not spend RNG on a weaker rescue when one intervention is already
        // scored as the better way to keep an existing household member alive.
        if (highestBand == AutonomousPriorityBands.EmergencySurvival)
            return inBand[0];

        // Succession may break only a genuine utility tie between otherwise
        // equivalent family-formation actions. It never upgrades ancestry into
        // a higher urgency or lets it outrank a better care/family option.
        if (highestBand == AutonomousPriorityBands.SustainableFamilyContinuity)
        {
            var exactBest = inBand
                .Where(action => Math.Abs(action.Score - bestScore) < 0.000001)
                .ToList();
            var bestLineage = exactBest.Max(action => action.LineagePriority);
            if (bestLineage > 0
                && exactBest.Any(action => action.LineagePriority < bestLineage))
            {
                return exactBest
                    .Where(action => action.LineagePriority == bestLineage)
                    .OrderBy(action => action.Action.Id, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(action => action.Target.Id)
                    .First();
            }
        }

        var competitive = inBand
            .Where(action => AutonomousStrategyRules.IsCloseEnoughToCompete(
                action.Score,
                bestScore))
            .ToList();

        // Score is a final ledger-aware preference only among already-safe,
        // near-equivalent development/optional choices. It cannot outbid the
        // household's ordinary utility or any higher survival/family band.
        if (highestBand <= AutonomousPriorityBands.LongTermImprovement)
        {
            var bestReward = competitive.Max(action => action.ExpectedGameScore);
            if (bestReward > 0)
            {
                competitive = competitive
                    .Where(action => action.ExpectedGameScore == bestReward)
                    .ToList();
            }
        }

        if (competitive.Count == 1)
            return competitive[0];

        var weighted = competitive
            .Select(action => (Action: action, Weight: Math.Pow(Math.Max(1.0, action.Score), 2)))
            .ToList();
        var total = weighted.Sum(item => item.Weight);
        var roll = _random.NextDouble() * total;
        foreach (var item in weighted)
        {
            roll -= item.Weight;
            if (roll < 0)
                return item.Action;
        }

        return weighted[^1].Action;
    }

    private static List<AutonomousActionCandidate> ApplyFamilyPlanFairness(
        List<AutonomousActionCandidate> candidates)
    {
        var planned = candidates.Where(candidate => candidate.PlanGoalId is not null).ToList();
        if (planned.Count == 0)
            return candidates;

        var maxDeadline = planned.Max(candidate => candidate.PlanDeadlineUrgency);
        if (maxDeadline > 0)
        {
            planned = planned
                .Where(candidate => candidate.PlanDeadlineUrgency == maxDeadline)
                .ToList();
        }

        var overdue = planned.Where(candidate => candidate.PlanIsOverdue).ToList();
        if (overdue.Count > 0)
            planned = overdue;

        var maxMissed = planned.Max(candidate => candidate.PlanMissedSafeOpportunities);
        planned = planned
            .Where(candidate => candidate.PlanMissedSafeOpportunities == maxMissed)
            .ToList();

        var readyYears = planned
            .Where(candidate => candidate.PlanFirstReadyYear is not null)
            .Select(candidate => candidate.PlanFirstReadyYear!.Value)
            .ToList();
        if (readyYears.Count > 0)
        {
            var oldestReady = readyYears.Min();
            planned = planned
                .Where(candidate => candidate.PlanFirstReadyYear == oldestReady)
                .ToList();
        }

        var oldestLastServed = planned.Min(candidate => candidate.PlanLastServedYear ?? int.MinValue);
        return planned
            .Where(candidate => (candidate.PlanLastServedYear ?? int.MinValue) == oldestLastServed)
            .ToList();
    }

    public bool QueueAction(
        AutonomousActionCandidate action,
        AutonomousHouseholdSnapshot snapshot)
    {
        return _actions.ExecuteAutonomous(
            action.Action.Id,
            snapshot.Head,
            action.Target,
            action.Parameters,
            snapshot.Household.HouseholdId).Success;
    }

    private static int GetLineagePriority(
        AutonomousActionCandidate action,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (action.PriorityBand != AutonomousPriorityBands.SustainableFamilyContinuity
            || action.Category is not (AutonomyCategory.Continuity or AutonomyCategory.FamilyRelations))
        {
            return 0;
        }

        // Only target-specific adult family formation can use succession as a
        // last tie-break. Medical care, child protection and conception never
        // receive ancestry priority.
        if (action.Action.Id is not ("relationship.marry_off_son"
            or "relationship.marry_off_daughter"
            or "household.ask_move_out"))
        {
            return 0;
        }

        var member = snapshot.Members.FirstOrDefault(candidate =>
            candidate.Person.Id == action.Target.Id);
        return member is { IsMaleLineage: true } ? 1 : 0;
    }

    private AutonomousActionCandidate ApplyPersonality(
        AutonomousActionCandidate option,
        IPerson head)
    {
        var id = option.Action.Id.ToLowerInvariant();
        double melancholic = 0;
        double phlegmatic = 0;
        double sanguine = 0;
        double choleric = 0;
        double good = 0;
        double evil = 0;

        if (id is "wellbeing.recover" or "wellbeing.therapy" or "relationship.repair_marriage")
            melancholic += 0.12;

        if (id is "career.seek_employment" or "career.find_another_job" or
            "career.work_harder" or "relationship.find_spouse")
            sanguine += 0.10;

        if (id is "career.work_harder" or "career.find_another_job" or "loan.take")
            choleric += 0.12;

        if (id is "turn.pass" or "relationship.repair_marriage")
            phlegmatic += 0.10;
        else if (id.StartsWith("career.", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("household.buy", StringComparison.OrdinalIgnoreCase))
            phlegmatic -= 0.08;

        if (id is "personality.religious_study" or
            "family_relations.give_money" or
            "family_relations.give_house" or
            "family_relations.give_farmland" or
            "family_relations.give_job_help")
        {
            good += 0.12;
            evil -= 0.08;
        }

        var multiplier = PersonalityInfluence.Multiplier(
            head,
            melancholic,
            phlegmatic,
            sanguine,
            choleric,
            good: good,
            evil: evil);

        return option with { Score = option.Score * multiplier };
    }
}
