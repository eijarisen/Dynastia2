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

    private async void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FamilyRelationActionViewModel action })
            return;

        string? propertyId = null;
        if (action.RequiresPropertySelection)
        {
            var options = _viewModel.GetGiveHouseOptions(action.RelativeId);
            if (options.Count == 0)
                return;

            var selector = new PropertySelectionWindow(
                "Select Property",
                "Give House",
                options);
            propertyId = await selector.ShowDialog<string?>(this);
            if (string.IsNullOrWhiteSpace(propertyId))
                return;
        }

        _viewModel.Queue(action, propertyId);
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
