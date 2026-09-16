namespace Dynastia.StandardUI.Genealogy.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Dynastia.StandardUI.Genealogy.Contracts;
using Dynastia.StandardUI.Genealogy.Layout;
using Dynastia.StandardUI.Genealogy.Projection;
using Dynastia.StandardUI.Genealogy.Rendering;

public sealed class GenealogyPanel :
    UserControl,
    IDisposable
{
    private static readonly IBrush ToolBackground =
        new SolidColorBrush(
            Color.FromArgb(
                0xF0,
                6,
                17,
                17));

    private static readonly IBrush ToolBorder =
        new SolidColorBrush(
            Color.FromRgb(
                143,
                105,
                37));

    private static readonly IBrush ToolText =
        new SolidColorBrush(
            Color.FromRgb(
                244,
                230,
                195));

    private readonly IGenealogyDataSource _data;
    private readonly IGlobalSelectionService _selection;

    private readonly GenealogyProjectionBuilder
        _projection =
            new();

    private readonly GenealogyLayoutEngine
        _layoutEngine =
            new();

    private readonly GenealogyCanvas
        _canvas =
            new();

    private readonly HashSet<Guid>
        _collapsedPersonIds =
            [];

    private readonly ToggleButton
        _showAllSpouses;

    private readonly ToggleButton
        _daughtersFamilies;

    private readonly ToggleButton
        _maleLineageOnly;

    private readonly ToggleButton
        _portraitMode;

    private long _topologyVersion =
        long.MinValue;

    public GenealogyPanel(
        IGenealogyDataSource data,
        IGlobalSelectionService selection)
    {
        _data =
            data;

        _selection =
            selection;

        _canvas.HorizontalAlignment =
            HorizontalAlignment.Stretch;

        _canvas.VerticalAlignment =
            VerticalAlignment.Stretch;

        _canvas.PersonClicked +=
            OnPersonClicked;

        _canvas.PersonDoubleClicked +=
            OnPersonDoubleClicked;

        _canvas.PersonRightClicked +=
            OnPersonRightClicked;

        var fit =
            CreateToolButton(
                "Fit Tree");

        fit.Click +=
            (_, _) =>
                _canvas.FitTree();

        var reset =
            CreateToolButton(
                "100%");

        reset.Click +=
            (_, _) =>
                _canvas.ResetZoom();

        var selected =
            CreateToolButton(
                "Center Selected");

        selected.Click +=
            (_, _) =>
            {
                if (_selection.SelectedPersonId
                    is Guid id)
                {
                    _canvas.CenterOn(
                        id);
                }
            };

        var founder =
            CreateToolButton(
                "Center Tree");

        founder.Click +=
            (_, _) =>
            {
                var root =
                    _data
                        .GetSnapshot()
                        .FounderId;

                if (root is Guid id)
                {
                    _canvas.CenterOn(
                        id);
                }
            };

        _showAllSpouses =
            CreateFilter(
                "Show All Spouses",
                isChecked: false);

        _daughtersFamilies =
            CreateFilter(
                "Daughters' Families",
                isChecked: true);

        _maleLineageOnly =
            CreateFilter(
                "Male Lineage Only",
                isChecked: false);

        _portraitMode =
            CreatePortraitModeToggle();

        _showAllSpouses.Click +=
            OnFilterChanged;

        _daughtersFamilies.Click +=
            OnFilterChanged;

        _maleLineageOnly.Click +=
            OnFilterChanged;

        _portraitMode.Click +=
            OnPortraitModeChanged;

        var instructions =
            new TextBlock
            {
                Text =
                    "Mouse wheel: zoom · Drag empty space: pan · Hover: details · Click: select · Double-click: open household · Right-click: hide/show branch",

                VerticalAlignment =
                    VerticalAlignment.Center,

                Foreground =
                    ToolText,

                Opacity =
                    0.76,

                FontSize =
                    12,

                Margin =
                    new Thickness(
                        10,
                        0,
                        0,
                        0)
            };

        var toolsContent =
            new WrapPanel
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Children =
                {
                    fit,
                    reset,
                    selected,
                    founder,
                    CreateSeparator(),
                    _portraitMode,
                    _showAllSpouses,
                    _daughtersFamilies,
                    _maleLineageOnly,
                    instructions
                }
            };

        var tools =
            new Border
            {
                Background =
                    ToolBackground,

                BorderBrush =
                    ToolBorder,

                BorderThickness =
                    new Thickness(
                        0,
                        0,
                        0,
                        1),

                Padding =
                    new Thickness(
                        8,
                        7),

                Child =
                    toolsContent
            };

        var grid =
            new Grid();

        grid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        grid.RowDefinitions.Add(
            new RowDefinition(
                new GridLength(
                    1,
                    GridUnitType.Star)));

        Grid.SetRow(
            tools,
            0);

        Grid.SetRow(
            _canvas,
            1);

        grid.Children.Add(
            tools);

        grid.Children.Add(
            _canvas);

        Content =
            grid;

        _data.TopologyChanged +=
            OnTopologyChanged;

        _data.VisualStateChanged +=
            OnVisualStateChanged;

        _selection.SelectedPersonChanged +=
            OnSelectedChanged;

        RefreshTopology();
    }

    public void ForceRefreshTopology()
    {
        _topologyVersion =
            long.MinValue;

        RefreshTopology();
    }

    public event Action<Guid>?
        PersonDoubleClicked;

    public void FitTree()
    {
        _canvas.FitTree();
    }

    public void Dispose()
    {
        _data.TopologyChanged -=
            OnTopologyChanged;

        _data.VisualStateChanged -=
            OnVisualStateChanged;

        _selection.SelectedPersonChanged -=
            OnSelectedChanged;

        _canvas.PersonClicked -=
            OnPersonClicked;

        _canvas.PersonDoubleClicked -=
            OnPersonDoubleClicked;

        _canvas.PersonRightClicked -=
            OnPersonRightClicked;

        _canvas.Dispose();
    }

    private static Button CreateToolButton(
        string text)
    {
        return new Button
        {
            Content =
                new TextBlock
                {
                    Text =
                        text,

                    Foreground =
                        ToolText,

                    FontSize =
                        12,

                    FontWeight =
                        FontWeight.SemiBold
                },

            Background =
                new SolidColorBrush(
                    Color.FromArgb(
                        0xE4,
                        9,
                        31,
                        27)),

            BorderBrush =
                ToolBorder,

            BorderThickness =
                new Thickness(1),

            Padding =
                new Thickness(
                    11,
                    5),

            Margin =
                new Thickness(
                    0,
                    0,
                    7,
                    0),

            CornerRadius =
                new CornerRadius(4)
        };
    }

    private static ToggleButton CreateFilter(
        string text,
        bool isChecked)
    {
        var checkMark =
            new TextBlock
            {
                Text = "✓",
                Foreground = ToolText,
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = isChecked
            };

        var checkBox =
            new Border
            {
                Width = 15,
                Height = 15,
                BorderBrush = ToolText,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Background = Brushes.Transparent,
                Child = checkMark
            };

        var label =
            new TextBlock
            {
                Text = text,
                Foreground = ToolText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

        var filter =
            new ToggleButton
            {
                IsChecked = isChecked,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(3, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 11, 0),
                Content =
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        Children =
                        {
                            checkBox,
                            label
                        }
                    }
            };

        filter.Click +=
            (_, _) =>
                checkMark.IsVisible =
                    filter.IsChecked == true;

        return filter;
    }

    private static ToggleButton CreatePortraitModeToggle()
    {
        return new ToggleButton
        {
            IsChecked = true,
            Background = new SolidColorBrush(
                Color.FromArgb(0xE4, 9, 31, 27)),
            BorderBrush = ToolBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(11, 5),
            Margin = new Thickness(0, 0, 7, 0),
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock
            {
                Text = "View: Portraits",
                Foreground = ToolText,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold
            }
        };
    }

    private static Border CreateSeparator()
    {
        return new Border
        {
            Width =
                1,

            Height =
                22,

            Background =
                ToolBorder,

            Opacity =
                0.8,

            Margin =
                new Thickness(
                    3,
                    0,
                    8,
                    0)
        };
    }

    private void OnPersonClicked(
        Guid personId)
    {
        _selection.SelectPerson(
            personId);
    }

    private void OnPersonDoubleClicked(
        Guid personId)
    {
        _selection.SelectPerson(
            personId);

        PersonDoubleClicked?.Invoke(
            personId);
    }

    private void OnPersonRightClicked(
        Guid personId)
    {
        if (!_collapsedPersonIds.Remove(
                personId))
        {
            _collapsedPersonIds.Add(
                personId);
        }

        RebuildAndFitTree();
    }

    private void OnPortraitModeChanged(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        var usePortraits =
            _portraitMode.IsChecked == true;

        _canvas.UsePortraits =
            usePortraits;

        if (_portraitMode.Content is TextBlock label)
        {
            label.Text = usePortraits
                ? "View: Portraits"
                : "View: States";
        }
    }

    private void OnFilterChanged(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        RebuildAndFitTree();
    }

    private void OnTopologyChanged(
        object? sender,
        EventArgs e)
    {
        RefreshTopology();
    }

    private void OnVisualStateChanged(
        object? sender,
        EventArgs e)
    {
        RefreshVisualOnly();
    }

    private void OnSelectedChanged(
        object? sender,
        EventArgs e)
    {
        _canvas.SelectedPersonId =
            _selection.SelectedPersonId;

        _canvas.InvalidateVisual();
    }

    private void RebuildAndFitTree()
    {
        _topologyVersion =
            long.MinValue;

        RefreshTopology();

        _canvas.FitTree();
    }

    private GenealogyProjectionOptions
        GetProjectionOptions()
    {
        return new GenealogyProjectionOptions(
            ShowAllSpouses:
                _showAllSpouses.IsChecked == true,
            IncludeDaughtersFamilies:
                _daughtersFamilies.IsChecked == true,
            MaleLineageOnly:
                _maleLineageOnly.IsChecked == true,
            CollapsedPersonIds:
                _collapsedPersonIds);
    }

    private void RefreshTopology()
    {
        var snapshot =
            _data.GetSnapshot();

        if (snapshot.TopologyVersion
                == _topologyVersion
            && _canvas.Layout
                is not null)
        {
            RefreshVisualOnly();
            return;
        }

        _topologyVersion =
            snapshot.TopologyVersion;

        if (!snapshot.People.Any(
            person =>
                person.IsBloodline))
        {
            _canvas.Layout =
                null;

            _canvas.InvalidateVisual();

            return;
        }

        var graph =
            _projection.Build(
                snapshot,
                GetProjectionOptions());

        _canvas.Layout =
            _layoutEngine.Layout(
                graph);

        _canvas.SelectedPersonId =
            _selection.SelectedPersonId;

        _canvas.InvalidateVisual();
    }

    private void RefreshVisualOnly()
    {
        var layout =
            _canvas.Layout;

        if (layout is null)
            return;

        var snapshot =
            _data.GetSnapshot();

        var people =
            snapshot.People
                .ToDictionary(
                    person =>
                        person.Id);

        _canvas.Layout =
            layout.WithPeople(
                people);

        _canvas.InvalidateVisual();
    }
}
