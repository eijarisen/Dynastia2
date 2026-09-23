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

    public AdvancedAutonomousHouseholdStrategy(
        IActionRegistry actions,
        IGameRandom random,
        AutonomousSnapshotBuilder snapshots,
        AutonomousActionCandidateBuilder candidates,
        IReadOnlyList<IAutonomousActionScorer> scorers)
    {
        _actions = actions;
        _random = random;
        _snapshots = snapshots;
        _candidates = candidates;
        _scorers = scorers.ToArray();
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
            Score = Math.Clamp(adjusted.Score, 1, 150)
        };
    }

    public AutonomousActionCandidate? ChooseAction(
        IReadOnlyList<AutonomousActionCandidate> scoredActions)
    {
        if (scoredActions.Count == 0)
            return null;

        var highestBand = scoredActions.Max(action => action.PriorityBand);
        var inBand = scoredActions
            .Where(action => action.PriorityBand == highestBand)
            .OrderByDescending(action => action.Score)
            .ThenBy(action => action.Action.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(action => action.Target.Id)
            .ToList();

        if (inBand.Count == 0)
            return null;

        var best = inBand[0].Score;
        var competitive = inBand
            .Where(action => AutonomousStrategyRules.IsCloseEnoughToCompete(
                action.Score,
                best))
            .ToList();

        if (competitive.Count == 1)
            return competitive[0];

        var totalWeight = competitive.Sum(action => action.Score * action.Score);
        var roll = _random.NextDouble() * totalWeight;

        foreach (var action in competitive)
        {
            var weight = action.Score * action.Score;
            if (roll < weight)
                return action;

            roll -= weight;
        }

        return competitive[^1];
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
