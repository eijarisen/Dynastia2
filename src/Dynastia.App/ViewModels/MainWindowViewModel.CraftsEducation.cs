using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string CraftProfessionUiActionId =
        "ui.craft_profession";

    private const decimal StandardEducationUiCost =
        5000m;

    private const decimal CraftEducationUiCost =
        10000m;

    private static GameActionDefinition CreateCraftProfessionPresentationAction() =>
        new()
        {
            Id = CraftProfessionUiActionId,
            Label = "Work in a Profession",
            Description =
                "Choose one of the known Crafts to use as a self-employed profession.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

    public IReadOnlyList<PropertySelectionOption> GetCraftProfessionOptions()
    {
        var actor = _succession.ActiveController;
        var target = FindSelectedPerson();
        if (actor is null || target is null || _craftService is null)
            return Array.Empty<PropertySelectionOption>();

        var available = _actionRegistry
            .GetAvailableActions(actor, target)
            .Where(action => action.Id.StartsWith(
                "craft.start.",
                StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                action => action.Id["craft.start.".Length..],
                action => action.Id,
                StringComparer.OrdinalIgnoreCase);

        return _craftService.GetKnownCrafts(target)
            .Where(craft => available.ContainsKey(craft.Id))
            .Select(craft =>
            {
                var progress = _craftService.GetProgress(target, craft.Id);
                var mastery = progress?.MasteryName ?? "Novice";
                var years = progress?.RelevantExperienceYears ?? 0;
                var expected = progress?.ExpectedAnnualIncome ?? 0m;
                return new PropertySelectionOption(
                    available[craft.Id],
                    craft.Name,
                    $"{mastery} · {years} years relevant experience",
                    $"Become self-employed as {craft.SelfEmploymentTitle}.\n" +
                    $"Expected long-run income: about {expected:N0} zł/year. Income is highly variable.",
                    "Choose",
                    $"{craft.Name} {craft.SelfEmploymentTitle} {mastery} profession");
            })
            .OrderBy(option => option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void QueueCraftProfessionAction(string actionId)
    {
        var actor = _succession.ActiveController;
        var target = FindSelectedPerson();
        if (actor is null || target is null || _succession.IsGameOver)
            return;

        var craftId = actionId.StartsWith(
            "craft.start.",
            StringComparison.OrdinalIgnoreCase)
            ? actionId["craft.start.".Length..]
            : string.Empty;
        var craftName = _craftService?.GetKnownCrafts(target)
            .FirstOrDefault(craft => craft.Id.Equals(craftId, StringComparison.OrdinalIgnoreCase))?.Name;
        var parameters = string.IsNullOrWhiteSpace(craftName)
            ? null
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["summaryCraft"] = craftName
            };

        var result = _actionRegistry.Execute(actionId, actor, target, parameters);
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

        RefreshPeople();
        RefreshAlbum();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshActions();
    }

    public IReadOnlyList<PropertySelectionOption> GetEducationSelectionOptions()
    {
        var actor = _succession.ActiveController;
        var target = FindSelectedPerson();
        if (actor is null || target is null || _educationService is null)
            return Array.Empty<PropertySelectionOption>();

        var canAffordStandard =
            _economyService?.CanAfford(actor, StandardEducationUiCost) == true;
        var canAffordCraft =
            _economyService?.CanAfford(actor, CraftEducationUiCost) == true;

        var options = new List<PropertySelectionOption>
        {
            new(
                "standard",
                "🎓 Standard Education",
                $"Current formal Education: Level {_educationService.GetEducationLevel(target)}",
                string.Empty,
                $"{StandardEducationUiCost:N0} zł",
                "standard education formal study",
                _educationService.GetEducationLevel(target) < 5
                    && canAffordStandard)
        };

        if (_craftService is null)
            return options;

        foreach (var craft in _craftService.GetEducationOptions(target))
        {
            var statName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(craft.PrimaryStat);
            var emoji = _craftService.Catalog
                .FirstOrDefault(info => info.Id.Equals(
                    craft.CraftId,
                    StringComparison.OrdinalIgnoreCase))?.Emoji
                ?? "🛠️";
            var heading = craft.IsKnownCraft
                ? $"{emoji} {craft.CraftName} — {craft.CurrentMasteryName}"
                : $"{emoji} Learn {craft.CraftName}";
            var secondary =
                $"Requires: {statName} · Chance: {craft.SuccessChance:P0}";

            options.Add(new PropertySelectionOption(
                $"craft:{craft.CraftId}",
                heading,
                secondary,
                string.Empty,
                $"{CraftEducationUiCost:N0} zł",
                $"{craft.CraftName} craft education {craft.CurrentMasteryName} {craft.PrimaryStat}",
                canAffordCraft));
        }

        return options;
    }

    public string GetEducationSelectionContextText()
    {
        var target = FindSelectedPerson();
        if (target is null)
            return string.Empty;

        var displayName = _familyService?.GetDisplayName(target)
            ?? $"{target.Name} {target.Surname}";

        var stats = _statsService?.GetStats(target)
            .ToDictionary(stat => stat.Id, stat => stat.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var intellect = stats.GetValueOrDefault("intellect", 3);
        var strength = stats.GetValueOrDefault("strength", 3);

        return $"{displayName} · Intellect {intellect} · Strength {strength}";
    }

    public void QueueEducationAction(string selectedOptionId)
    {
        var actor = _succession.ActiveController;
        var target = FindSelectedPerson();
        if (actor is null || target is null || _succession.IsGameOver)
            return;

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["educationOption"] = selectedOptionId,
            ["summaryEducationOption"] = selectedOptionId.Equals(
                "standard",
                StringComparison.OrdinalIgnoreCase)
                ? "Standard Education"
                : _craftService?.GetEducationOptions(target)
                    .FirstOrDefault(option => selectedOptionId.Equals(
                        $"craft:{option.CraftId}",
                        StringComparison.OrdinalIgnoreCase))?.CraftName
                    ?? selectedOptionId
        };

        var result = _actionRegistry.Execute(
            "education.get_education",
            actor,
            target,
            parameters);

        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

        RefreshPeople();
        RefreshAlbum();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshActions();
    }

}
