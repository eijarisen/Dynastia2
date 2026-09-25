using System.Collections.ObjectModel;
using System.Globalization;
using Dynastia.Contracts;
using static Dynastia.App.ViewModels.Actions.ActionSurfaceDefinitions;

namespace Dynastia.App.ViewModels.Actions;

/// <summary>
/// Owns action presentation and dispatch. Selection requests and execution results flow out;
/// only the host decides when to perform a broad UI refresh.
/// </summary>
internal sealed class ActionPanelCoordinator : ViewModelBase
{
    private readonly IGameState _gameState;
    private readonly ISuccessionService _succession;
    private readonly IActionRegistry _actionRegistry;
    private readonly IFamilyService? _familyService;
    private readonly IEconomyService? _economyService;
    private readonly IFarmingService? _farmingService;
    private readonly ILocationService? _locationService;
    private readonly ActionSelectionOptionService _selectionOptions;
    private readonly ActionSurfaceDefinitions _surfaces;
    private readonly Func<IPerson?> _getSelectedPerson;

    internal ActionPanelCoordinator(
        IGameState gameState,
        ISuccessionService succession,
        IActionRegistry actionRegistry,
        IFamilyService? familyService,
        IEconomyService? economyService,
        IFarmingService? farmingService,
        ILocationService? locationService,
        ActionSelectionOptionService selectionOptions,
        ActionSurfaceDefinitions surfaces,
        Func<IPerson?> getSelectedPerson)
    {
        _gameState = gameState;
        _succession = succession;
        _actionRegistry = actionRegistry;
        _familyService = familyService;
        _economyService = economyService;
        _farmingService = farmingService;
        _locationService = locationService;
        _selectionOptions = selectionOptions;
        _surfaces = surfaces;
        _getSelectedPerson = getSelectedPerson;
    }

    private readonly List<AvailableActionViewModel> _allAvailableActions = [];
    private readonly HashSet<ActionCategory> _activeActionCategories = new(Enum.GetValues<ActionCategory>());
    private string _queuedActionText = string.Empty;
    private bool _hasQueuedAction;

    public ObservableCollection<AvailableActionViewModel> AvailableActions { get; } = [];
    public ObservableCollection<AvailableActionViewModel> PassActions { get; } = [];
    public ObservableCollection<ActionFilterViewModel> ActionFilters { get; } = [];

    public event EventHandler<ActionSelectionRequestedEventArgs>? ActionSelectionRequested;
    public event EventHandler<ActionUiExecutionResult>? ActionExecuted;
    public event EventHandler? FiltersOrActionsChanged;

    public string QueuedActionText
    {
        get => _queuedActionText;
        private set
        {
            if (_queuedActionText == value)
                return;

            _queuedActionText = value;
            OnPropertyChanged();
        }
    }

    public bool HasQueuedAction
    {
        get => _hasQueuedAction;
        private set
        {
            if (_hasQueuedAction == value)
                return;

            _hasQueuedAction = value;
            OnPropertyChanged();
        }
    }

