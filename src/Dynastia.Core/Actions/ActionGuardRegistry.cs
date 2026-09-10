using Dynastia.Contracts;

namespace Dynastia.Core.Actions;

public sealed class ActionGuardRegistry :
    IActionGuardRegistry
{
    private readonly Dictionary<
        string,
        IActionGuard>
        _guards =
            new(
                StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IActionGuard> Guards =>
        _guards.Values.ToList();

    public void Register(
        IActionGuard guard)
    {
        ArgumentNullException.ThrowIfNull(
            guard);

        if (string.IsNullOrWhiteSpace(
            guard.Id))
        {
            throw new InvalidOperationException(
                "Action guard ID is required.");
        }

        if (!_guards.TryAdd(
            guard.Id,
            guard))
        {
            throw new InvalidOperationException(
                $"Action guard '{guard.Id}' " +
                "is already registered.");
        }
    }

    public ActionGuardResult Evaluate(
        IPerson actor)
    {
        ArgumentNullException.ThrowIfNull(
            actor);

        foreach (var guard in
            _guards.Values)
        {
            var result =
                guard.Evaluate(
                    actor);

            if (!result.Allowed)
                return result;
        }

        return new ActionGuardResult(
            true);
    }
}
