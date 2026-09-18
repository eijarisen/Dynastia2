using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Dynastia.App.Views;

public partial class InstructionsWindow : Window
{
    public InstructionsWindow()
    {
        InitializeComponent();

        AddHandler(
            InputElement.KeyDownEvent,
            OnWindowKeyDown,
            RoutingStrategies.Tunnel);
    }

    private void OnWindowKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key is not Key.Escape and not Key.Enter)
            return;

        e.Handled = true;
        Close();
    }

    private void OnCloseClick(
        object? sender,
        RoutedEventArgs e)
    {
        Close();
    }
}
