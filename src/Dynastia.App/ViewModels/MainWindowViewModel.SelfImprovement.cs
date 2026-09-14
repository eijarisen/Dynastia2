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
            new("stats.improve_strength", "strength", "Strength"),
            new("stats.improve_intellect", "intellect", "Intellect"),
            new("stats.improve_immunity", "immunity", "Immunity"),
            new("stats.improve_appeal", "appeal", "Appeal"),
            new("stats.improve_longevity", "longevity", "Longevity"),
            new("stats.improve_fertility", "fertility", "Fertility")
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

        var availableActions =
            _actionRegistry
                .GetAvailableActions(
                    actor,
                    target)
                .Where(action =>
                    IsStatImprovementAction(action.Id))
                .ToDictionary(
                    action => action.Id,
                    StringComparer.OrdinalIgnoreCase);

        var stats =
            _statsService
                .GetStats(target)
                .ToDictionary(
                    stat => stat.Id,
                    stat => stat.Value,
                    StringComparer.OrdinalIgnoreCase);

        return SelfImprovementDefinitions
            .Where(definition =>
                availableActions.ContainsKey(
                    definition.ActionId))
            .Select(definition =>
            {
                stats.TryGetValue(
                    definition.StatId,
                    out var currentValue);

                var action =
                    availableActions[definition.ActionId];

                return new SelfImprovementOption(
                    definition.ActionId,
                    definition.StatName,
                    action.Label,
                    currentValue,
                    SelfImprovementCost,
                    action.Description,
                    true,
                    "Available");
            })
            .Where(option =>
                option.CurrentValue < 5)
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
                "Choose one of the personal improvements currently available in this period. Each costs 10,000 zł and uses this household's annual action.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

    private sealed record SelfImprovementDefinition(
        string ActionId,
        string StatId,
        string StatName);
}
