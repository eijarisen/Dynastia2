using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;
using Dynastia.Contracts;

namespace Dynastia.App.Views;

public partial class LoanSelectionWindow : Window
{
    public LoanSelectionWindow()
        : this(false, Array.Empty<LoanOfferInfo>())
    {
    }

    public LoanSelectionWindow(
        bool isGivingLoan,
        IReadOnlyList<LoanOfferInfo> offers)
    {
        InitializeComponent();

        Title = isGivingLoan
            ? "Give a Loan"
            : "Take a Loan";

        TitleText.Text = Title;
        IntroText.Text = isGivingLoan
            ? "Choose one available lending offer. The person is an offer profile only and will not enter the simulation."
            : "Choose one available borrowing offer. The person is an offer profile only and will not enter the simulation.";

        OffersList.ItemsSource = offers
            .Select(offer => new LoanOfferCardViewModel(offer, isGivingLoan))
            .ToList();
    }

    private void OnOfferClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                DataContext: LoanOfferCardViewModel offer
            })
        {
            Close(offer.ToSelection());
        }
    }

    private void OnCancelClick(
        object? sender,
        RoutedEventArgs e) =>
        Close(null);

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
