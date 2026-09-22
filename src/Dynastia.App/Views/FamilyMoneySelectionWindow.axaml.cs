using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Dynastia.App.Views;

public partial class FamilyMoneySelectionWindow : Window
{
    private readonly decimal _stepAmount;

    public FamilyMoneySelectionWindow()
        : this("Give Money", 1000m)
    {
    }

    public FamilyMoneySelectionWindow(
        string actionLabel,
        decimal maximumAmount,
        decimal minimumAmount = 1000m,
        decimal stepAmount = 1000m,
        string? explanation = null)
    {
        InitializeComponent();

        _stepAmount = Math.Max(1m, stepAmount);
        var minimumUnits = Math.Max(
            1m,
            Math.Ceiling(Math.Max(_stepAmount, minimumAmount) / _stepAmount));
        var maximumUnits = Math.Max(
            minimumUnits,
            Math.Floor(Math.Max(maximumAmount, minimumAmount) / _stepAmount));

        Title = actionLabel;
        TitleText.Text = actionLabel;
        ConfirmButton.Content = actionLabel;
        AmountSlider.Minimum = (double)minimumUnits;
        AmountSlider.Maximum = (double)maximumUnits;
        AmountSlider.Value = (double)minimumUnits;
        AmountSlider.SmallChange = 1;
        AmountSlider.LargeChange = Math.Max(1, (double)Math.Round(1000m / _stepAmount));
        AmountSlider.TickFrequency = 1;
        AmountSlider.IsSnapToTickEnabled = true;

        MinimumText.Text = $"{minimumUnits * _stepAmount:N0} zł";
        MaximumText.Text = $"{maximumUnits * _stepAmount:N0} zł";

        ExplanationText.Text = explanation
            ?? (actionLabel.StartsWith(
                    "Request",
                    StringComparison.OrdinalIgnoreCase)
                ? "Whether the other household agrees depends on the family relationship. The amount is rechecked when the action resolves."
                : "The gift is paid when the queued action resolves. The amount is rechecked against household wealth at that time.");

        RefreshAmount();
    }

    private decimal SelectedAmount =>
        (decimal)Math.Round(
            AmountSlider.Value,
            MidpointRounding.AwayFromZero)
        * _stepAmount;

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
