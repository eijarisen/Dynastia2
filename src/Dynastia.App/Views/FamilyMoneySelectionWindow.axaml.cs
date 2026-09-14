using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Dynastia.App.Views;

public partial class FamilyMoneySelectionWindow : Window
{
    public FamilyMoneySelectionWindow()
        : this("Give Money", 1000m)
    {
    }

    public FamilyMoneySelectionWindow(
        string actionLabel,
        decimal maximumAmount)
    {
        InitializeComponent();

        var maximumThousands =
            Math.Max(
                1m,
                Math.Floor(maximumAmount / 1000m));

        Title = actionLabel;
        TitleText.Text = actionLabel;
        ConfirmButton.Content = actionLabel;
        AmountSlider.Maximum =
            (double)maximumThousands;
        MaximumText.Text =
            $"{maximumThousands * 1000m:N0} zł";

        ExplanationText.Text =
            actionLabel.StartsWith(
                "Request",
                StringComparison.OrdinalIgnoreCase)
                ? "Whether the other household agrees depends on the family relationship. The amount is rechecked when the action resolves."
                : "The gift is paid when the queued action resolves. The amount is rechecked against household wealth at that time.";

        RefreshAmount();
    }

    private decimal SelectedAmount =>
        (decimal)Math.Round(
            AmountSlider.Value,
            MidpointRounding.AwayFromZero)
        * 1000m;

    private void OnAmountChanged(
        object? sender,
        RangeBaseValueChangedEventArgs e)
    {
        RefreshAmount();
    }

    private void RefreshAmount()
    {
        if (AmountText is null)
            return;

        AmountText.Text =
            $"{SelectedAmount:N0} zł";
    }

    private void OnConfirmClick(
        object? sender,
        RoutedEventArgs e)
    {
        Close(SelectedAmount);
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
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Close(SelectedAmount);
        }
    }
}
