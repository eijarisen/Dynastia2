using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string SelfImprovementUiActionId =
        "ui.self_improvement";

    private const decimal SelfImprovementCost =
        20000m;

    private const decimal ReligiousStudyCost =
        3000m;

    private const string ReligiousStudyActionId =
        "personality.religious_study";

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
                    IsSelfImprovementAction(action.Id))
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

        var options = SelfImprovementDefinitions
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
                option.CurrentValue is int value
                && value < 5)
            .ToList();

        if (availableActions.TryGetValue(
                ReligiousStudyActionId,
                out var religiousStudy))
        {
            options.Add(
                new SelfImprovementOption(
                    ReligiousStudyActionId,
                    "Morals",
                    religiousStudy.Label,
                    null,
                    ReligiousStudyCost,
                    religiousStudy.Description,
                    true,
                    "Available"));
        }

        return options;
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
            || !IsSelfImprovementAction(actionId))
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

    private static bool IsSelfImprovementAction(
        string actionId) =>
        IsStatImprovementAction(actionId)
        || actionId.Equals(
            ReligiousStudyActionId,
            StringComparison.OrdinalIgnoreCase);

    private static GameActionDefinition
        CreateSelfImprovementPresentationAction() =>
        new()
        {
            Id = SelfImprovementUiActionId,
            Label = "Self Improvement",
            Description =
                "Choose personal training or Religious Study. Prices vary by option and each uses this household's annual action.",
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
