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
        IReadOnlyList<PropertySelectionOption> options)
    {
        InitializeComponent();
        _allOptions = options;
        Title = title;
        TitleText.Text = title;
        ConfirmButton.Content = confirmLabel;
        RefreshFilter();
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
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (OptionsList.SelectedItem is PropertySelectionOption selected)
            Close(selected.Id);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
