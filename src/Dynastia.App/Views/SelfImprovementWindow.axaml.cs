using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;

namespace Dynastia.App.Views;

public partial class SelfImprovementWindow : Window
{
    public SelfImprovementWindow()
        : this(
            "selected person",
            Array.Empty<SelfImprovementOption>())
    {
    }

    public SelfImprovementWindow(
        string targetName,
        IReadOnlyList<SelfImprovementOption> options)
    {
        InitializeComponent();

        TargetText.Text =
            $"Improving {targetName}";

        OptionsList.ItemsSource =
            options;
    }

    private void OnQueueClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                DataContext: SelfImprovementOption option
            }
            && option.IsAvailable)
        {
            Close(option.ActionId);
        }
    }

    private void OnCloseClick(
        object? sender,
        RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnWindowKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close(null);
    }
}