    public bool TryExecuteAvailableActionShortcut(
        bool isGameStarted,
        bool isMainMenuPromptVisible,
        params string[] actionIds)
    {
        if (!isGameStarted
            || isMainMenuPromptVisible
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

    public void Refresh(bool isBloodlineFamilyView)
    {
        AvailableActions.Clear();
        PassActions.Clear();
        ActionFilters.Clear();
        _allAvailableActions.Clear();

        var actor =
            _succession.ActiveController;

        var target =
            _getSelectedPerson();

        HasQueuedAction = false;
        QueuedActionText =
            string.Empty;

        if (isBloodlineFamilyView)
        {
            FiltersOrActionsChanged?.Invoke(this, EventArgs.Empty);

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
                if (_surfaces.CanOpenTownAffairs(actor, target))
                {
                    var townAffairs =
                        _surfaces.CreateTownAffairsPresentationAction(target);

                    _allAvailableActions.Add(
                        new AvailableActionViewModel(
                            townAffairs,
                            () => ExecuteAction(
                                TownAffairsUiActionId)));
                }

                var addedSurfaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var action in
                    ActionPresentationPolicy.Order(
                        _actionRegistry
                            .GetAvailableActions(
                                actor,
                                target)))
                {
                    var actionId =
                        action.Id;

                    if (!ActionPresentationPolicy.Resolve(action).ShowInPrimaryActionList)
                        continue;

                    if (GetAggregateActionId(actionId) is { } surfaceId)
                    {
                        if (addedSurfaces.Add(surfaceId))
                        {
                            _allAvailableActions.Add(new AvailableActionViewModel(
                                CreateAggregateAction(surfaceId),
                                () => ExecuteAction(surfaceId)));
                        }

                        continue;
                    }

                    var viewModel =
                        new AvailableActionViewModel(
                            action,
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

        FiltersOrActionsChanged?.Invoke(this, EventArgs.Empty);
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

        FiltersOrActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyActionFilters()
    {
        AvailableActions.Clear();

        foreach (var action in
            _allAvailableActions)
        {
            if (action.Categories.Count == 0
                || action.Categories.Any(_activeActionCategories.Contains))
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

    public void ResetActionCategoryFilters()
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
            _getSelectedPerson();

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
            var options = _selectionOptions.GetPropertySelectionOptions(actionId);

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

        if (RequiresSelection(actionId))
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

        Publish(new ActionUiExecutionResult(true, result.Success, result.Message, true));
    }

    public void QueueActionWithSelection(string actionId, string selectedId) =>
        Publish(_selectionOptions.QueueActionWithSelection(actionId, selectedId, _getSelectedPerson()));

    public void QueueLoanAction(string actionId, LoanSelectionResult selection) =>
        Publish(_selectionOptions.QueueLoanAction(actionId, selection));

    private void Publish(ActionUiExecutionResult result)
    {
        if (result.Attempted)
            ActionExecuted?.Invoke(this, result);
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
            $"Queued: {FormatQueuedAction(queued, queuedLabel, queuedActor, queuedTarget)}";

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

    private string FormatQueuedAction(
        QueuedActionInfo queued,
        string label,
        IPerson? actor,
        IPerson? target)
    {
        if (actor is not null && target is not null)
        {
            var definition = _actionRegistry.TryResolveDefinition(
                queued.ActionId,
                actor,
                target);

            if (definition is not null)
                return ActionEmojiMap.Format(definition, label);
        }

        return ActionEmojiMap.Format(queued.ActionId, label);
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

    internal string BuildQueuedActionDetail(
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
            if (TryReadDecimalParameter(parameters, "summaryPrice", out var summaryPrice)
                || TryReadDecimalParameter(parameters, "farmlandAskingPrice", out summaryPrice))
            {
                return $"{summaryPrice.ToString("N0", CultureInfo.InvariantCulture)} zł";
            }

            return _farmingService is null
                ? "10,000 zł"
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

    internal static bool SuppressQueuedActionPersonName(
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

    public string GetEmptyText(bool isBloodlineFamilyView, bool hasDisplayedHousehold)
    {
        if (isBloodlineFamilyView)
        {
            return !hasDisplayedHousehold
                    ? "No autonomous bloodline households."
                    : "This household lives independently.";
        }

        if (AvailableActions.Count > 0
            || PassActions.Count > 0
            || HasQueuedAction)
        {
            return string.Empty;
        }

        var actor =
            _succession.ActiveController;

        if (actor is not null)
        {
            var blockedReason =
                _actionRegistry.GetBlockedReason(
                    actor);

            if (!string.IsNullOrWhiteSpace(
                blockedReason))
            {
                return blockedReason;
            }
        }

        return
            "No actions available for the selected person.";
    }
}
