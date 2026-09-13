using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public bool HasFamilyRelations =>
        IsGameStarted
        && IsLineageFamilyView
        && _succession.ActiveController is not null
        && _familyRelationService is not null;

    internal IReadOnlyList<FamilyRelationHouseholdViewModel> GetFamilyRelationsHouseholds()
    {
        var actor = _succession.ActiveController;
        if (actor is null
            || _familyRelationService is null
            || _familyService is null
            || _householdService is null
            || _economyService is null)
        {
            return [];
        }

        _familyRelationService.ReconcileAll();
        var householdInfos = _householdService.GetActiveHouseholds();
        var parameters = new Dictionary<string, string>
        {
            ["familyRelations"] = "true"
        };

        return _familyRelationService.GetRelatedHouseholds(actor)
            .Select(info =>
            {
                var primary = info.PrimaryRelation;
                var relative = _gameState.People.FirstOrDefault(p => p.Id == primary.RelativeId);
                if (relative is null)
                    return null;

                var targetHead = info.HeadId.HasValue
                    ? _gameState.People.FirstOrDefault(p => p.Id == info.HeadId.Value)
                    : null;
                var householdInfo = info.HouseholdId.HasValue
                    ? householdInfos.FirstOrDefault(h => h.HouseholdId == info.HouseholdId.Value)
                    : null;

                if (targetHead is null)
                    return null;

                var finance = _economyService.GetHousehold(targetHead);
                if (finance is null)
                    return null;

                var title = householdInfo is null
                    ? relative.Surname
                    : householdInfo.Generation.HasValue
                        ? $"G{householdInfo.Generation.Value} {householdInfo.Surname}"
                        : householdInfo.Surname;

                var relativesText = string.Join(
                    Environment.NewLine,
                    info.Relations.Select(link =>
                    {
                        var person = _gameState.People.FirstOrDefault(p => p.Id == link.RelativeId);
                        return person is null
                            ? $"{link.Kinship} ({link.State})"
                            : $"{link.Kinship} — {_familyService.GetDisplayName(person)} ({link.State})";
                    }));

                var relationshipText = $"Relationship: {primary.State}";
                var wealthText = $"Wealth: {finance.Wealth:N0} zł";

                var locationPerson = targetHead ?? relative;
                var locationText = _locationService is null
                    ? string.Empty
                    : FormatRelationLocation(_locationService.GetLocation(locationPerson).HomeTown);

                var actionModels = _actionRegistry
                    .GetAvailableActions(actor, relative, parameters)
                    .Where(action => action.Id.StartsWith("family_relations.", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(action => RelationActionOrder(action.Id))
                    .Select(action => new FamilyRelationActionViewModel(
                        action.Id,
                        action.Label,
                        action.Description,
                        relative.Id,
                        action.Id.Equals("family_relations.give_house", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                return new FamilyRelationHouseholdViewModel(
                    title,
                    relativesText,
                    relationshipText,
                    wealthText,
                    locationText,
                    actionModels);
            })
            .Where(item => item is not null)
            .Cast<FamilyRelationHouseholdViewModel>()
            .ToList();
    }

    internal IReadOnlyList<PropertySelectionOption> GetFamilyRelationGiveHouseOptions(Guid relativeId)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _economyService is null)
            return [];

        return _economyService.GetHouses(actor)
            .Where(house => house.IsRented)
            .Select(house => new PropertySelectionOption(
                house.Id.ToString(),
                house.Town.Town,
                house.Town.County,
                $"{house.Town.SettlementClassDisplayName} — rented investment",
                $"Value { _economyService.GetHouseSaleValue(house.Town):N0} zł",
                $"{house.Town.Town} {house.Town.County} {house.Town.RegionId}"))
            .ToList();
    }

    internal GameActionResult QueueFamilyRelationAction(
        Guid relativeId,
        string actionId,
        string? propertyId)
    {
        var actor = _succession.ActiveController;
        var relative = _gameState.People.FirstOrDefault(p => p.Id == relativeId);
        if (actor is null || relative is null)
            return new GameActionResult(false, "The family relationship is no longer available.");

        var parameters = new Dictionary<string, string>
        {
            ["familyRelations"] = "true"
        };
        if (!string.IsNullOrWhiteSpace(propertyId))
            parameters["propertyId"] = propertyId;

        var result = _actionRegistry.Execute(actionId, actor, relative, parameters);
        RefreshActions();
        OnPropertyChanged(nameof(HasQueuedAction));
        OnPropertyChanged(nameof(QueuedActionText));
        return result;
    }

    private static string FormatRelationLocation(TownInfo town) =>
        town.DisplayName;

    private static int RelationActionOrder(string id) => id switch
    {
        "family_relations.improve" => 0,
        "family_relations.ask_money" => 10,
        "family_relations.ask_house" => 20,
        "family_relations.ask_job_help" => 30,
        "family_relations.give_money" => 40,
        "family_relations.give_house" => 50,
        "family_relations.give_job_help" => 60,
        _ => 100
    };
}
