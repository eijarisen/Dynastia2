using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string TownAffairsUiActionId =
        "ui.town_affairs";

    public string TownLifeNavigationLabel
    {
        get
        {
            var representative = GetDisplayedHouseholdHead();
            if (representative is null || _locationService is null)
                return "Town Affairs";

            try
            {
                var town = _locationService.GetLocation(representative).HomeTown;
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
    }

    private GameActionDefinition CreateTownAffairsPresentationAction() =>
        new()
        {
            Id = TownAffairsUiActionId,
            Label = TownLifeNavigationLabel,
            Description =
                "Inspect the active household's town, prosperity, institutions and local career possibilities.",
            Mode = ActionExecutionMode.Immediate,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

    internal bool CanOpenTownAffairs =>
        _locationService is not null
        && GetTownLifeRepresentative() is not null;

    internal IPerson? GetTownLifeRepresentative() =>
        GetDisplayedHouseholdHead()
        ?? FindSelectedPerson();
}
