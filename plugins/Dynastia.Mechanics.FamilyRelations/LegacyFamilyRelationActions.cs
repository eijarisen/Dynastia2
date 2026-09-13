using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static class LegacyFamilyRelationActions
{
    private static readonly (string Id, string Label)[] Legacy =
    [
        ("family_support.ask_parents", "Ask Parents for Money"),
        ("family_support.ask_child", "Ask Child for Money"),
        ("household.ask_parents_house", "Ask Parents for a House"),
        ("household.ask_father_house", "Ask Father for a House"),
        ("household.ask_mother_house", "Ask Mother for a House"),
        ("career.use_family_connections", "Use Family Connections")
    ];

    public static void Register(IActionRegistry actions)
    {
        foreach (var (id, label) in Legacy)
        {
            actions.Register(new GameActionDefinition
            {
                Id = id,
                Label = label,
                Description = "Legacy queued action retained for save compatibility. Cross-household family assistance now uses Family Relations.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.FamilyRelationActions,
                IsAvailable = _ => false,
                Execute = _ => new GameActionResult(false)
            });
        }
    }
}
