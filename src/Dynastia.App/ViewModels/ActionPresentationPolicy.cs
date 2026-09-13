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
                ActionCategory.Skills);

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
                    ActionCategory.Personal,
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
                    "wellbeing.heal_relative",
                    StringComparison.OrdinalIgnoreCase)
                || actionId.Equals(
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
                StringComparison.OrdinalIgnoreCase))
            {
                categories.Add(
                    ActionCategory.Family);
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
            "household.",
            StringComparison.OrdinalIgnoreCase))
        {
            if (actionId.Contains(
                "nanny",
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

        var connections =
            actions.FirstOrDefault(
                action =>
                    action.Id.Equals(
                        "career.use_family_connections",
                        StringComparison.OrdinalIgnoreCase));

        if (connections is not null)
        {
            actions.Remove(
                connections);

            var seekIndex =
                actions.FindIndex(
                    action =>
                        action.Id.Equals(
                            "career.seek_employment",
                            StringComparison.OrdinalIgnoreCase)
                        || action.Id.Equals(
                            "career.help_seek_employment",
                            StringComparison.OrdinalIgnoreCase));

            if (seekIndex >= 0)
            {
                actions.Insert(
                    seekIndex + 1,
                    connections);
            }
            else
            {
                actions.Add(
                    connections);
            }
        }

        var pass =
            actions.FirstOrDefault(
                action =>
                    action.Id.Equals(
                        "turn.pass",
                        StringComparison.OrdinalIgnoreCase));

        if (pass is not null)
        {
            actions.Remove(
                pass);

            actions.Add(
                pass);
        }

        return actions;
    }
}
