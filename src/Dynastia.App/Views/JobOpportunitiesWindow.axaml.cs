using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;
using Dynastia.Contracts;

namespace Dynastia.App.Views;

public partial class JobOpportunitiesWindow : Window
{
    public JobOpportunitiesWindow()
    {
        InitializeComponent();
    }

    public JobOpportunitiesWindow(
        JobOpportunityDialogViewModel model)
        : this()
    {
        DataContext = model;
    }

    private void OnApplyClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                DataContext: JobOpportunityCardViewModel card
            })
        {
            Close(card.Opportunity);
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
