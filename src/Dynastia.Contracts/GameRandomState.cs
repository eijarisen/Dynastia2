namespace Dynastia.Contracts;

public sealed record GameRandomState(
    ulong State0,
    ulong State1);

public interface IStatefulGameRandom : IGameRandom
{
    GameRandomState CaptureState();
    void RestoreState(GameRandomState state);
}
