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
        5000m;

    private const decimal PrivateTutorUiCost =
        3000m;

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
                    $"{craft.Name} {craft.SelfEmploymentTitle} {mastery} profession",
                    LeadingEmoji: craft.Emoji);
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
        return target is null
            ? Array.Empty<PropertySelectionOption>()
            : GetEducationSelectionOptions(target);
    }

    internal IReadOnlyList<PropertySelectionOption> GetEducationSelectionOptions(
        IPerson target)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _educationService is null)
            return Array.Empty<PropertySelectionOption>();

        if (target.Age < 18)
        {
            if (target.Age < 6)
                return Array.Empty<PropertySelectionOption>();

            var childOptions = new List<PropertySelectionOption>();
            var currentLevel = _educationService.GetEducationLevel(target);

            var helpDefinition = _actionRegistry
                .GetCandidateActions(actor, target)
                .FirstOrDefault(action => action.Id.Equals(
                    "education.help_learning",
                    StringComparison.OrdinalIgnoreCase));
            if (helpDefinition is not null)
            {
                var evaluation = _actionRegistry.Evaluate(
                    helpDefinition.Id,
                    actor,
                    target);
                var helperLevel = _educationService.GetEducationLevel(actor);
                childOptions.Add(new PropertySelectionOption(
                    "help_learning",
                    helpDefinition.Label,
                    $"Current Education: Level {currentLevel} · Parent Education: Level {helperLevel}",
                    helpDefinition.Description,
                    "No cost",
                    "help learning child education parent study",
                    evaluation.Available,
                    LeadingEmoji: "📚"));
            }

            var tutorDefinition = _actionRegistry
                .GetCandidateActions(actor, target)
                .FirstOrDefault(action => action.Id.Equals(
                    "education.private_tutor",
                    StringComparison.OrdinalIgnoreCase));
            if (tutorDefinition is not null)
            {
                var evaluation = _actionRegistry.Evaluate(
                    tutorDefinition.Id,
                    actor,
                    target);
                var ceiling = _educationService.GetHelpedEducationCeiling(_gameState.Year);
                var chance = _educationService.GetPrivateTutorSuccessChance(target);
                childOptions.Add(new PropertySelectionOption(
                    "private_tutor",
                    tutorDefinition.Label,
                    $"Current Education: Level {currentLevel} · Tutoring cap: Level {ceiling}",
                    "Private tutoring is independent of local School quality and the parent's Education.",
                    $"{PrivateTutorUiCost:N0} zł",
                    "private tutor child education school",
                    evaluation.Available,
                    chance,
                    "🧑‍🏫"));
            }

            return childOptions;
        }

        var canAffordStandard =
            _economyService?.CanAfford(actor, StandardEducationUiCost) == true;
        var canAffordCraft =
            _economyService?.CanAfford(actor, CraftEducationUiCost) == true;

        var standardChance =
            _educationService.GetPaidEducationSuccessChance(target);
        var localEducationCeiling =
            _educationService.GetLocalEducationCeiling(target, _gameState.Year);

        var options = new List<PropertySelectionOption>
        {
            new(
                "standard",
                "Standard Education",
                $"Current formal Education: Level {_educationService.GetEducationLevel(target)} · Local School cap: Level {localEducationCeiling}",
                localEducationCeiling <= 0
                    ? "No ordinary local schooling is available."
                    : $"Ordinary local study can advance Education only through Level {localEducationCeiling}.",
                $"{StandardEducationUiCost:N0} zł",
                "standard education formal study",
                _educationService.GetEducationLevel(target) < localEducationCeiling
                    && canAffordStandard,
                standardChance,
                "🎓")
        };

        if (_craftService is null)
            return options;

        var regionalTags =
            _localCareerOpportunityService?
                .GetOpportunitySnapshot(target)
                .RegionOpportunityTags
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var craftOptions =
            new List<(
                PropertySelectionOption Option,
                bool HasRegionalSupport,
                bool IsKnownCraft,
                string CraftName)>();

        foreach (var craft in _craftService.GetEducationOptions(target))
        {
            var statName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(craft.PrimaryStat);
            var craftDefinition = _craftService.Catalog
                .FirstOrDefault(info => info.Id.Equals(
                    craft.CraftId,
                    StringComparison.OrdinalIgnoreCase));
            var emoji = craftDefinition?.Emoji
                ?? "🛠️";
            var heading = craft.IsKnownCraft
                ? $"{craft.CraftName} — {craft.CurrentMasteryName}"
                : $"Learn {craft.CraftName}";
            var supportTags = craftDefinition is null
                ? Array.Empty<string>()
                : craftDefinition.RequiredOpportunityTags.Count > 0
                    ? craftDefinition.RequiredOpportunityTags
                    : craftDefinition.PreferredOpportunityTags;
            var hasRegionalSupport =
                supportTags.Any(regionalTags.Contains);
            var supportText = supportTags.Count == 0
                ? "No specific regional industry"
                : hasRegionalSupport
                    ? "Regional support: Yes"
                    : "Regional support: No";
            var secondary =
                $"Requires: {statName} · {supportText}";

            craftOptions.Add((
                new PropertySelectionOption(
                    $"craft:{craft.CraftId}",
                    heading,
                    secondary,
                    string.Empty,
                    $"{CraftEducationUiCost:N0} zł",
                    $"{craft.CraftName} craft education {craft.CurrentMasteryName} {craft.PrimaryStat}",
                    canAffordCraft,
                    craft.SuccessChance,
                    emoji),
                hasRegionalSupport,
                craft.IsKnownCraft,
                craft.CraftName));
        }

        // Standard education remains the first choice. Among Crafts, put
        // locally supported industries first, then preserve the useful known-
        // craft preference before falling back to alphabetical order.
        options.AddRange(
            craftOptions
                .OrderByDescending(option => option.HasRegionalSupport)
                .ThenByDescending(option => option.IsKnownCraft)
                .ThenBy(option => option.CraftName, StringComparer.CurrentCultureIgnoreCase)
                .Select(option => option.Option));

        return options;
    }

    public string GetEducationSelectionContextText()
    {
        var target = FindSelectedPerson();
        return target is null
            ? string.Empty
            : GetEducationSelectionContextText(target);
    }

    internal string GetEducationSelectionContextText(IPerson target)
    {

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
        var target = FindSelectedPerson();
        if (target is null)
            return;

        QueueEducationAction(selectedOptionId, target);
    }

    internal void QueueEducationAction(
        string selectedOptionId,
        IPerson target)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        GameActionResult result;
        if (selectedOptionId.Equals(
                "help_learning",
                StringComparison.OrdinalIgnoreCase))
        {
            result = _actionRegistry.Execute(
                "education.help_learning",
                actor,
                target);
        }
        else if (selectedOptionId.Equals(
                     "private_tutor",
                     StringComparison.OrdinalIgnoreCase))
        {
            result = _actionRegistry.Execute(
                "education.private_tutor",
                actor,
                target);
        }
        else
        {
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

            result = _actionRegistry.Execute(
                "education.get_education",
                actor,
                target,
                parameters);
        }

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
