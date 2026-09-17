namespace Dynastia.Contracts;

public static class ActionReasonCodes
{
    public const string Available = "available";
    public const string UnknownAction = "unknown_action";
    public const string ActorMissing = "actor_missing";
    public const string TargetMissing = "target_missing";
    public const string ActorBlocked = "actor_blocked";
    public const string AlreadyQueued = "already_queued";
    public const string NoLongerEligible = "no_longer_eligible";
    public const string MechanicFailure = "mechanic_failure";
    public const string Executed = "executed";
}
