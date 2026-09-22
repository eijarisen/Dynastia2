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
                StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(ActionCategory.Career);
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

    public static IReadOnlyList<GameActionDefinition> Order(
        IReadOnlyList<GameActionDefinition> source)
    {
        var actions =
            source.ToList();

        GroupTogether(
            actions,
            "wellbeing.recover",
            "career.work_harder",
            "career.seek_employment",
            "career.find_another_job",
            "career.quit_job",
            "justice.commit_crime",
            "justice.leave_life_of_crime");

        GroupTogether(
            actions,
            "loan.take",
            "loan.give");

        GroupTogether(
            actions,
            "household.buy_house",
            "household.sell_house",
            "farming.buy_farmland",
            "farming.sell_farmland",
            "heirloom.sell");

        GroupTogether(
            actions,
            "reproduction.try_for_baby",
            "relationship.repair_marriage",
            "relationship.divorce_spouse");

        GroupTogether(
            actions,
            "wellbeing.heal_relative",
            "wellbeing.therapy",
            "education.get_education",
            "stats.improve_strength",
            "stats.improve_intellect",
            "stats.improve_immunity",
            "stats.improve_appeal",
            "stats.improve_longevity",
            "stats.improve_fertility",
            "personality.religious_study");

        var connections =
            actions.FirstOrDefault(
                action =>
                    action.Id.Equals(
                        "career.use_family_connections",
                        StringComparison.OrdinalIgnoreCase));

        if (connections is not null)
        {
            actions.Remove(connections);

            var seekIndex =
                actions.FindIndex(
                    action =>
                        action.Id.Equals(
                            "career.seek_employment",
                            StringComparison.OrdinalIgnoreCase)
                        || action.Id.Equals(
                            "career.help_seek_employment",
                            StringComparison.OrdinalIgnoreCase)
                        || action.Id.Equals(
                            "career.help_find_better_job",
                            StringComparison.OrdinalIgnoreCase));

            actions.Insert(
                seekIndex >= 0
                    ? seekIndex + 1
                    : actions.Count,
                connections);
        }

        var pass =
            actions.FirstOrDefault(
                action =>
                    action.Id.Equals(
                        "turn.pass",
                        StringComparison.OrdinalIgnoreCase));

        if (pass is not null)
        {
            actions.Remove(pass);
            actions.Add(pass);
        }

        return actions;
    }

    private static void GroupTogether(
        List<GameActionDefinition> actions,
        params string[] orderedIds)
    {
        var selected =
            orderedIds
                .Select(id =>
                    actions.FirstOrDefault(action =>
                        action.Id.Equals(
                            id,
                            StringComparison.OrdinalIgnoreCase)))
                .Where(action => action is not null)
                .Cast<GameActionDefinition>()
                .ToList();

        if (selected.Count < 2)
            return;

        var insertIndex =
            selected
                .Select(action => actions.IndexOf(action))
                .Min();

        foreach (var action in selected)
            actions.Remove(action);

        actions.InsertRange(
            insertIndex,
            selected);
    }

}
