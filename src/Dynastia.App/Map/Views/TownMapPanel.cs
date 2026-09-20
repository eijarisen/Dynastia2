namespace Dynastia.StandardUI.Map.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Dynastia.App.Map.Host;
using Dynastia.Contracts;
using Dynastia.StandardUI.Genealogy.Contracts;
using Dynastia.StandardUI.Map.Models;
using Dynastia.StandardUI.Map.Rendering;

public sealed class TownMapPanel :
    UserControl,
    IDisposable
{
    private static readonly IBrush ToolBackground =
        new SolidColorBrush(
            Color.FromArgb(0xF0, 5, 19, 18));

    private static readonly IBrush ToolBorder =
        new SolidColorBrush(
            Color.FromRgb(143, 105, 37));

    private static readonly IBrush ToolText =
        new SolidColorBrush(
            Color.FromRgb(244, 230, 195));

    private static readonly IBrush MutedText =
        new SolidColorBrush(
            Color.FromRgb(186, 174, 145));

    private static readonly IBrush Gold =
        new SolidColorBrush(
            Color.FromRgb(222, 183, 78));

    private readonly GameMapDataSource _data;
    private readonly IGlobalSelectionService _selection;
    private readonly ITownLifeService? _townLife;
    private readonly TownMapControl _canvas =
        new();

    private readonly TextBox _searchBox;
    private readonly TextBlock _searchStatus;
    private readonly TextBlock _townTitle;
    private readonly TextBlock _townSubtitle;
    private readonly TextBlock _townSummary;
    private readonly StackPanel _residentList;
    private readonly StackPanel _overlapOptions;
    private readonly Border _overlapPicker;

    private TownMapSnapshot _snapshot;
    private string? _selectedTownId;

    public TownMapPanel(
        GameMapDataSource data,
        IGlobalSelectionService selection,
        ITownLifeService? townLife = null)
    {
        _data = data;
        _selection = selection;
        _townLife = townLife;
        _snapshot = data.GetSnapshot();

        _canvas.HorizontalAlignment =
            HorizontalAlignment.Stretch;

        _canvas.VerticalAlignment =
            VerticalAlignment.Stretch;

        _canvas.SetSnapshot(
            _snapshot);

        _canvas.TownSelected +=
            OnTownSelected;

        _canvas.TownActivated +=
            OnTownActivated;

        _canvas.TownSelectionRequested +=
            OnTownSelectionRequested;

        _searchBox =
            new TextBox
            {
                Width = 220,
                Height = 31,
                PlaceholderText = "Search town...",
                FontSize = 12,
                Background =
                    new SolidColorBrush(
                        Color.FromRgb(244, 230, 195)),
                Foreground = Brushes.Black,
                BorderBrush = ToolBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4),
                Margin = new Thickness(0, 0, 6, 0)
            };

        _searchBox.KeyDown +=
            OnSearchBoxKeyDown;

        var find =
            CreateToolButton("Find");

        find.Click +=
            (_, _) =>
                SearchTown();

        var reset =
            CreateToolButton("Reset View");

        reset.Click +=
            (_, _) =>
                ResetView();

        var current =
            CreateToolButton("Current Household");

        current.Click +=
            (_, _) =>
                CenterCurrentHousehold();

        _searchStatus =
            new TextBlock
            {
                Foreground = MutedText,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

        var instructions =
            new TextBlock
            {
                Text =
                    "Mouse wheel: zoom · Drag map: pan · Hover: town · Click: select · Double-click: Town Affairs",
                Foreground = ToolText,
                Opacity = 0.72,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

        var toolsContent =
            new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    _searchBox,
                    find,
                    reset,
                    current,
                    CreateSeparator(),
                    instructions,
                    _searchStatus
                }
            };

        var tools =
            new Border
            {
                Background = ToolBackground,
                BorderBrush = ToolBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(9, 7),
                Child = toolsContent
            };

        _townTitle =
            new TextBlock
            {
                Text = "Select a town",
                FontSize = 21,
                FontWeight = FontWeight.Bold,
                Foreground = Gold,
                TextWrapping = TextWrapping.Wrap
            };

        _townSubtitle =
            new TextBlock
            {
                FontSize = 12,
                Foreground = MutedText,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

        _townSummary =
            new TextBlock
            {
                FontSize = 12.5,
                Foreground = ToolText,
                Margin = new Thickness(0, 14, 0, 0),
                LineHeight = 20,
                TextWrapping = TextWrapping.Wrap,
                Text =
                    "Click a town to inspect dynasty presence and property."
            };

        _residentList =
            new StackPanel
            {
                Spacing = 5,
                Margin = new Thickness(0, 10, 0, 0)
            };

        _overlapOptions =
            new StackPanel
            {
                Spacing = 2
            };

        _overlapPicker =
            new Border
            {
                Width = 230,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsVisible = false,
                Background = ToolBackground,
                BorderBrush = ToolBorder,
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8),
                Child =
                    new StackPanel
                    {
                        Spacing = 5,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Select location",
                                Foreground = Gold,
                                FontSize = 12.5,
                                FontWeight = FontWeight.Bold
                            },
                            new ScrollViewer
                            {
                                MaxHeight = 260,
                                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                Content = _overlapOptions
                            }
                        }
                    }
            };

        var legend =
            new TextBlock
            {
                Text =
                    "● current household\n● playable household\n● dynasty residents\n○ owned property",
                Foreground = MutedText,
                FontSize = 11.5,
                LineHeight = 18,
                Margin = new Thickness(0, 20, 0, 0)
            };

        var detailsContent =
            new StackPanel
            {
                Margin = new Thickness(17),
                Children =
                {
                    _townTitle,
                    _townSubtitle,
                    _townSummary,
                    _residentList,
                    legend
                }
            };

        var details =
            new Border
            {
                Width = 292,
                Background =
                    new SolidColorBrush(
                        Color.FromArgb(0xF0, 5, 18, 18)),
                BorderBrush = ToolBorder,
                BorderThickness = new Thickness(1, 0, 0, 0),
                Child =
                    new ScrollViewer
                    {
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = detailsContent
                    }
            };

        var mapArea =
            new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(
                        new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

        Grid.SetColumn(_canvas, 0);
        Grid.SetColumn(_overlapPicker, 0);
        Grid.SetColumn(details, 1);

        mapArea.Children.Add(_canvas);
        mapArea.Children.Add(_overlapPicker);
        mapArea.Children.Add(details);

        var root =
            new Grid();

        root.RowDefinitions.Add(
            new RowDefinition(GridLength.Auto));

        root.RowDefinitions.Add(
            new RowDefinition(
                new GridLength(1, GridUnitType.Star)));

        Grid.SetRow(tools, 0);
        Grid.SetRow(mapArea, 1);

        root.Children.Add(tools);
        root.Children.Add(mapArea);

        Content = root;

        if (_snapshot.CurrentHouseholdTownId
            is string currentTownId)
        {
            _canvas.SelectTown(currentTownId);
        }
    }

    public event Action<string>?
        TownActivated;

    public void ResetView()
    {
        _overlapPicker.IsVisible = false;
        _canvas.ResetView();
    }

    public void Refresh()
    {
        _snapshot =
            _data.GetSnapshot();

        _canvas.SetSnapshot(
            _snapshot);

        if (_selectedTownId is not null)
        {
            UpdateDetails(_selectedTownId);
        }
    }

    public void Dispose()
    {
        _canvas.TownSelected -=
            OnTownSelected;

        _canvas.TownActivated -=
            OnTownActivated;

        _canvas.TownSelectionRequested -=
            OnTownSelectionRequested;

        _searchBox.KeyDown -=
            OnSearchBoxKeyDown;

        _canvas.Dispose();
    }

    private void OnTownSelectionRequested(
        IReadOnlyList<string> townIds,
        Point pointer,
        bool activateAfterSelection)
    {
        _overlapOptions.Children.Clear();

        foreach (var townId in townIds)
        {
            var town = _snapshot.Towns.FirstOrDefault(item =>
                string.Equals(
                    item.TownId,
                    townId,
                    StringComparison.OrdinalIgnoreCase));

            if (town is null)
                continue;

            var button =
                new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 143, 105, 37)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(5, 4),
                    Content =
                        new TextBlock
                        {
                            Text = town.DisplayName,
                            Foreground = ToolText,
                            FontSize = 11.5,
                            TextWrapping = TextWrapping.Wrap
                        }
                };

            var selectedTownId = town.TownId;
            button.Click +=
                (_, _) =>
                {
                    _overlapPicker.IsVisible = false;
                    _canvas.SelectTown(selectedTownId);

                    if (activateAfterSelection)
                    {
                        OnTownActivated(selectedTownId);
                    }
                };

            _overlapOptions.Children.Add(button);
        }

        if (_overlapOptions.Children.Count == 0)
            return;

        var pickerHeight = Math.Min(
            290,
            42 + _overlapOptions.Children.Count * 31);
        var maxLeft = Math.Max(4, _canvas.Bounds.Width - 238);
        var maxTop = Math.Max(4, _canvas.Bounds.Height - pickerHeight - 4);

        _overlapPicker.Margin =
            new Thickness(
                Math.Clamp(pointer.X + 12, 4, maxLeft),
                Math.Clamp(pointer.Y + 12, 4, maxTop),
                0,
                0);
        _overlapPicker.IsVisible = true;
    }

    private void OnTownSelected(
        string townId)
    {
        _overlapPicker.IsVisible = false;
        _selectedTownId =
            townId;

        UpdateDetails(townId);
    }

    private void OnTownActivated(
        string townId)
    {
        _selectedTownId = townId;
        UpdateDetails(townId);
        TownActivated?.Invoke(townId);
    }

    private void UpdateDetails(
        string townId)
    {
        var town =
            _snapshot.Towns.FirstOrDefault(
                item =>
                    string.Equals(
                        item.TownId,
                        townId,
                        StringComparison.OrdinalIgnoreCase));

        if (town is null)
            return;

        _townTitle.Text =
            town.Name;

        _townSubtitle.Text =
            string.Empty;

        if (_townLife is not null)
        {
            try
            {
                var affairs =
                    _townLife.GetTownLife(townId);

                var institutions =
                    affairs.Institutions.Institutions
                        .Where(institution => institution.Tier > 0)
                        .Select(institution =>
                            $"  {institution.DisplayName}: Level {institution.Tier}")
                        .ToArray();

                var institutionText =
                    institutions.Length == 0
                        ? "  None"
                        : string.Join("\n", institutions);

                _townSummary.Text =
                    $"Region: {affairs.RegionName}\n"
                    + $"Settlement Type: {affairs.Town.SettlementClassDisplayName}\n"
                    + $"Population: {affairs.PopulationText}\n"
                    + $"Local Economy: {affairs.Prosperity.Index} — {affairs.Prosperity.Label} {affairs.Prosperity.TrendText}\n"
                    + $"Strong Fields: {affairs.LocalOpportunityText}\n"
                    + $"Regional Support: {affairs.RegionalOpportunityText}\n"
                    + $"Shocks: {affairs.Prosperity.ActiveShocksText}\n"
                    + "Institutions:\n"
                    + institutionText;
            }
            catch
            {
                _townSummary.Text =
                    $"Population: {town.Population:N0}";
            }
        }
        else
        {
            _townSummary.Text =
                $"Population: {town.Population:N0}";
        }

        _residentList.Children.Clear();

        if (town.DynastyResidents.Count == 0)
            return;

        _residentList.Children.Add(
            new TextBlock
            {
                Text = "Dynasty residents",
                Foreground = Gold,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 4, 0, 2)
            });

        foreach (var resident in
                 town.DynastyResidents)
        {
            var suffix =
                resident.IsActiveHouseholdHead
                    ? "  • current"
                    : resident.IsPlayableHouseholdHead
                        ? "  • household"
                        : string.Empty;

            var button =
                new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(3, 4),
                    Content =
                        new TextBlock
                        {
                            Text = resident.DisplayName + suffix,
                            Foreground = resident.IsActiveHouseholdHead
                                ? Gold
                                : ToolText,
                            FontSize = 11.5,
                            TextWrapping = TextWrapping.Wrap
                        }
                };

            var personId =
                resident.PersonId;

            button.Click +=
                (_, _) =>
                    _selection.SelectPerson(
                        personId);

            _residentList.Children.Add(button);
        }
    }

    private void CenterCurrentHousehold()
    {
        _overlapPicker.IsVisible = false;
        Refresh();

        if (_snapshot.CurrentHouseholdTownId
            is not string townId)
        {
            _searchStatus.Text =
                "No active household location.";
            return;
        }

        _canvas.CenterOnTown(
            townId,
            zoom: 4.0);

        _searchStatus.Text =
            string.Empty;
    }

    private void SearchTown()
    {
        _overlapPicker.IsVisible = false;

        var query =
            _searchBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            _searchStatus.Text =
                "Type a town name.";
            return;
        }

        var matches =
            _snapshot.Towns
                .Where(town =>
                    town.Name.Equals(
                        query,
                        StringComparison.CurrentCultureIgnoreCase)
                    || town.DisplayName.Equals(
                        query,
                        StringComparison.CurrentCultureIgnoreCase))
                .Concat(
                    _snapshot.Towns.Where(town =>
                        town.Name.StartsWith(
                            query,
                            StringComparison.CurrentCultureIgnoreCase)))
                .Concat(
                    _snapshot.Towns.Where(town =>
                        town.Name.Contains(
                            query,
                            StringComparison.CurrentCultureIgnoreCase)))
                .DistinctBy(town => town.TownId)
                .OrderByDescending(town => town.Population)
                .ToArray();

        if (matches.Length == 0)
        {
            _searchStatus.Text =
                "Town not found.";
            return;
        }

        var match =
            matches[0];

        _canvas.CenterOnTown(
            match.TownId,
            zoom: 5.0);

        _searchStatus.Text =
            matches.Length > 1
                ? $"Showing {match.DisplayName} ({matches.Length} matches)."
                : string.Empty;
    }

    private void OnSearchBoxKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        SearchTown();
    }

    private static Button CreateToolButton(
        string text)
    {
        return new Button
        {
            Content =
                new TextBlock
                {
                    Text = text,
                    Foreground = ToolText,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold
                },
            Background =
                new SolidColorBrush(
                    Color.FromArgb(0xE4, 9, 31, 27)),
            BorderBrush = ToolBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(11, 5),
            Margin = new Thickness(0, 0, 7, 0),
            CornerRadius = new CornerRadius(4)
        };
    }

    private static Border CreateSeparator()
    {
        return new Border
        {
            Width = 1,
            Height = 22,
            Background = ToolBorder,
            Opacity = 0.8,
            Margin = new Thickness(3, 0, 8, 0)
        };
    }
}
