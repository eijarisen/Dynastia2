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
                var craftProfessionAdded =
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
                CraftProfessionUiActionId,
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "education.get_education",
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
        else if (actionId.Equals(
                     "household.sell_house",
                     StringComparison.OrdinalIgnoreCase)
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
                    _economyService.GetHouseSaleValue(house.Town)
                        .ToString(CultureInfo.InvariantCulture);
            }
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

        var detail =
            BuildQueuedActionDetail(queued);

        if (!string.IsNullOrWhiteSpace(detail))
            text += $" -- {detail}";

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

        return $"{text} — {personName}";
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
                ? "10,000 zł"
                : $"{_farmingService.PurchasePrice.ToString("N0", CultureInfo.InvariantCulture)} zł";
        }

        if (queued.ActionId.Equals(
                "farming.sell_farmland",
                StringComparison.OrdinalIgnoreCase))
        {
            return _farmingService is null
                ? "8,000 zł"
                : $"{_farmingService.SalePrice.ToString("N0", CultureInfo.InvariantCulture)} zł";
        }

        if (queued.ActionId.Equals(
                "household.buy_house",
                StringComparison.OrdinalIgnoreCase)
            || queued.ActionId.Equals(
                "household.sell_house",
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
                    price ??= _economyService.GetHouseSaleValue(house.Town);
                }
            }

            if (!string.IsNullOrWhiteSpace(townName)
                && price is not null)
            {
                return
                    $"{townName}, " +
                    $"{price.Value.ToString("N0", CultureInfo.InvariantCulture)} zł";
            }

            if (!string.IsNullOrWhiteSpace(townName))
                return townName;
        }

        return string.Empty;
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
