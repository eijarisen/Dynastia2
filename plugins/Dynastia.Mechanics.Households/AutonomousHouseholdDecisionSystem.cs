using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousHouseholdDecisionSystem :
    IYearSystem
{
    private readonly AutonomousHouseholdDecisionService
        _decisions;

    public AutonomousHouseholdDecisionSystem(
        AutonomousHouseholdDecisionService decisions)
    {
        _decisions = decisions;
    }

    public string Id =>
        "households.autonomous_decisions";

    public YearPhase Phase =>
        YearPhase.PreYear;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        _decisions.QueueActionsForAutonomousHouseholds();
    }
}
