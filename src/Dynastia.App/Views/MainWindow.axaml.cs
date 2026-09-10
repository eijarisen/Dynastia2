using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;

namespace Dynastia.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(
            InputElement.KeyDownEvent,
            OnWindowKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void OnWindowKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (DataContext
            is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (!viewModel.NextYearCommand.CanExecute(null))
            return;

        // Tunnel routing prevents a focused Button from also
        // consuming Enter and accidentally advancing twice.
        e.Handled = true;

        viewModel.NextYearCommand.Execute(null);
    }
}
