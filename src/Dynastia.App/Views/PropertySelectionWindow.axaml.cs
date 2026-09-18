using Avalonia.Controls;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;

namespace Dynastia.App.Views;

public partial class PropertySelectionWindow : Window
{
    private readonly IReadOnlyList<PropertySelectionOption> _allOptions;

    public PropertySelectionWindow()
        : this("Select Property", "Confirm", Array.Empty<PropertySelectionOption>())
    {
    }

    public PropertySelectionWindow(
        string title,
        string confirmLabel,
        IReadOnlyList<PropertySelectionOption> options,
        string? contextText = null,
        bool showSearch = true,
        bool compact = false)
    {
        InitializeComponent();
        _allOptions = options;
        Title = title;
        TitleText.Text = title;
        ContextText.Text = contextText ?? string.Empty;
        ContextText.IsVisible = !string.IsNullOrWhiteSpace(contextText);
        SearchBox.IsVisible = showSearch;
        ConfirmButton.Content = confirmLabel;

        if (compact)
        {
            Width = 900;
            Height = 610;
            MinWidth = 900;
            MinHeight = 610;
            OptionsList.Classes.Add("compact");
        }

        var isBuy = confirmLabel.Equals(
            "Buy",
            StringComparison.OrdinalIgnoreCase);
        var isSell = confirmLabel.Equals(
            "Sell",
            StringComparison.OrdinalIgnoreCase);
        var useHouseArtwork = isBuy || isSell;

        ImageActionPanel.IsVisible = useHouseArtwork;
        GenericActionPanel.IsVisible = !useHouseArtwork;

        if (useHouseArtwork)
        {
            ImageConfirmButton.IdleSource = isBuy
                ? "avares://Dynastia.App/Assets/UI/b_buyhouse_idle.png"
                : "avares://Dynastia.App/Assets/UI/b_sellhouse_idle.png";
            ImageConfirmButton.HoverSource = isBuy
                ? "avares://Dynastia.App/Assets/UI/b_buyhouse_hover.png"
                : "avares://Dynastia.App/Assets/UI/b_sellhouse_hover.png";
            ImageConfirmButton.FallbackText = isBuy
                ? "Buy a House"
                : "Sell a House";
        }

        RefreshFilter();
        UpdateConfirmAvailability();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshFilter();
    }

    private void RefreshFilter()
    {
        var search = SearchBox.Text?.Trim();
        var filtered = string.IsNullOrWhiteSpace(search)
            ? _allOptions
            : _allOptions
                .Where(option =>
                    option.SearchText.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                    || option.PrimaryText.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                    || option.SecondaryText.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        OptionsList.ItemsSource = filtered;
        if (filtered.Count == 1)
            OptionsList.SelectedIndex = 0;

        UpdateConfirmAvailability();
    }

    private void OnSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        UpdateConfirmAvailability();
    }

    private void UpdateConfirmAvailability()
    {
        var canConfirm =
            OptionsList.SelectedItem is PropertySelectionOption selected
            && selected.IsEnabled;

        ConfirmButton.IsEnabled = canConfirm;
        ImageConfirmButton.IsEnabled = canConfirm;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (OptionsList.SelectedItem is PropertySelectionOption selected
            && selected.IsEnabled)
        {
            Close(selected.Id);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
