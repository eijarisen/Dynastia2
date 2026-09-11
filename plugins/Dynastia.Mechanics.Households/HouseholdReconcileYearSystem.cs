using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class HouseholdReconcileYearSystem :
    IYearSystem
{
    private readonly IHouseholdService _households;

    public HouseholdReconcileYearSystem(
        IHouseholdService households,
        string id,
        YearPhase phase,
        IReadOnlyCollection<string> before,
        IReadOnlyCollection<string> after)
    {
        _households =
            households;

        Id =
            id;

        Phase =
            phase;

        Before =
            before;

        After =
            after;
    }

    public string Id { get; }

    public YearPhase Phase { get; }

    public IReadOnlyCollection<string> Before { get; }

    public IReadOnlyCollection<string> After { get; }

    public void Execute(
        IGameState gameState)
    {
        _households.ReconcileHouseholds();
    }
}
