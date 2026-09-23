using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static class LegacyFamilyRelationActions
{
    private static readonly (string Id, string Label, ActionPresentationMetadata Presentation)[] Legacy =
    [
        ("family_support.ask_parents", "Ask Parents for Money", new()
        {
            Emoji = "🙏",
            Categories = [ActionPresentationCategories.Family, ActionPresentationCategories.Finances]
        }),
        ("family_support.ask_child", "Ask Child for Money", new()
        {
            Emoji = "🙏",
            Categories = [ActionPresentationCategories.Family, ActionPresentationCategories.Finances]
        }),
        ("household.ask_parents_house", "Ask Parents for a House", new()
        {
            Emoji = "🙏",
            Categories = [ActionPresentationCategories.Finances]
        }),
        ("household.ask_father_house", "Ask Father for a House", new()
        {
            Emoji = "🙏",
            Categories = [ActionPresentationCategories.Finances]
        }),
        ("household.ask_mother_house", "Ask Mother for a House", new()
        {
            Emoji = "🙏",
            Categories = [ActionPresentationCategories.Finances]
        }),
        ("career.use_family_connections", "Use Family Connections", new()
        {
            Emoji = "🤝",
            Categories = [ActionPresentationCategories.Career],
            PlaceAfterAnchor = ActionPresentationGroups.EmploymentSearch
        })
    ];

    public static void Register(IActionRegistry actions)
    {
        foreach (var (id, label, presentation) in Legacy)
        {
            actions.Register(new GameActionDefinition
            {
                Id = id,
                Presentation = presentation,
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
