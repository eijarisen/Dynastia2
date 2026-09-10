namespace Dynastia.Contracts;

public interface IYearSystem
{
    string Id { get; }
    YearPhase Phase { get; }
    IReadOnlyCollection<string> Before { get; }
    IReadOnlyCollection<string> After { get; }
    void Execute(IGameState gameState);
}
