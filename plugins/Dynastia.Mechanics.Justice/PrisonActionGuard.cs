using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class PrisonActionGuard :
    IActionGuard
{
    private readonly IJusticeService _justice;

    public PrisonActionGuard(
        IJusticeService justice)
    {
        _justice = justice;
    }

    public string Id =>
        "justice.prison_action_lock";

    public ActionGuardResult Evaluate(
        IPerson actor)
    {
        var status =
            _justice.GetStatus(
                actor);

        if (!status.IsImprisoned)
        {
            return new ActionGuardResult(
                true);
        }

        var remaining =
            status.IsLifeSentence
                ? "life"
                : status.RemainingYears == 1
                    ? "1 year remaining"
                    : $"{status.RemainingYears} years remaining";

        return new ActionGuardResult(
            false,
            $"{actor.Name} is imprisoned " +
            $"({remaining}) and cannot perform actions.");
    }
}
