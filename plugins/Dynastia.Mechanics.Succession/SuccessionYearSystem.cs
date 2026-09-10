using Dynastia.Contracts;

namespace Dynastia.Mechanics.Succession;

public sealed class SuccessionYearSystem : IYearSystem
{
    private readonly ISuccessionService _succession;

    public SuccessionYearSystem(
        ISuccessionService succession)
    {
        _succession =
            succession;
    }

    public string Id =>
        "succession.control_and_game_over";

    public YearPhase Phase =>
        YearPhase.Succession;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        _succession.Refresh();
    }
}
