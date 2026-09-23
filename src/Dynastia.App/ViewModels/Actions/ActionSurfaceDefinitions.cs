using Dynastia.Contracts;

namespace Dynastia.App.ViewModels.Actions;

/// <summary>App-only navigation actions and the rules that compose mechanics into UI surfaces.</summary>
internal sealed class ActionSurfaceDefinitions(
    ILocationService? locationService,
    IEconomyService? economyService,
    IJusticeService? justiceService)
{
    internal const string TownAffairsUiActionId = "ui.town_affairs";
    internal const string ManagePropertiesUiActionId = "ui.manage_properties";
    internal const string ManageFinancesUiActionId = "ui.manage_finances";
    internal const string CraftProfessionUiActionId = "ui.craft_profession";

    internal static readonly IReadOnlySet<string> TownAffairsJobActionIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "career.seek_employment",
            "career.find_another_job",
            "career.help_seek_employment",
            "career.help_find_better_job"
        };

    internal static readonly IReadOnlySet<string> TownAffairsHealthActionIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wellbeing.heal_relative",
            "wellbeing.therapy",
            "stats.improve_strength",
            "stats.improve_intellect",
            "stats.improve_immunity",
            "stats.improve_appeal",
            "stats.improve_longevity",
            "stats.improve_fertility"
        };

    internal static readonly IReadOnlySet<string> TownAffairsChurchActionIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "church.attend",
            "church.donate",
            "church.aid_poor_family",
            "church.ask_welfare",
            "personality.religious_study"
        };

    private static GameActionDefinition CreateManagePropertiesPresentationAction() =>
        new()
        {
            Id = ManagePropertiesUiActionId,
            Presentation = new()
            {
                Emoji = "🏘️",
                Categories = [ActionPresentationCategories.Finances]
            },
            Label = "Manage Properties",
            Description =
                "Open Family Inventory to buy, extend, sell, and assign houses alongside farmland and heirlooms.",
            Mode = ActionExecutionMode.Immediate,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

    private static GameActionDefinition CreateManageFinancesPresentationAction() =>
        new()
        {
            Id = ManageFinancesUiActionId,
            Presentation = new()
            {
                Emoji = "🏦",
                Categories = [ActionPresentationCategories.Finances]
            },
            Label = "Manage Finances",
            Description =
                "Open Family Inventory to review income, expenses, lifestyle, loans and local banking access.",
            Mode = ActionExecutionMode.Immediate,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

    private static GameActionDefinition CreateCraftProfessionPresentationAction() =>
        new()
        {
            Id = CraftProfessionUiActionId,
            Presentation = new()
            {
                Emoji = "⚙️",
                Categories = [ActionPresentationCategories.Career]
            },
            Label = "Work in a Profession",
            Description =
                "Choose one of the known Crafts to use as a self-employed profession.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.LifeEvents,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

    internal GameActionDefinition CreateTownAffairsPresentationAction(IPerson? representative) =>
        new()
        {
            Id = TownAffairsUiActionId,
            Presentation = new()
            {
                Emoji = "🏛️",
                Categories = [ActionPresentationCategories.Personal]
            },
            Label = GetTownLifeNavigationLabel(representative),
            Description =
                "Inspect the selected adult household member's town, institutions and local services.",
            Mode = ActionExecutionMode.Immediate,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

    internal string GetTownLifeNavigationLabel(IPerson? representative)
    {
        if (representative is null || locationService is null)
            return "Town Affairs";

        try
        {
            var town = locationService.GetLocation(representative).HomeTown;
            return town.SettlementClass is SettlementClass.City
                or SettlementClass.MajorCity
                    ? "City Affairs"
                    : "Town Affairs";
        }
        catch (InvalidOperationException)
        {
            return "Town Affairs";
        }
    }

    internal bool CanOpenTownAffairs(IPerson? actor, IPerson? target)
    {
        if (locationService is null
            || economyService is null
            || actor is null
            || target is null
            || justiceService?.IsImprisoned(actor) == true
            || justiceService?.IsImprisoned(target) == true
            || target.Age < 18
            || !target.Tags.Has("state.alive"))
        {
            return false;
        }

        return target.Id == actor.Id
            || economyService
                .GetHouseholdMemberIds(actor)
                .Contains(target.Id);
    }

    internal static string? GetAggregateActionId(string actionId)
    {
        if (IsPropertyManagementAction(actionId))
            return ManagePropertiesUiActionId;
        if (IsFinanceManagementAction(actionId))
            return ManageFinancesUiActionId;
        if (actionId.StartsWith("craft.start.", StringComparison.OrdinalIgnoreCase))
            return CraftProfessionUiActionId;
        return null;
    }

    internal static GameActionDefinition CreateAggregateAction(string surfaceId) => surfaceId switch
    {
        ManagePropertiesUiActionId => CreateManagePropertiesPresentationAction(),
        ManageFinancesUiActionId => CreateManageFinancesPresentationAction(),
        CraftProfessionUiActionId => CreateCraftProfessionPresentationAction(),
        _ => throw new ArgumentOutOfRangeException(nameof(surfaceId), surfaceId, "Unknown action surface.")
    };

    private static bool IsPropertyManagementAction(
        string actionId) =>
        actionId.Equals(
            "household.buy_house",
            StringComparison.OrdinalIgnoreCase)
        || actionId.Equals(
            "household.sell_house",
            StringComparison.OrdinalIgnoreCase)
        || actionId.Equals(
            "household.extend_house",
            StringComparison.OrdinalIgnoreCase)
        || actionId.Equals(
            "farming.buy_farmland",
            StringComparison.OrdinalIgnoreCase)
        || actionId.Equals(
            "farming.sell_farmland",
            StringComparison.OrdinalIgnoreCase)
        || actionId.Equals(
            "farming.add_livestock",
            StringComparison.OrdinalIgnoreCase)
        || actionId.Equals(
            "heirloom.sell",
            StringComparison.OrdinalIgnoreCase);


    private static bool IsFinanceManagementAction(
        string actionId) =>
        actionId.Equals(
            "loan.take",
            StringComparison.OrdinalIgnoreCase)
        || actionId.Equals(
            "loan.give",
            StringComparison.OrdinalIgnoreCase)
        || actionId.StartsWith(
            "economy.lifestyle.",
            StringComparison.OrdinalIgnoreCase);

    internal static bool RequiresSelection(string actionId) =>
        actionId.Equals(
                TownAffairsUiActionId,
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                ManagePropertiesUiActionId,
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                ManageFinancesUiActionId,
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                CraftProfessionUiActionId,
                StringComparison.OrdinalIgnoreCase)
            || TownAffairsHealthActionIds.Contains(actionId)
            || TownAffairsChurchActionIds.Contains(actionId)
            || actionId.Equals(
                "education.get_education",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "education.private_tutor",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "wellbeing.heal_relative",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "wellbeing.therapy",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "household.buy_house",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "household.sell_house",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "loan.take",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "loan.give",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "career.seek_employment",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "career.find_another_job",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "career.help_seek_employment",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "career.help_find_better_job",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "relationship.find_spouse",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "relationship.marry_off_daughter",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "relationship.marry_off_son",
                StringComparison.OrdinalIgnoreCase);
}
