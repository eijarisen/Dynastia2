using System.Collections.ObjectModel;
using Avalonia.Threading;
using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public event EventHandler<ActionSelectionRequestedEventArgs>? ActionSelectionRequested;
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
                var selfImprovementAdded =
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

                    if (IsStatImprovementAction(actionId))
                    {
                        if (!selfImprovementAdded)
                        {
                            selfImprovementAdded = true;

                            var selfImprovement =
                                CreateSelfImprovementPresentationAction();

                            _allAvailableActions.Add(
                                new AvailableActionViewModel(
                                    selfImprovement,
                                    new HashSet<ActionCategory>
                                    {
                                        ActionCategory.Personal
                                    },
                                    () =>
                                        ExecuteAction(
                                            SelfImprovementUiActionId)));
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
                                    actionId));

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
                SelfImprovementUiActionId,
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
            var ownedTownIds =
                _economyService.GetHouses(actor)
                    .Select(house => house.Town.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var wealth =
                _economyService.GetHousehold(actor)?.Wealth
                ?? 0m;

            return _locationService.GetTowns()
                .Select(town =>
                {
                    var opportunities = _localCareerOpportunityService?.GetOpportunitySnapshot(town);
                    var price = _economyService.GetHousePrice(town);
                    var livingCost = _economyService.GetLivingCostPerPerson(town);
                    var rentalIncome = _economyService.GetRentalIncome(town);
                    var region = opportunities?.RegionName ?? town.RegionId;
                    var affordable = wealth >= price;
                    var details = $"Population: {town.Population:N0} • {town.SettlementClassDisplayName}\n"
                        + $"{opportunities?.Description ?? "General local work and services."}\n"
                        + $"Living costs: {livingCost:N0} zł per person/year • Rental income: {rentalIncome:N0} zł/year";
                    var search = $"{town.Town} {town.County} {region} {opportunities?.Description}";
                    return new PropertySelectionOption(
                        town.Id,
                        town.Town,
                        $"{town.County} • {region}",
                        details,
                        $"{price:N0} zł",
                        search,
                        affordable);
                })
                .OrderByDescending(option => ownedTownIds.Contains(option.Id))
                .ThenBy(option => option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        if (actionId.Equals("household.sell_house", StringComparison.OrdinalIgnoreCase))
        {
            return _economyService.GetHouses(actor)
                .Select(house =>
                {
                    var opportunities = _localCareerOpportunityService?.GetOpportunitySnapshot(house.Town);
                    var localPrice = _economyService.GetHousePrice(house.Town);
                    var sale = _economyService.GetHouseSaleValue(house.Town);
                    var rentalIncome = _economyService.GetRentalIncome(house.Town);
                    var status = house.IsResidence ? "Residence" : "Rented property";
                    var region = opportunities?.RegionName ?? house.Town.RegionId;
                    return new PropertySelectionOption(
                        house.Id.ToString(),
                        house.Town.Town,
                        $"{house.Town.County} • {region}",
                        $"{status} • {house.Town.SettlementClassDisplayName}\nLocal house price: {localPrice:N0} zł\nRental income: {rentalIncome:N0} zł/year",
                        $"Sale: {sale:N0} zł",
                        $"{house.Town.Town} {house.Town.County} {region} {status}");
                })
                .OrderByDescending(option => option.DetailsText.StartsWith("Residence", StringComparison.OrdinalIgnoreCase))
                .ThenBy(option => option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
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

        // Property inventory actions always belong to the active household,
        // regardless of which family member is currently selected in the UI.
        var target = actor;

        var key = actionId.Equals("household.buy_house", StringComparison.OrdinalIgnoreCase)
            ? "townId"
            : "propertyId";

        var result = _actionRegistry.Execute(
            actionId,
            actor,
            target,
            new Dictionary<string, string>
            {
                [key] = selectedId
            });

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
                        System.Globalization.CultureInfo.InvariantCulture)
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
        var text =
            $"Queued: {ActionEmojiMap.Format(queued.ActionId, queued.Label)}";

        if (queued.TargetId == queued.ActorId)
            return text;

        var target =
            _gameState.People.FirstOrDefault(
                person => person.Id == queued.TargetId);

        if (target is null)
            return text;

        var targetName =
            _familyService is null
                ? $"{target.Name} {target.Surname}"
                : _familyService.GetDisplayName(target);

        return $"{text} — Target: {targetName}";
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
