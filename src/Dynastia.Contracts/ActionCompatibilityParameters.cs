namespace Dynastia.Contracts;

public static class ActionCompatibilityParameters
{
    public const string RestoredQueuedAction =
        "compatibility.restored_queued_action";

    public static bool IsRestoredQueuedAction(
        IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue(
            RestoredQueuedAction,
            out var value)
        && bool.TryParse(value, out var parsed)
        && parsed;
}
