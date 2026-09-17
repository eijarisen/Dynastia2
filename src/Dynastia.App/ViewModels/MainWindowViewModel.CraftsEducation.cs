using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string CraftProfessionUiActionId =
        "ui.craft_profession";

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
        var target = FindSelectedPerson();
        if (target is null || _educationService is null)
            return Array.Empty<PropertySelectionOption>();

        var options = new List<PropertySelectionOption>
        {
            new(
                "standard",
                "Standard Education",
                $"Current formal Education: Level {_educationService.GetEducationLevel(target)}",
                "Attempt to increase formal Education by one level. Success uses the existing Intellect-based education rules.",
                "3,000 zł",
                "standard education formal study",
                _educationService.GetEducationLevel(target) < 5)
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
            var secondary = craft.IsKnownCraft
                ? $"{craft.MasteryProgress:0.##} Mastery progress · {craft.RelevantExperienceYears} years relevant experience"
                : $"New chosen Craft · {statName} {craft.PrimaryStatValue}";
            var details = craft.IsKnownCraft
                ? $"Successful study adds 3 Mastery progress. Chance: {craft.SuccessChance:P0}. " +
                  "Higher tiers also require real relevant professional experience."
                : $"Unlock this Craft in the chosen slot. Chance: {craft.SuccessChance:P0}. " +
                  "Prior related Career experience is credited immediately on success.";

            options.Add(new PropertySelectionOption(
                $"craft:{craft.CraftId}",
                heading,
                secondary,
                details,
                "3,000 zł",
                $"{craft.CraftName} craft education {craft.CurrentMasteryName} {craft.PrimaryStat}"));
        }

        return options;
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
