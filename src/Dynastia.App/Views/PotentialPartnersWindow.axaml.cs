using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;

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

    private void OnCandidatePointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || sender is not Border
            {
                DataContext: PotentialPartnerCardViewModel card
            })
        {
            return;
        }

        e.Handled = true;
        Close(card.Candidate);
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
