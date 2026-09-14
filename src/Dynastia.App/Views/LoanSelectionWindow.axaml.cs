using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;
using Dynastia.Contracts;

namespace Dynastia.App.Views;

public partial class LoanSelectionWindow :
    Window
{
    private readonly bool _isGivingLoan;
    private readonly Func<decimal, int, LoanTermsInfo?> _calculateTerms;

    public LoanSelectionWindow()
        : this(
            false,
            (_, _) => null,
            10000m)
    {
    }

    public LoanSelectionWindow(
        bool isGivingLoan,
        Func<decimal, int, LoanTermsInfo?> calculateTerms,
        decimal maximumPrincipal)
    {
        InitializeComponent();

        _isGivingLoan =
            isGivingLoan;

        _calculateTerms =
            calculateTerms;

        Title =
            isGivingLoan
                ? "Give a Loan"
                : "Take a Loan";

        TitleText.Text =
            Title;

        ConfirmButton.IdleSource =
            isGivingLoan
                ? "avares://Dynastia.App/Assets/UI/b_giveloan_idle.png"
                : "avares://Dynastia.App/Assets/UI/b_takeloan_idle.png";

        ConfirmButton.HoverSource =
            isGivingLoan
                ? "avares://Dynastia.App/Assets/UI/b_giveloan_hover.png"
                : "avares://Dynastia.App/Assets/UI/b_takeloan_hover.png";

        ConfirmButton.FallbackText =
            isGivingLoan
                ? "Give Loan"
                : "Take Loan";

        var maximumWholeThousands =
            Math.Clamp(
                Math.Floor(maximumPrincipal / 1000m),
                1m,
                10m);

        AmountSlider.Maximum =
            (double)maximumWholeThousands;

        AmountMaximumText.Text =
            $"{maximumWholeThousands * 1000m:N0} zł";

        if (AmountSlider.Value > AmountSlider.Maximum)
            AmountSlider.Value = AmountSlider.Maximum;

        RefreshTerms();
    }

    private decimal SelectedPrincipal =>
        (decimal)Math.Round(
            AmountSlider.Value,
            MidpointRounding.AwayFromZero)
        * 1000m;

    private int SelectedDuration =>
        (int)Math.Round(
            DurationSlider.Value,
            MidpointRounding.AwayFromZero);

    private void OnSliderValueChanged(
        object? sender,
        RangeBaseValueChangedEventArgs e)
    {
        RefreshTerms();
    }

    private void RefreshTerms()
    {
        if (AmountSlider is null
            || DurationSlider is null
            || ConfirmButton is null
            || AmountSelectionText is null
            || DurationSelectionText is null)
        {
            return;
        }

        AmountSelectionText.Text =
            $"{SelectedPrincipal:N0} zł";

        DurationSelectionText.Text =
            $"{SelectedDuration} " +
            $"{(SelectedDuration == 1 ? "year" : "years")}";

        var terms =
            _calculateTerms(
                SelectedPrincipal,
                SelectedDuration);

        ConfirmButton.IsEnabled =
            terms is not null;

        if (terms is null)
            return;

        AmountText.Text =
            _isGivingLoan
                ? $"Amount lent: {terms.Principal:N0} zł"
                : $"Amount received: {terms.Principal:N0} zł";

        DurationText.Text =
            $"Duration: {terms.DurationYears} " +
            $"{(terms.DurationYears == 1 ? "year" : "years")}";

        InterestText.Text =
            $"Total interest: {terms.TotalInterestRate:P1}";

        AnnualPaymentText.Text =
            _isGivingLoan
                ? $"Yearly repayment: {terms.AnnualPayment:N0} zł"
                : $"Yearly payment: {terms.AnnualPayment:N0} zł";

        TotalRepaymentText.Text =
            _isGivingLoan
                ? $"Total expected return: {terms.TotalRepayment:N0} zł"
                : $"Total amount to repay: {terms.TotalRepayment:N0} zł";
    }

    private void OnConfirmClick(
        object? sender,
        RoutedEventArgs e)
    {
        Confirm();
    }

    private void Confirm()
    {
        if (!ConfirmButton.IsEnabled)
            return;

        Close(
            new LoanSelectionResult(
                SelectedPrincipal,
                SelectedDuration));
    }

    private void OnCancelClick(
        object? sender,
        RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnWindowKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
            return;
        }

        if (e.Key == Key.Enter
            && ConfirmButton.IsEnabled)
        {
            e.Handled = true;
            Confirm();
        }
    }
}
