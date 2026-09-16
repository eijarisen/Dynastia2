namespace Dynastia.StandardUI.Map.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Dynastia.App.Map.Host;
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
    private readonly TownMapControl _canvas =
        new();

    private readonly TextBox _searchBox;
    private readonly TextBlock _searchStatus;
    private readonly TextBlock _townTitle;
    private readonly TextBlock _townSubtitle;
    private readonly TextBlock _townSummary;
    private readonly StackPanel _residentList;

    private TownMapSnapshot _snapshot;
    private string? _selectedTownId;

    public TownMapPanel(
        GameMapDataSource data,
        IGlobalSelectionService selection)
    {
        _data = data;
        _selection = selection;
        _snapshot = data.GetSnapshot();

        _canvas.HorizontalAlignment =
            HorizontalAlignment.Stretch;

        _canvas.VerticalAlignment =
            VerticalAlignment.Stretch;

        _canvas.SetSnapshot(
            _snapshot);

        _canvas.TownSelected +=
            OnTownSelected;

        _searchBox =
            new TextBox
            {
                Width = 220,
                Height = 31,
                Watermark = "Search town...",
                FontSize = 12,
                Background =
                    new SolidColorBrush(
                        Color.FromArgb(0xE8, 9, 31, 27)),
                Foreground = ToolText,
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
                _canvas.ResetView();

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
                    "Mouse wheel: zoom · Drag map: pan · Hover: town · Click: select",
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
        Grid.SetColumn(details, 1);

        mapArea.Children.Add(_canvas);
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

    public void ResetView() =>
        _canvas.ResetView();

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

        _searchBox.KeyDown -=
            OnSearchBoxKeyDown;

        _canvas.Dispose();
    }

    private void OnTownSelected(
        string townId)
    {
        _selectedTownId =
            townId;

        UpdateDetails(townId);
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
            string.Equals(
                town.Name,
                town.County,
                StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : town.County;

        var currentText =
            town.IsCurrentHouseholdTown
                ? "Yes"
                : "No";

        _townSummary.Text =
            $"Current household: {currentText}\n"
            + $"Dynasty members: {town.DynastyResidents.Count}\n"
            + $"Playable households: {town.PlayableHouseholds}\n"
            + $"Owned houses: {town.OwnedHouses}";

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
