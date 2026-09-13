using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string SelfImprovementUiActionId =
        "ui.self_improvement";

    private const decimal SelfImprovementCost =
        10000m;

    private static readonly SelfImprovementDefinition[]
        SelfImprovementDefinitions =
        [
            new(
                "stats.improve_strength",
                "strength",
                "Strength",
                "Gym Membership",
                "Intensive long-term physical training. Guaranteed Strength +1."),
            new(
                "stats.improve_intellect",
                "intellect",
                "Intellect",
                "Intelligence Training",
                "Private instruction and demanding mental exercises. Guaranteed Intellect +1."),
            new(
                "stats.improve_immunity",
                "immunity",
                "Immunity",
                "Immune Therapy",
                "Specialist medical treatment intended to strengthen resistance to illness. Guaranteed Immunity +1."),
            new(
                "stats.improve_appeal",
                "appeal",
                "Appeal",
                "Plastic Surgery",
                "Substantial cosmetic surgery. Guaranteed Appeal +1."),
            new(
                "stats.improve_longevity",
                "longevity",
                "Longevity",
                "Preventive Medicine",
                "Preventive medicine, specialist monitoring and rehabilitation. Guaranteed Longevity +1."),
            new(
                "stats.improve_fertility",
                "fertility",
                "Fertility",
                "Fertility Treatment",
                "Specialist fertility diagnosis and treatment. Guaranteed Fertility +1, including 0 → 1.")
        ];

    public IReadOnlyList<SelfImprovementOption> GetSelfImprovementOptions()
    {
        var actor =
            _succession.ActiveController;

        var target =
            FindSelectedPerson();

        if (actor is null
            || target is null
            || _statsService is null)
        {
            return [];
        }

        var availableActionIds =
            _actionRegistry
                .GetAvailableActions(
                    actor,
                    target)
                .Where(action =>
                    IsStatImprovementAction(action.Id))
                .Select(action =>
                    action.Id)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var stats =
            _statsService
                .GetStats(target)
                .ToDictionary(
                    stat => stat.Id,
                    stat => stat.Value,
                    StringComparer.OrdinalIgnoreCase);

        return SelfImprovementDefinitions
            .Select(definition =>
            {
                stats.TryGetValue(
                    definition.StatId,
                    out var currentValue);

                var isAvailable =
                    availableActionIds.Contains(
                        definition.ActionId);

                return new SelfImprovementOption(
                    definition.ActionId,
                    definition.StatName,
                    definition.ActionLabel,
                    currentValue,
                    SelfImprovementCost,
                    definition.Description,
                    isAvailable,
                    currentValue >= 5
                        ? "Maximum reached"
                        : isAvailable
                            ? "Available"
                            : "Unavailable");
            })
            .ToList();
    }

    public string GetSelfImprovementTargetName()
    {
        var target =
            FindSelectedPerson();

        if (target is null)
            return "selected person";

        return _familyService?.GetDisplayName(target)
            ?? $"{target.Name} {target.Surname}";
    }

    public void QueueSelfImprovementAction(
        string actionId)
    {
        var actor =
            _succession.ActiveController;

        var target =
            FindSelectedPerson();

        if (actor is null
            || target is null
            || !IsStatImprovementAction(actionId))
        {
            return;
        }

        var result =
            _actionRegistry.Execute(
                actionId,
                actor,
                target);

        if (!result.Success
            && !string.IsNullOrWhiteSpace(result.Message))
        {
            PersistenceStatusText =
                result.Message;
        }

        RefreshPeople();
        RefreshAlbum();
        RefreshHealth();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
        RefreshActions();
    }

    private static bool IsStatImprovementAction(
        string actionId) =>
        actionId.StartsWith(
            "stats.improve_",
            StringComparison.OrdinalIgnoreCase);

    private static GameActionDefinition
        CreateSelfImprovementPresentationAction() =>
        new()
        {
            Id = SelfImprovementUiActionId,
            Label = "Self Improvement",
            Description =
                "Choose one of the six personal skill improvements. Each costs 10,000 zł and uses this household's annual action.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

    private sealed record SelfImprovementDefinition(
        string ActionId,
        string StatId,
        string StatName,
        string ActionLabel,
        string Description);
}
