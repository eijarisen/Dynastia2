using Dynastia.Contracts;

namespace Dynastia.Mechanics.GameScore;

public sealed class GameScoreClaimReconcileYearSystem : IYearSystem
{
    private readonly StandardGameScoreService _score;
    public GameScoreClaimReconcileYearSystem(StandardGameScoreService score) => _score = score;
    public string Id => "game_score.claim_reconcile";
    public YearPhase Phase => YearPhase.PostYear;
    public IReadOnlyCollection<string> Before => [];
    public IReadOnlyCollection<string> After => [];
    public void Execute(IGameState gameState) => _score.ReconcileClaims();
}
