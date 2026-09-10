namespace Dynastia.StandardUI.Genealogy.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Dynastia.StandardUI.Genealogy.Contracts;
using Dynastia.StandardUI.Genealogy.Layout;
using Dynastia.StandardUI.Genealogy.Projection;
using Dynastia.StandardUI.Genealogy.Rendering;

public sealed class GenealogyPanel :
    UserControl,
    IDisposable
{
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

        var fit =
            new Button
            {
                Content =
                    "Fit Tree"
            };

        fit.Click +=
            (_, _) =>
                _canvas.FitTree();

        var reset =
            new Button
            {
                Content =
                    "100%"
            };

        reset.Click +=
            (_, _) =>
                _canvas.ResetZoom();

        var selected =
            new Button
            {
                Content =
                    "Center Selected"
            };

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
            new Button
            {
                Content =
                    "Center Founder"
            };

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

        var instructions =
            new TextBlock
            {
                Text =
                    "Mouse wheel: zoom · Drag empty space: pan · Click a person: select",
                VerticalAlignment =
                    VerticalAlignment.Center,
                Opacity =
                    0.65,
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        0)
            };

        var tools =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,

                Spacing =
                    6,

                Margin =
                    new Thickness(8),

                Children =
                {
                    fit,
                    reset,
                    selected,
                    founder,
                    instructions
                }
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
    }

    private void OnPersonClicked(
        Guid personId)
    {
        _selection.SelectPerson(
            personId);
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
                snapshot);

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

        // The uploaded feature separated topology and visual state,
        // but its original RefreshVisualOnly only invalidated old immutable
        // records. Replace node records while preserving coordinates/edges.
        _canvas.Layout =
            layout.WithPeople(
                people);

        _canvas.SelectedPersonId =
            _selection.SelectedPersonId;

        _canvas.InvalidateVisual();
    }
}
