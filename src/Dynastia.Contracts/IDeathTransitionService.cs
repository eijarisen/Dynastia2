namespace Dynastia.Contracts;

public interface IDeathTransitionService
{
    void Kill(
        IGameState gameState,
        IPerson person,
        string cause);
}
