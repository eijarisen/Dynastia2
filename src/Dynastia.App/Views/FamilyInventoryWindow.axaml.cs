using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;
using Dynastia.Contracts;

namespace Dynastia.App.Views;

public partial class FamilyInventoryWindow : Window
{
    private readonly MainWindowViewModel _main;
    private readonly FamilyInventoryWindowViewModel _viewModel;
    private readonly ITownLifeService? _townLifeService;
    private readonly TownAffairsRequest? _bankRequest;

    public FamilyInventoryWindow()
        : this(null!)
    {
    }

    public FamilyInventoryWindow(
        MainWindowViewModel main,
        FamilyInventoryTab initialTab = FamilyInventoryTab.Money,
        ITownLifeService? townLifeService = null)
    {
        InitializeComponent();

        _main = main;
        _townLifeService = townLifeService;
        _bankRequest = main is null
            ? null
            : main.CreateActiveHouseholdTownAffairsRequest(
                TownAffairsTab.Bank);

        var canVisitBank = false;
        if (_bankRequest is not null && townLifeService is not null)
        {
            try
            {
                canVisitBank = townLifeService
                    .GetTownLife(_bankRequest.TownId)
                    .Bank
                    .IsAvailable;
            }
            catch (InvalidOperationException)
            {
                canVisitBank = false;
            }
        }

        _viewModel = main is null
            ? null!
            : new FamilyInventoryWindowViewModel(
                main,
                initialTab,
                canVisitBank);

        if (main is not null)
            DataContext = _viewModel;

        AddHandler(
            KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private async void OnVisitBankClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (!_viewModel.CanVisitBank
            || _townLifeService is null
            || _bankRequest is null)
        {
            return;
        }

        var snapshot = _townLifeService.GetTownLife(_bankRequest.TownId);
        var model = _main.CreateTownAffairsViewModel(
            snapshot,
            _bankRequest);
        var window = new TownLifeWindow(model);
        await window.ShowDialog(this);
        _viewModel.Refresh();
    }

    private async void OnBuyHouseClick(
        object? sender,
        RoutedEventArgs e)
    {
        var options = _main.GetPropertySelectionOptions(
            "household.buy_house");
        if (options.Count == 0 || _townLifeService is null || Owner is not Window dialogOwner)
            return;

        Close();

        var selector = new PropertySelectionWindow(
            "Select Town",
            "View Housing",
            options,
            compact: true);
        var townId = await selector.ShowDialog<string?>(dialogOwner);
        if (string.IsNullOrWhiteSpace(townId))
            return;

        var request = _main.CreateHousingTownAffairsRequest(townId);
        var snapshot = _townLifeService.GetTownLife(request.TownId);
        var model = _main.CreateTownAffairsViewModel(snapshot, request);
        var window = new TownLifeWindow(model);
        await window.ShowDialog(dialogOwner);
    }

    private async void OnSellHouseClick(
        object? sender,
        RoutedEventArgs e) =>
        await OpenPropertyWindowAndClose(
            "household.sell_house",
            "Select Property",
            "Sell");

    private void OnExtendHouseClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not Guid propertyId)
        {
            return;
        }

        _main.QueueActionWithSelection(
            "household.extend_house",
            propertyId.ToString());
        _viewModel.Refresh();
    }

    private async Task OpenPropertyWindowAndClose(
        string actionId,
        string title,
        string confirmText)
    {
        var options =
            _main.GetPropertySelectionOptions(
                actionId);

        if (options.Count == 0 || Owner is not Window dialogOwner)
            return;

        Close();

        var window =
            new PropertySelectionWindow(
                title,
                confirmText,
                options);

        var selectedId =
            await window.ShowDialog<string?>(dialogOwner);

        if (string.IsNullOrWhiteSpace(selectedId))
            return;

        _main.QueueActionWithSelection(
            actionId,
            selectedId);
    }

    private void OnLifestyleClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not string actionId
            || string.IsNullOrWhiteSpace(actionId))
        {
            return;
        }

        var result =
            _main.QueueFamilyInventoryAction(actionId);

        if (!result.Success)
        {
            _viewModel.Refresh();
            return;
        }

        Close();
    }

    private void OnSellHeirloomClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not Guid heirloomId)
        {
            return;
        }

        _main.QueueActionWithSelection(
            "heirloom.sell",
            heirloomId.ToString());
        Close();
    }

    private async void OnBuyFarmlandClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (_townLifeService is null || Owner is not Window dialogOwner)
            return;

        var request = _main.CreateActiveHouseholdTownAffairsRequest(
            TownAffairsTab.Housing);
        if (request is null)
            return;

        Close();

        var snapshot = _townLifeService.GetTownLife(request.TownId);
        var model = _main.CreateTownAffairsViewModel(snapshot, request);
        var window = new TownLifeWindow(model);
        await window.ShowDialog(dialogOwner);
    }

    private async void OnSellFarmlandClick(
        object? sender,
        RoutedEventArgs e) =>
        await OpenPropertyWindowAndClose(
            "farming.sell_farmland",
            "Select Farmland",
            "Sell");

    private void OnAddLivestockClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not Guid farmlandId)
        {
            return;
        }

        _main.QueueActionWithSelection(
            "farming.add_livestock",
            farmlandId.ToString());
        _viewModel.Refresh();
    }

    private void OnCloseClick(
        object? sender,
        RoutedEventArgs e) =>
        Close();

    private void OnKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close();
    }
}
