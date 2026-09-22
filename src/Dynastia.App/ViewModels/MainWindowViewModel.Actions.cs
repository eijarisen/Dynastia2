using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public event EventHandler<ActionSelectionRequestedEventArgs>? ActionSelectionRequested;
    public bool TryExecuteAvailableActionShortcut(
        params string[] actionIds)
    {
        if (!IsGameStarted
            || IsMainMenuPromptVisible
            || actionIds.Length == 0)
        {
            return false;
        }

        foreach (var actionId in actionIds)
        {
            var action =
                _allAvailableActions
                    .Concat(PassActions)
                    .FirstOrDefault(
                        candidate =>
                            candidate.Id.Equals(
                                actionId,
                                StringComparison.OrdinalIgnoreCase));

            if (action is null)
                continue;

            action.ExecuteCommand.Execute(null);
            return true;
        }

        return false;
    }

    private void RefreshActions()
    {
        AvailableActions.Clear();
        PassActions.Clear();
        ActionFilters.Clear();
        _allAvailableActions.Clear();

        var actor =
            _succession.ActiveController;

        var target =
            FindSelectedPerson();

        HasQueuedAction = false;
        QueuedActionText =
            string.Empty;

        if (IsBloodlineFamilyView)
        {
            OnPropertyChanged(
                nameof(ActionsEmptyText));

            return;
        }

        if (actor is not null
            && target is not null
            && !_succession.IsGameOver)
        {
            var queued =
                _actionRegistry
                    .GetQueuedActions(
                        actor);

            if (queued.Count > 0)
            {
                HasQueuedAction =
                    true;

                QueuedActionText =
                    BuildQueuedActionText(
                        queued[0]);
            }
            else
            {
                if (CanOpenTownAffairs)
                {
                    var townAffairs =
                        CreateTownAffairsPresentationAction();

                    _allAvailableActions.Add(
                        new AvailableActionViewModel(
                            townAffairs,
                            new HashSet<ActionCategory>
                            {
                                ActionCategory.Personal
                            },
                            () => ExecuteAction(
                                TownAffairsUiActionId)));
                }

                var craftProfessionAdded =
                    false;
                var managePropertiesAdded =
                    false;
                var manageFinancesAdded =
                    false;

                foreach (var action in
                    ActionPresentationPolicy.Order(
                        _actionRegistry
                            .GetAvailableActions(
                                actor,
                                target)))
                {
                    var actionId =
                        action.Id;

                    if (actionId.Equals(
                            "church.attend",
                            StringComparison.OrdinalIgnoreCase)
                        || actionId.Equals(
                            "personality.religious_study",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (IsPropertyManagementAction(actionId))
                    {
                        if (!managePropertiesAdded)
                        {
                            managePropertiesAdded = true;
                            var manageProperties =
                                CreateManagePropertiesPresentationAction();

                            _allAvailableActions.Add(
                                new AvailableActionViewModel(
                                    manageProperties,
                                    new HashSet<ActionCategory>
                                    {
                                        ActionCategory.Finances
                                    },
                                    () => ExecuteAction(
                                        ManagePropertiesUiActionId)));
                        }

                        continue;
                    }

                    if (IsFinanceManagementAction(actionId))
                    {
                        if (!manageFinancesAdded)
                        {
                            manageFinancesAdded = true;
                            var manageFinances =
                                CreateManageFinancesPresentationAction();

                            _allAvailableActions.Add(
                                new AvailableActionViewModel(
                                    manageFinances,
                                    new HashSet<ActionCategory>
                                    {
                                        ActionCategory.Finances
                                    },
                                    () => ExecuteAction(
                                        ManageFinancesUiActionId)));
                        }

                        continue;
                    }

                    if (actionId.StartsWith(
                            "craft.start.",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (!craftProfessionAdded)
                        {
                            craftProfessionAdded = true;
                            var profession = CreateCraftProfessionPresentationAction();
                            _allAvailableActions.Add(
                                new AvailableActionViewModel(
                                    profession,
                                    new HashSet<ActionCategory>
                                    {
                                        ActionCategory.Career
                                    },
                                    () => ExecuteAction(CraftProfessionUiActionId)));
                        }

                        continue;
                    }

                    var categories =
                        ActionPresentationPolicy.GetCategories(
                            actionId);

                    var viewModel =
                        new AvailableActionViewModel(
                            action,
                            categories,
                            () =>
                                ExecuteAction(
                                    actionId),
                            GetContextualActionLabel(
                                actionId,
                                actor,
                                target));

                    if (actionId.Equals(
                        "turn.pass",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        PassActions.Add(
                            viewModel);
                    }
                    else
                    {
                        _allAvailableActions.Add(
                            viewModel);
                    }
                }

                RebuildActionFilters();
                ApplyActionFilters();
            }
        }

        OnPropertyChanged(
            nameof(ActionsEmptyText));
    }

    private string? GetContextualActionLabel(
        string actionId,
        IPerson actor,
        IPerson target)
    {
        if (!actionId.Equals(
                "childhood.raise_child",
                StringComparison.OrdinalIgnoreCase)
            || _familyService is null
            || !AreSiblings(actor, target))
        {
            return null;
        }

        return _familyService.GetSex(target) == Sex.Male
            ? "Support Brother"
            : "Support Sister";
    }

    private bool AreSiblings(
        IPerson first,
        IPerson second)
    {
        if (_familyService is null || first.Id == second.Id)
            return false;

        var firstFather = _familyService.GetFather(first)?.Id;
        var firstMother = _familyService.GetMother(first)?.Id;

        return firstFather is Guid fatherId
                && _familyService.GetFather(second)?.Id == fatherId
            || firstMother is Guid motherId
                && _familyService.GetMother(second)?.Id == motherId;
    }

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

    private void RebuildActionFilters()
    {
        ActionFilters.Clear();

        foreach (var category in
            Enum.GetValues<ActionCategory>())
        {
            if (!_allAvailableActions.Any(
                action =>
                    action.Categories.Contains(
                        category)))
            {
                continue;
            }

            ActionFilters.Add(
                new ActionFilterViewModel(
                    category,
                    _activeActionCategories.Contains(
                        category),
                    ToggleActionCategory));
        }
    }

    private void ToggleActionCategory(
        ActionCategory category)
    {
        if (!_activeActionCategories.Add(
            category))
        {
            _activeActionCategories.Remove(
                category);
        }

        foreach (var filter in
            ActionFilters)
        {
            filter.SetActive(
                _activeActionCategories.Contains(
                    filter.Category));
        }

        ApplyActionFilters();

        OnPropertyChanged(
            nameof(ActionsEmptyText));
    }

    private void ApplyActionFilters()
    {
        AvailableActions.Clear();

        foreach (var action in
            _allAvailableActions)
        {
            if (action.Categories.Any(
                _activeActionCategories.Contains))
            {
                AvailableActions.Add(
                    action);
            }
        }

        // Pass is never filtered, but stays at the end of the regular
        // action flow instead of occupying a separate row.
        foreach (var pass in PassActions)
        {
            AvailableActions.Add(pass);
        }
    }

    private void ResetActionCategoryFilters()
    {
        _activeActionCategories.Clear();

        foreach (var category in
            Enum.GetValues<ActionCategory>())
        {
            _activeActionCategories.Add(
                category);
        }
    }

    private void ExecuteAction(
        string actionId)
    {
        var actor =
            _succession.ActiveController;

        var target =
            FindSelectedPerson();

        if (actor is null
            || target is null
            || _succession.IsGameOver)
        {
            return;
        }

        if (actionId.Equals(
                "household.ask_move_out",
                StringComparison.OrdinalIgnoreCase))
        {
            var options = GetPropertySelectionOptions(actionId);

            if (options.Count == 1)
            {
                QueueActionWithSelection(
                    actionId,
                    options[0].Id);
                return;
            }

            if (options.Count > 1)
            {
                ActionSelectionRequested?.Invoke(
                    this,
                    new ActionSelectionRequestedEventArgs(actionId));
                return;
            }

            // No spare property means the queued action uses the rental
            // branch and can be submitted without an additional dialog.
        }

        if (actionId.Equals(
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
                StringComparison.OrdinalIgnoreCase))
        {
            ActionSelectionRequested?.Invoke(
                this,
                new ActionSelectionRequestedEventArgs(actionId));
            return;
        }

        var result =
            _actionRegistry.Execute(
                actionId,
                actor,
                target);

        if (result.Success
            && GetMaleHeirsWithoutAnnualAction()
                .Count == 0
            && PersistenceStatusText.StartsWith(
                "Choose ",
                StringComparison.OrdinalIgnoreCase))
        {
            PersistenceStatusText =
                string.Empty;
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

    public IReadOnlyList<PropertySelectionOption> GetPropertySelectionOptions(
        string actionId)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _economyService is null || _locationService is null)
            return Array.Empty<PropertySelectionOption>();

        if (actionId.Equals("household.buy_house", StringComparison.OrdinalIgnoreCase))
        {
            var currentTown = _economyService.GetResidenceTown(actor);
            var ownedTownIds =
                _economyService.GetHouses(actor)
                    .Select(house => house.Town.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _locationService.GetTowns()
                .Select(town =>
                {
                    var opportunities = _localCareerOpportunityService?
                        .GetOpportunitySnapshot(town);
                    var prosperity = _townProsperityService?.Get(town);
                    var region = opportunities?.RegionName ?? town.RegionId;
                    var category = town.Id.Equals(
                            currentTown.Id,
                            StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : ownedTownIds.Contains(town.Id)
                            ? 1
                            : 2;
                    var opportunityText = opportunities is null
                        ? "—"
                        : string.Join(
                            ", ",
                            opportunities.TownOpportunityTags
                                .Concat(opportunities.RegionOpportunityTags)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Select(FormatOpportunityTag));
                    if (string.IsNullOrWhiteSpace(opportunityText))
                        opportunityText = "—";

                    var details =
                        $"Population: {town.Population:N0} • "
                        + $"Prosperity: {prosperity?.Index ?? 100} ({prosperity?.Label ?? "Stable"}) • "
                        + $"Opportunities: {opportunityText}";

                    var regionalText = opportunities is { RegionOpportunityTags.Count: > 0 }
                        ? string.Join(", ", opportunities.RegionOpportunityTags
                            .Select(FormatOpportunityTag))
                        : string.Empty;

                    var search =
                        $"{town.Town} {town.County} {region} {town.PolityName} "
                        + $"{town.SettlementClassDisplayName} {opportunityText} "
                        + $"{regionalText}";

                    return new
                    {
                        Category = category,
                        Option = new PropertySelectionOption(
                            town.Id,
                            town.Town,
                            $"{town.PolityName} • {region} • {town.County}",
                            details,
                            string.Empty,
                            search)
                    };
                })
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => item.Option)
                .ToList();
        }

        if (actionId.Equals("household.sell_house", StringComparison.OrdinalIgnoreCase))
        {
            return _economyService.GetHouses(actor)
                .Select(house =>
                {
                    var opportunities = _localCareerOpportunityService?.GetOpportunitySnapshot(house.Town);
                    var localPrice = _economyService.GetHousePrice(house.Town);
                    var propertyValue = _economyService.GetHouseValue(house);
                    var sale = _economyService.GetHouseSaleValue(house);
                    var rentalIncome = _economyService.GetRentalIncome(house);
                    var status = house.IsResidence ? "Residence" : "Rented property";
                    var region = opportunities?.RegionName ?? house.Town.RegionId;
                    return new PropertySelectionOption(
                        house.Id.ToString(),
                        house.Town.Town,
                        $"{house.Town.County} • {region}",
                        $"{status} • {house.Town.SettlementClassDisplayName}\nProperty value: {propertyValue:N0} zł • Local base price: {localPrice:N0} zł\nRental income: {rentalIncome:N0} zł/year",
                        $"Sale: {sale:N0} zł",
                        $"{house.Town.Town} {house.Town.County} {region} {status}");
                })
                .OrderByDescending(option => option.DetailsText.StartsWith("Residence", StringComparison.OrdinalIgnoreCase))
                .ThenBy(option => option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        if (actionId.Equals(
                "farming.sell_farmland",
                StringComparison.OrdinalIgnoreCase)
            && _farmingService is not null)
        {
            return _farmingService.GetSnapshot(actor).Farmland
                .Select(farmland =>
                {
                    var sale = _farmingService.GetFarmlandSaleValue(farmland);
                    var livestock = string.IsNullOrWhiteSpace(farmland.LivestockDisplayName)
                        ? "No livestock"
                        : $"Livestock: {farmland.LivestockEmoji} {farmland.LivestockDisplayName}";
                    var title =
                        $"{farmland.FarmTypeEmoji} {farmland.FarmTypeDisplayName}";

                    return new PropertySelectionOption(
                        farmland.Id.ToString(),
                        title,
                        $"{farmland.Town.Town} • {farmland.Town.County}",
                        $"{livestock}\nAcquired: {farmland.AcquiredYear}",
                        $"Sale: {sale:N0} zł",
                        $"{title} {farmland.Town.Town} {farmland.Town.County} {livestock}");
                })
                .OrderBy(option => option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(option => option.SecondaryText, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        if (actionId.Equals(
                "household.ask_move_out",
                StringComparison.OrdinalIgnoreCase))
        {
            return _economyService.GetHouses(actor)
                .Where(house => !house.IsResidence)
                .Select(house =>
                {
                    var opportunities =
                        _localCareerOpportunityService?.GetOpportunitySnapshot(house.Town);
                    var rentalIncome = _economyService.GetRentalIncome(house);
                    var region = opportunities?.RegionName ?? house.Town.RegionId;
                    return new PropertySelectionOption(
                        house.Id.ToString(),
                        house.Town.Town,
                        $"{house.Town.County} • {region}",
                        $"Spare property • {house.Town.SettlementClassDisplayName}\nCurrently yields {rentalIncome:N0} zł/year as rental income. It will become the selected resident's new home.",
                        "Provide house",
                        $"{house.Town.Town} {house.Town.County} {region} move out resident");
                })
                .OrderBy(option => option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        return Array.Empty<PropertySelectionOption>();
    }

    public void QueueActionWithSelection(
        string actionId,
        string selectedId)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var moveOut = actionId.Equals(
            "household.ask_move_out",
            StringComparison.OrdinalIgnoreCase);

        // Inventory actions belong to the active household. Move Out is the
        // exception: the selected resident remains the queued target.
        var target = moveOut
            ? FindSelectedPerson()
            : actor;

        if (target is null)
            return;

        var key = actionId.Equals("household.buy_house", StringComparison.OrdinalIgnoreCase)
            ? "townId"
            : actionId.Equals("heirloom.sell", StringComparison.OrdinalIgnoreCase)
                ? "heirloomId"
                : (actionId.Equals("farming.sell_farmland", StringComparison.OrdinalIgnoreCase)
                   || actionId.Equals("farming.add_livestock", StringComparison.OrdinalIgnoreCase))
                    ? "farmlandId"
                    : "propertyId";

        var parameters =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                [key] = selectedId
            };

        if (actionId.Equals(
                "household.buy_house",
                StringComparison.OrdinalIgnoreCase)
            && _locationService?.FindTown(selectedId) is { } buyTown
            && _economyService is not null)
        {
            parameters["summaryTown"] = buyTown.Town;
            parameters["summaryPrice"] =
                _economyService.GetHousePrice(buyTown)
                    .ToString(CultureInfo.InvariantCulture);
        }
        else if ((actionId.Equals(
                      "household.sell_house",
                      StringComparison.OrdinalIgnoreCase)
                  || actionId.Equals(
                      "household.extend_house",
                      StringComparison.OrdinalIgnoreCase))
                 && _economyService is not null
                 && Guid.TryParse(selectedId, out var propertyId))
        {
            var house =
                _economyService.GetHouses(actor)
                    .FirstOrDefault(candidate => candidate.Id == propertyId);

            if (house is not null)
            {
                parameters["summaryTown"] = house.Town.Town;
                parameters["summaryPrice"] =
                    (actionId.Equals(
                        "household.extend_house",
                        StringComparison.OrdinalIgnoreCase)
                        ? house.ExtensionCost
                        : _economyService.GetHouseSaleValue(house))
                    .ToString(CultureInfo.InvariantCulture);
            }
        }
        else if ((actionId.Equals(
                      "farming.sell_farmland",
                      StringComparison.OrdinalIgnoreCase)
                  || actionId.Equals(
                      "farming.add_livestock",
                      StringComparison.OrdinalIgnoreCase))
                 && _farmingService is not null
                 && Guid.TryParse(selectedId, out var farmlandId)
                 && _succession.ActiveController is { } farmingActor)
        {
            var farmland = _farmingService.GetSnapshot(farmingActor).Farmland
                .FirstOrDefault(item => item.Id == farmlandId);
            if (farmland is not null)
            {
                parameters["summaryFarmland"] =
                    $"{farmland.FarmTypeEmoji} {farmland.FarmTypeDisplayName} — {farmland.Town.Town}";
                parameters["summaryPrice"] =
                    (actionId.Equals(
                        "farming.add_livestock",
                        StringComparison.OrdinalIgnoreCase)
                        ? _farmingService.LivestockPurchasePrice
                        : _farmingService.GetFarmlandSaleValue(farmland))
                    .ToString(CultureInfo.InvariantCulture);
            }
        }
        else if (actionId.Equals(
                     "heirloom.sell",
                     StringComparison.OrdinalIgnoreCase)
                 && _heirloomService is not null
                 && Guid.TryParse(selectedId, out var heirloomId))
        {
            var heirloom = _heirloomService.GetHeirlooms(actor)
                .FirstOrDefault(item => item.Id == heirloomId);
            if (heirloom is not null)
            {
                parameters["summaryHeirloom"] = heirloom.DisplayName;
                parameters["summaryPrice"] = _heirloomService.GetSaleValue(heirloom)
                    .ToString(CultureInfo.InvariantCulture);
            }
        }
        else if (moveOut
                 && _economyService is not null
                 && Guid.TryParse(selectedId, out var movePropertyId))
        {
            var house = _economyService.GetHouses(actor)
                .FirstOrDefault(candidate => candidate.Id == movePropertyId);

            if (house is not null)
                parameters["summaryTown"] = house.Town.Town;
        }

        var result = _actionRegistry.Execute(
            actionId,
            actor,
            target,
            parameters);

        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

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

    public decimal GetMaximumLoanPrincipal(
        string actionId)
    {
        if (!actionId.Equals(
                "loan.give",
                StringComparison.OrdinalIgnoreCase))
        {
            return 10000m;
        }

        var actor =
            _succession.ActiveController;

        var wealth =
            actor is null
                ? 0m
                : _economyService?.GetHousehold(actor)?.Wealth
                    ?? 0m;

        var wholeThousands =
            Math.Floor(wealth / 1000m) * 1000m;

        return Math.Clamp(
            wholeThousands,
            0m,
            10000m);
    }

    public IReadOnlyList<LoanOfferInfo> GetLoanOffers(
        bool isGivingLoan,
        decimal maximumPrincipal)
    {
        var actor = _succession.ActiveController;
        if (actor is null
            || _loanService is null
            || maximumPrincipal < 1000m)
        {
            return [];
        }

        return _loanService.GetOffers(
            actor,
            isGivingLoan,
            maximumPrincipal);
    }

    public LoanTermsInfo? GetLoanTerms(
        decimal principal,
        int durationYears)
    {
        if (_loanService is null)
            return null;

        try
        {
            return _loanService.CalculateTerms(
                principal,
                durationYears);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public void QueueLoanAction(
        string actionId,
        LoanSelectionResult selection)
    {
        var actor =
            _succession.ActiveController;

        if (actor is null
            || _succession.IsGameOver)
        {
            return;
        }

        // Loan inventory actions always belong to the active household,
        // regardless of which family member is currently selected in the UI.
        var target = actor;

        var parameters =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["principal"] =
                    selection.Principal.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),

                ["durationYears"] =
                    selection.DurationYears.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),

                ["interestMultiplier"] =
                    selection.InterestMultiplier.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),

                ["counterpartyName"] =
                    selection.CounterpartyName,

                ["counterpartyTownId"] =
                    selection.CounterpartyTownId,

                ["counterpartyNationalityId"] =
                    selection.CounterpartyNationalityId
            };

        var result =
            _actionRegistry.Execute(
                actionId,
                actor,
                target,
                parameters);

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

    private string BuildQueuedActionText(
        QueuedActionInfo queued)
    {
        var queuedActor = _gameState.People.FirstOrDefault(
            person => person.Id == queued.ActorId);
        var queuedTarget = _gameState.People.FirstOrDefault(
            person => person.Id == queued.TargetId);
        var contextualLabel = queuedActor is not null && queuedTarget is not null
            ? GetContextualActionLabel(
                queued.ActionId,
                queuedActor,
                queuedTarget)
            : null;

        var queuedLabel =
            ResolveQueuedActionLabel(
                queued,
                contextualLabel);
        var text =
            $"Queued: {ActionEmojiMap.Format(queued.ActionId, queuedLabel)}";

        var detail =
            BuildQueuedActionDetail(queued);

        if (!string.IsNullOrWhiteSpace(detail))
            text += $" – {detail}";

        if (SuppressQueuedActionPersonName(queued.ActionId))
            return text;

        var personId =
            queued.TargetId != queued.ActorId
                ? queued.TargetId
                : queued.ActorId;

        var person =
            _gameState.People.FirstOrDefault(
                candidate => candidate.Id == personId);

        if (person is null)
            return text;

        var personName =
            _familyService is null
                ? $"{person.Name} {person.Surname}"
                : _familyService.GetDisplayName(person);

        return $"{text} – {personName}";
    }

    private static string ResolveQueuedActionLabel(
        QueuedActionInfo queued,
        string? contextualLabel)
    {
        if (!string.IsNullOrWhiteSpace(contextualLabel))
            return contextualLabel;

        if (queued.ActionId.Equals(
                "farming.buy_farmland",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Buy Farmland";
        }

        if (queued.ActionId.Equals(
                "farming.sell_farmland",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Sell Farmland";
        }

        if (queued.ActionId.Equals(
                "farming.add_livestock",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Add Livestock";
        }

        if (queued.ActionId.Equals(
                "heirloom.sell",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Sell Heirloom";
        }

        if (queued.ActionId.Equals(
                "craft.stop_occupation",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Quit Profession";
        }

        return queued.Label;
    }

    private string BuildQueuedActionDetail(
        QueuedActionInfo queued)
    {
        var parameters =
            queued.Parameters;

        if (parameters is null)
            return string.Empty;

        if (queued.ActionId.Equals(
                "education.get_education",
                StringComparison.OrdinalIgnoreCase)
            && parameters.TryGetValue("summaryEducationOption", out var educationOption))
        {
            return educationOption;
        }

        if (queued.ActionId.StartsWith(
                "craft.start.",
                StringComparison.OrdinalIgnoreCase)
            && parameters.TryGetValue("summaryCraft", out var craftName))
        {
            return craftName;
        }

        if (queued.ActionId.StartsWith(
                "church.",
                StringComparison.OrdinalIgnoreCase)
            && TryReadDecimalParameter(
                parameters,
                "churchAmount",
                out var churchAmount))
        {
            var amountText =
                $"{churchAmount.ToString("N0", CultureInfo.InvariantCulture)} zł";
            return parameters.TryGetValue("churchTier", out var churchTier)
                ? $"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(churchTier)} — {amountText}"
                : amountText;
        }

        if (queued.ActionId.StartsWith(
                "loan.",
                StringComparison.OrdinalIgnoreCase)
            && TryReadDecimalParameter(
                parameters,
                "principal",
                out var principal)
            && TryReadIntParameter(
                parameters,
                "durationYears",
                out var durationYears))
        {
            var yearsLabel =
                durationYears == 1
                    ? "year"
                    : "years";

            return
                $"{principal.ToString("N0", CultureInfo.InvariantCulture)} zł, " +
                $"{durationYears} {yearsLabel}";
        }

        if (queued.ActionId.Equals(
                "farming.buy_farmland",
                StringComparison.OrdinalIgnoreCase))
        {
            return _farmingService is null
                ? "20,000 zł"
                : $"{_farmingService.PurchasePrice.ToString("N0", CultureInfo.InvariantCulture)} zł";
        }

        if (queued.ActionId.Equals(
                "farming.sell_farmland",
                StringComparison.OrdinalIgnoreCase)
            || queued.ActionId.Equals(
                "farming.add_livestock",
                StringComparison.OrdinalIgnoreCase))
        {
            var name = parameters.TryGetValue("summaryFarmland", out var storedFarm)
                ? storedFarm
                : "Selected farmland";
            if (TryReadDecimalParameter(parameters, "summaryPrice", out var farmlandPrice))
            {
                return $"{name} — {farmlandPrice.ToString("N0", CultureInfo.InvariantCulture)} zł";
            }

            if (_farmingService is null)
                return name;

            var fallback = queued.ActionId.Equals(
                    "farming.add_livestock",
                    StringComparison.OrdinalIgnoreCase)
                ? _farmingService.LivestockPurchasePrice
                : _farmingService.SalePrice;
            return $"{name} — {fallback.ToString("N0", CultureInfo.InvariantCulture)} zł";
        }

        if (queued.ActionId.Equals(
                "heirloom.sell",
                StringComparison.OrdinalIgnoreCase))
        {
            var name = parameters.TryGetValue("summaryHeirloom", out var storedName)
                ? storedName
                : "Selected heirloom";
            return TryReadDecimalParameter(parameters, "summaryPrice", out var saleValue)
                ? $"{name} — {saleValue.ToString("N0", CultureInfo.InvariantCulture)} zł"
                : name;
        }

        if (queued.ActionId.Equals(
                "household.buy_house",
                StringComparison.OrdinalIgnoreCase)
            || queued.ActionId.Equals(
                "household.sell_house",
                StringComparison.OrdinalIgnoreCase)
            || queued.ActionId.Equals(
                "household.extend_house",
                StringComparison.OrdinalIgnoreCase))
        {
            var townName =
                parameters.TryGetValue("summaryTown", out var storedTown)
                    ? storedTown
                    : null;

            decimal? price =
                TryReadDecimalParameter(
                    parameters,
                    "summaryPrice",
                    out var storedPrice)
                    ? storedPrice
                    : null;

            if (queued.ActionId.Equals(
                    "household.buy_house",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(townName)
                    && parameters.TryGetValue("townId", out var townId)
                    && _locationService?.FindTown(townId) is { } town)
                {
                    townName = town.Town;
                    price ??= _economyService?.GetHousePrice(town);
                }
            }
            else if (parameters.TryGetValue(
                         "propertyId",
                         out var propertyIdRaw)
                     && Guid.TryParse(propertyIdRaw, out var propertyId)
                     && _economyService is not null)
            {
                var actor =
                    _gameState.People.FirstOrDefault(
                        person => person.Id == queued.ActorId);

                var house =
                    actor is null
                        ? null
                        : _economyService.GetHouses(actor)
                            .FirstOrDefault(candidate => candidate.Id == propertyId);

                if (house is not null)
                {
                    townName ??= house.Town.Town;
                    price ??= queued.ActionId.Equals(
                            "household.extend_house",
                            StringComparison.OrdinalIgnoreCase)
                        ? house.ExtensionCost
                        : _economyService.GetHouseSaleValue(house);
                }
            }

            if (!string.IsNullOrWhiteSpace(townName)
                && price is not null)
            {
                var capacityText =
                    queued.ActionId.Equals(
                            "household.buy_house",
                            StringComparison.OrdinalIgnoreCase)
                        && TryReadIntParameter(parameters, "summaryCapacity", out var capacity)
                            ? $", {capacity} residents"
                            : string.Empty;

                return
                    $"{townName}{capacityText}, " +
                    $"{price.Value.ToString("N0", CultureInfo.InvariantCulture)} zł";
            }

            if (!string.IsNullOrWhiteSpace(townName))
                return townName;
        }

        return string.Empty;
    }

    private static string FormatOpportunityTag(string tag)
    {
        var value = tag;
        var separator = value.LastIndexOf('.');
        if (separator >= 0 && separator + 1 < value.Length)
            value = value[(separator + 1)..];

        value = value.Replace('_', ' ').Replace('-', ' ');
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(value);
    }

    private static bool TryReadDecimalParameter(
        IReadOnlyDictionary<string, string> parameters,
        string key,
        out decimal value)
    {
        value = 0m;

        return
            parameters.TryGetValue(key, out var text)
            && decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static bool TryReadIntParameter(
        IReadOnlyDictionary<string, string> parameters,
        string key,
        out int value)
    {
        value = 0;

        return
            parameters.TryGetValue(key, out var text)
            && int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static bool SuppressQueuedActionPersonName(
        string actionId)
    {
        if (actionId.Equals(
                "turn.pass",
                StringComparison.OrdinalIgnoreCase)
            || actionId.StartsWith(
                "loan.",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return actionId.Equals(
                   "household.buy_house",
                   StringComparison.OrdinalIgnoreCase)
               || actionId.Equals(
                   "household.sell_house",
                   StringComparison.OrdinalIgnoreCase)
               || actionId.Equals(
                   "household.give_house_to_son",
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
    }

    private IPerson? FindSelectedPerson()
    {
        if (SelectedPerson is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                x =>
                    x.Id
                    == SelectedPerson.Id);
    }

}
