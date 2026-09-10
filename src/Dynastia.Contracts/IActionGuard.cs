namespace Dynastia.Contracts;

public interface IActionGuard
{
    string Id { get; }

    ActionGuardResult Evaluate(
        IPerson actor);
}
