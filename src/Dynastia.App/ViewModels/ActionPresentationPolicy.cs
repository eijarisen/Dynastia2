using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

internal static class ActionPresentationPolicy
{
    public static IReadOnlySet<ActionCategory> GetCategories(
        string actionId)
    {
        var categories =
            new HashSet<ActionCategory>();

        if (actionId.Equals(
            "turn.pass",
            StringComparison.OrdinalIgnoreCase))
        {
            return categories;
        }

        if (actionId.StartsWith(
            "stats.",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(
                ActionCategory.Personal);

            return categories;
        }

        if (actionId.Equals(
            "wellbeing.recover",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.UnionWith(
                new[]
                {
                    ActionCategory.Personal,
                    ActionCategory.Career
                });

            return categories;
        }

        if (actionId.Equals(
            "career.work_harder",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.UnionWith(
                new[]
                {
                    ActionCategory.Career,
                    ActionCategory.Finances
                });

            return categories;
        }

        if (actionId.Equals(
            "career.ask_to_recover",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.UnionWith(
                new[]
                {
                    ActionCategory.Personal,
                    ActionCategory.Career,
                    ActionCategory.Family,
                    ActionCategory.Finances
                });

            return categories;
        }

        if (actionId.StartsWith(
            "wellbeing.",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(
                ActionCategory.Personal);

            if (actionId.Equals(
                    "wellbeing.therapy",
                    StringComparison.OrdinalIgnoreCase))
            {
                categories.Add(
                    ActionCategory.Family);
            }

            return categories;
        }

        if (actionId.StartsWith(
            "education.",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(
                ActionCategory.Career);

            if (actionId.Equals(
                    "education.help_learning",
                    StringComparison.OrdinalIgnoreCase)
                || actionId.Equals(
                    "education.private_tutor",
                    StringComparison.OrdinalIgnoreCase))
            {
                categories.Add(
                    ActionCategory.Family);
            }

            return categories;
        }

        if (actionId.Equals(
                "justice.commit_crime",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "justice.leave_life_of_crime",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "justice.ask_to_quit_crime",
                StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(ActionCategory.Career);
            if (actionId.Equals(
                    "justice.ask_to_quit_crime",
                    StringComparison.OrdinalIgnoreCase))
            {
                categories.Add(ActionCategory.Family);
            }
            return categories;
        }

        if (actionId.StartsWith(
            "career.",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(
                ActionCategory.Career);

            if (actionId.Equals(
                    "career.help_seek_employment",
                    StringComparison.OrdinalIgnoreCase)
                || actionId.Equals(
                    "career.help_find_better_job",
                    StringComparison.OrdinalIgnoreCase)
                || actionId.Equals(
                    "career.ask_to_quit",
                    StringComparison.OrdinalIgnoreCase))
            {
                categories.Add(
                    ActionCategory.Family);
            }

            return categories;
        }

        if (actionId.StartsWith(
                "relationship.",
                StringComparison.OrdinalIgnoreCase)
            || actionId.StartsWith(
                "reproduction.",
                StringComparison.OrdinalIgnoreCase)
            || actionId.StartsWith(
                "childhood.",
                StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(
                ActionCategory.Family);

            return categories;
        }

        if (actionId.Equals(
            "family.adopt_polish_surname",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(ActionCategory.Family);
            return categories;
        }

        if (actionId.StartsWith(
            "family_support.",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.UnionWith(
                new[]
                {
                    ActionCategory.Family,
                    ActionCategory.Finances
                });

            return categories;
        }

        if (actionId.StartsWith(
            "craft.teach.",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.UnionWith(
                new[]
                {
                    ActionCategory.Family,
                    ActionCategory.Skills
                });

            return categories;
        }

        if (actionId.StartsWith(
                "craft.start.",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "craft.stop_occupation",
                StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(
                ActionCategory.Career);

            return categories;
        }

        if (actionId.StartsWith(
            "farming.",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(
                ActionCategory.Finances);

            return categories;
        }

        if (actionId.StartsWith(
            "loan.",
            StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(
                ActionCategory.Finances);

            return categories;
        }

        if (actionId.StartsWith(
            "household.",
            StringComparison.OrdinalIgnoreCase))
        {
            if (actionId.Contains(
                    "nanny",
                    StringComparison.OrdinalIgnoreCase)
                || actionId.Equals(
                    "household.ask_move_out",
                    StringComparison.OrdinalIgnoreCase))
            {
                categories.Add(
                    ActionCategory.Family);
            }
            else
            {
                categories.Add(
                    ActionCategory.Finances);

                if (actionId.Equals(
                    "household.give_house_to_son",
                    StringComparison.OrdinalIgnoreCase))
                {
                    categories.Add(
                        ActionCategory.Family);
                }
            }

            return categories;
        }

        // Keep future uncategorized actions reachable instead of hiding them.
        categories.Add(
            ActionCategory.Personal);

        return categories;
    }

    public static ActionPresentationMetadata Resolve(GameActionDefinition definition)
    {
        if (definition.Presentation is { } presentation
            && !ReferenceEquals(presentation, ActionPresentationMetadata.Empty))
        {
            return presentation;
        }

        var legacy = LegacyPlacement.TryGetValue(definition.Id, out var placement)
            ? placement
            : ActionPresentationMetadata.Empty;
        return legacy with
        {
            Categories = GetCategories(definition.Id)
                .Select(category => category.ToString().ToLowerInvariant()).ToArray(),
            ShowInPrimaryActionList = !definition.Id.Equals("church.attend", StringComparison.OrdinalIgnoreCase)
                && !definition.Id.Equals("personality.religious_study", StringComparison.OrdinalIgnoreCase)
        };
    }

    public static IReadOnlySet<ActionCategory> GetCategories(GameActionDefinition definition)
    {
        var metadata = Resolve(definition);
        var categories = new HashSet<ActionCategory>();
        foreach (var id in metadata.Categories)
        {
            if (CategoryIds.TryGetValue(id, out var category))
                categories.Add(category);
        }

        // Unknown category IDs must not make a third-party action unreachable.
        if (categories.Count == 0 && metadata.Categories.Count > 0)
            categories.Add(ActionCategory.Personal);
        return categories;
    }

    public static IReadOnlyList<GameActionDefinition> Order(
        IReadOnlyList<GameActionDefinition> source)
    {
        var indexed = source.Select((definition, index) =>
            new PresentedAction(definition, Resolve(definition), index)).ToArray();
        var groups = indexed
            .Where(action => !string.IsNullOrWhiteSpace(action.Presentation.AdjacencyGroup))
            .GroupBy(action => action.Presentation.AdjacencyGroup!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key,
                group => group.OrderBy(action => action.Presentation.GroupOrder)
                    .ThenBy(action => action.OriginalIndex).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var actions = new List<PresentedAction>(indexed.Length);

        // Emit each group at its first original occurrence; unrelated actions keep
        // their registry order. This is not a global numeric sort.
        foreach (var action in indexed)
        {
            var group = action.Presentation.AdjacencyGroup;
            if (string.IsNullOrWhiteSpace(group))
                actions.Add(action);
            else if (emitted.Add(group))
                actions.AddRange(groups[group]);
        }

        // Family Connections follows the first self/assisted job search. An
        // absent anchor places the followers at the end, before PlaceLast items.
        // Both ends are metadata-driven, including for third-party actions.
        var followers = indexed
            .Where(action => !string.IsNullOrWhiteSpace(action.Presentation.PlaceAfterAnchor))
            .GroupBy(action => action.Presentation.PlaceAfterAnchor!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Min(action => action.OriginalIndex));
        foreach (var group in followers)
        {
            var members = group.OrderBy(action => action.Presentation.GroupOrder)
                .ThenBy(action => action.OriginalIndex).ToArray();
            foreach (var member in members)
                actions.Remove(member);
            var anchorIndex = actions.FindIndex(action => string.Equals(
                action.Presentation.PlacementAnchor, group.Key, StringComparison.OrdinalIgnoreCase));
            actions.InsertRange(anchorIndex >= 0 ? anchorIndex + 1 : actions.Count, members);
        }

        return actions.Where(action => !action.Presentation.PlaceLast)
            .Concat(actions.Where(action => action.Presentation.PlaceLast))
            .Select(action => action.Definition).ToArray();
    }

    private sealed record PresentedAction(
        GameActionDefinition Definition,
        ActionPresentationMetadata Presentation,
        int OriginalIndex);

    private static readonly IReadOnlyDictionary<string, ActionCategory> CategoryIds =
        new Dictionary<string, ActionCategory>(StringComparer.OrdinalIgnoreCase)
        {
            [ActionPresentationCategories.Personal] = ActionCategory.Personal,
            [ActionPresentationCategories.Career] = ActionCategory.Career,
            [ActionPresentationCategories.Family] = ActionCategory.Family,
            [ActionPresentationCategories.Finances] = ActionCategory.Finances,
            [ActionPresentationCategories.Skills] = ActionCategory.Skills
        };

    // Frozen compatibility table. First-party definitions own their presentation;
    // new metadata-enabled actions require no addition here or in ActionEmojiMap.
    private static readonly IReadOnlyDictionary<string, ActionPresentationMetadata> LegacyPlacement =
        new Dictionary<string, ActionPresentationMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["wellbeing.recover"] = new() { AdjacencyGroup = ActionPresentationGroups.CareerWork, GroupOrder = 10 },
            ["career.work_harder"] = new() { AdjacencyGroup = ActionPresentationGroups.CareerWork, GroupOrder = 20 },
            ["career.seek_employment"] = new() { AdjacencyGroup = ActionPresentationGroups.CareerWork, GroupOrder = 30, PlacementAnchor = ActionPresentationGroups.EmploymentSearch },
            ["career.find_another_job"] = new() { AdjacencyGroup = ActionPresentationGroups.CareerWork, GroupOrder = 40 },
            ["career.quit_job"] = new() { AdjacencyGroup = ActionPresentationGroups.CareerWork, GroupOrder = 50 },
            ["justice.commit_crime"] = new() { AdjacencyGroup = ActionPresentationGroups.CareerWork, GroupOrder = 60 },
            ["justice.leave_life_of_crime"] = new() { AdjacencyGroup = ActionPresentationGroups.CareerWork, GroupOrder = 70 },
            ["justice.ask_to_quit_crime"] = new() { AdjacencyGroup = ActionPresentationGroups.CareerWork, GroupOrder = 80 },
            ["loan.take"] = new() { AdjacencyGroup = ActionPresentationGroups.Loans, GroupOrder = 10 },
            ["loan.give"] = new() { AdjacencyGroup = ActionPresentationGroups.Loans, GroupOrder = 20 },
            ["household.buy_house"] = new() { AdjacencyGroup = ActionPresentationGroups.PropertyMarket, GroupOrder = 10 },
            ["household.sell_house"] = new() { AdjacencyGroup = ActionPresentationGroups.PropertyMarket, GroupOrder = 20 },
            ["farming.buy_farmland"] = new() { AdjacencyGroup = ActionPresentationGroups.PropertyMarket, GroupOrder = 30 },
            ["farming.sell_farmland"] = new() { AdjacencyGroup = ActionPresentationGroups.PropertyMarket, GroupOrder = 40 },
            ["heirloom.sell"] = new() { AdjacencyGroup = ActionPresentationGroups.PropertyMarket, GroupOrder = 50 },
            ["reproduction.try_for_baby"] = new() { AdjacencyGroup = ActionPresentationGroups.MarriageFamily, GroupOrder = 10 },
            ["relationship.repair_marriage"] = new() { AdjacencyGroup = ActionPresentationGroups.MarriageFamily, GroupOrder = 20 },
            ["relationship.divorce_spouse"] = new() { AdjacencyGroup = ActionPresentationGroups.MarriageFamily, GroupOrder = 30 },
            ["wellbeing.heal_relative"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 10 },
            ["wellbeing.therapy"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 20 },
            ["education.get_education"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 30 },
            ["stats.improve_strength"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 40 },
            ["stats.improve_intellect"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 50 },
            ["stats.improve_immunity"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 60 },
            ["stats.improve_appeal"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 70 },
            ["stats.improve_longevity"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 80 },
            ["stats.improve_fertility"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 90 },
            ["personality.religious_study"] = new() { AdjacencyGroup = ActionPresentationGroups.TreatmentGrowth, GroupOrder = 100 },
            ["career.help_seek_employment"] = new() { PlacementAnchor = ActionPresentationGroups.EmploymentSearch },
            ["career.help_find_better_job"] = new() { PlacementAnchor = ActionPresentationGroups.EmploymentSearch },
            ["career.use_family_connections"] = new() { PlaceAfterAnchor = ActionPresentationGroups.EmploymentSearch },
            ["turn.pass"] = new() { PlaceLast = true },
        };
}
