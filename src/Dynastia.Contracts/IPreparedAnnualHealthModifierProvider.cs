namespace Dynastia.Contracts;

public interface IPreparedAnnualHealthModifierProvider
{
    void PrepareAnnualHealthContext(
        IGameState gameState);

    void ClearAnnualHealthContext();
}
