using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class ThoughtYearSystem :
    IYearSystem
{
    private readonly StandardThoughtService _thoughts;

    public ThoughtYearSystem(
        StandardThoughtService thoughts)
    {
        _thoughts =
            thoughts;
    }

    public string Id =>
        "thoughts.generate";

    public YearPhase Phase =>
        YearPhase.Thoughts;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        _thoughts.GenerateAnnualThoughts();
    }
}
