namespace Dynastia.Contracts;

public interface IActionGuardRegistry
{
    IReadOnlyCollection<IActionGuard> Guards { get; }

    void Register(
        IActionGuard guard);

    ActionGuardResult Evaluate(
        IPerson actor);
}
