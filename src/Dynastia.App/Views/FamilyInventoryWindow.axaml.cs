using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;

namespace Dynastia.App.Views;

public partial class FamilyInventoryWindow : Window
{
    private readonly MainWindowViewModel _main;
    private readonly FamilyInventoryWindowViewModel _viewModel;

    public FamilyInventoryWindow()
        : this(null!)
    {
    }

    public FamilyInventoryWindow(
        MainWindowViewModel main,
        FamilyInventoryTab initialTab = FamilyInventoryTab.Money)
    {
        InitializeComponent();

        _main = main;
        _viewModel = main is null
            ? null!
            : new FamilyInventoryWindowViewModel(
                main,
                initialTab);

        if (main is not null)
            DataContext = _viewModel;

        AddHandler(
            KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private async void OnTakeLoanClick(
        object? sender,
        RoutedEventArgs e) =>
        await OpenLoanWindow(
            "loan.take",
            isGivingLoan: false);

    private async void OnGiveLoanClick(
        object? sender,
        RoutedEventArgs e) =>
        await OpenLoanWindow(
            "loan.give",
            isGivingLoan: true);

    private async Task OpenLoanWindow(
        string actionId,
        bool isGivingLoan)
    {
        var maximumPrincipal =
            _main.GetMaximumLoanPrincipal(actionId);

        var offers =
            _main.GetLoanOffers(
                isGivingLoan,
                maximumPrincipal);

        if (offers.Count == 0)
            return;

        var window =
            new LoanSelectionWindow(
                isGivingLoan,
                offers);

        var selection =
            await window.ShowDialog<LoanSelectionResult?>(this);

        if (selection is null)
            return;

        _main.QueueLoanAction(
            actionId,
            selection);

        _viewModel.Refresh();
    }

    private async void OnBuyHouseClick(
        object? sender,
        RoutedEventArgs e) =>
        await OpenPropertyWindow(
            "household.buy_house",
            "Select Town",
            "Buy");

    private async void OnSellHouseClick(
        object? sender,
        RoutedEventArgs e) =>
        await OpenPropertyWindow(
            "household.sell_house",
            "Select Property",
            "Sell");

    private async Task OpenPropertyWindow(
        string actionId,
        string title,
        string confirmText)
    {
        var options =
            _main.GetPropertySelectionOptions(
                actionId);

        if (options.Count == 0)
            return;

        var window =
            new PropertySelectionWindow(
                title,
                confirmText,
                options);

        var selectedId =
            await window.ShowDialog<string?>(this);

        if (string.IsNullOrWhiteSpace(selectedId))
            return;

        _main.QueueActionWithSelection(
            actionId,
            selectedId);

        _viewModel.Refresh();
    }

    private void OnBuyFarmlandClick(
        object? sender,
        RoutedEventArgs e)
    {
        _main.QueueFamilyInventoryAction(
            "farming.buy_farmland");
        _viewModel.Refresh();
    }

    private void OnSellFarmlandClick(
        object? sender,
        RoutedEventArgs e)
    {
        _main.QueueFamilyInventoryAction(
            "farming.sell_farmland");
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
