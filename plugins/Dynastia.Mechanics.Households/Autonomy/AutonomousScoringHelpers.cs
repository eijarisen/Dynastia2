using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

/// <summary>Pure calculations shared by the autonomous scorers and snapshot builder.</summary>
internal static class AutonomousScoringHelpers
{
    internal static AutonomousActionCandidate WithScore(
        AutonomousActionCandidate option,
        AutonomyCategory category,
        int band,
        double score) =>
        option with
        {
            Category = category,
            PriorityBand = band,
            Score = score
        };

    internal static bool IsAtLeast(
        AutonomousFinancialState value,
        AutonomousFinancialState minimum) =>
        (int)value >= (int)minimum;

    internal static int GetStat(
        IReadOnlyDictionary<string, int> stats,
        string id) =>
        stats.TryGetValue(id, out var value) ? value : 0;
}
