using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;
using Dynastia.Contracts;

namespace Dynastia.App.Views;

public partial class PotentialPartnersWindow : Window
{
    public PotentialPartnersWindow()
    {
        InitializeComponent();
    }

    public PotentialPartnersWindow(
        PotentialPartnerDialogViewModel model)
        : this()
    {
        DataContext = model;
    }

    private void OnApproachClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                DataContext: PotentialPartnerCardViewModel card
            })
        {
            Close(card.Candidate);
        }
    }

    private void OnCloseClick(
        object? sender,
        RoutedEventArgs e) =>
        Close(null);

    private void OnWindowKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }
}
