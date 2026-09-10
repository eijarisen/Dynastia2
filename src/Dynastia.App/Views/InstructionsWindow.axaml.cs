using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Dynastia.App.Views;

public partial class InstructionsWindow : Window
{
    public InstructionsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(
        object? sender,
        RoutedEventArgs e)
    {
        Close();
    }
}
