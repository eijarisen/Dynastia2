using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;

namespace Dynastia.App.Views;

public partial class FamilyRelationsWindow : Window
{
    private readonly FamilyRelationsWindowViewModel _viewModel;

    public FamilyRelationsWindow()
        : this(null!)
    {
    }

    public FamilyRelationsWindow(MainWindowViewModel main)
    {
        InitializeComponent();
        _viewModel = main is null
            ? null!
            : new FamilyRelationsWindowViewModel(main);
        if (main is not null)
            DataContext = _viewModel;

        AddHandler(
            KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void OnHouseholdNameClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button
            {
                DataContext: FamilyRelationHouseholdViewModel household,
                IsEnabled: true
            })
        {
            return;
        }

        _viewModel.OpenPlayableHousehold(household);
    }

    private async void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FamilyRelationActionViewModel action })
            return;

        string? propertyId =
            action.HasInlinePropertySelection
                ? action.SelectedInlineProperty?.Id
                : null;
        decimal? moneyAmount = null;

        if (action.HasInlinePropertySelection
            && string.IsNullOrWhiteSpace(propertyId))
        {
            return;
        }

        if (action.RequiresPropertySelection)
        {
            var options = _viewModel.GetGiveHouseOptions(action.RelativeId);
            if (options.Count == 0)
                return;

            var selector = new PropertySelectionWindow(
                "Select Property",
                action.Label,
                options);
            propertyId = await selector.ShowDialog<string?>(this);
            if (string.IsNullOrWhiteSpace(propertyId))
                return;
        }

        if (action.RequiresMoneySelection)
        {
            var maximum =
                _viewModel.GetMoneyMaximum(action);

            if (maximum < 1000m)
                return;

            var selector =
                new FamilyMoneySelectionWindow(
                    action.Label,
                    maximum);

            moneyAmount =
                await selector.ShowDialog<decimal?>(this);

            if (moneyAmount is null)
                return;
        }

        if (_viewModel.Queue(
            action,
            propertyId,
            moneyAmount))
        {
            Close();
        }
    }


    private void OnJusticeActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button
            {
                DataContext: FamilyRelationJusticeActionViewModel action,
                IsEnabled: true
            })
        {
            return;
        }

        if (_viewModel.QueueJustice(action))
            Close();
    }


    private async void OnConnectionActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: HouseholdConnectionActionViewModel action })
            return;

        string? propertyId = null;
        decimal? moneyAmount = null;

        if (action.RequiresPropertySelection)
        {
            var options = _viewModel.GetConnectionPropertyOptions(action);
            if (options.Count == 0)
                return;

            var selector = new PropertySelectionWindow(
                "Select Property",
                action.Label,
                options);
            propertyId = await selector.ShowDialog<string?>(this);
            if (string.IsNullOrWhiteSpace(propertyId))
                return;
        }

        if (action.RequiresMoneySelection)
        {
            var maximum = _viewModel.GetConnectionMoneyMaximum(action);
            if (maximum < 1000m)
                return;

            var selector = new FamilyMoneySelectionWindow(action.Label, maximum);
            moneyAmount = await selector.ShowDialog<decimal?>(this);
            if (moneyAmount is null)
                return;
        }

        if (_viewModel.QueueConnection(action, propertyId, moneyAmount))
            Close();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        e.Handled = true;
        Close();
    }
}
