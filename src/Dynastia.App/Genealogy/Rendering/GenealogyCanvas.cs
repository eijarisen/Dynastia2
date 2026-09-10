namespace Dynastia.StandardUI.Genealogy.Rendering;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Dynastia.StandardUI.Genealogy.Layout;

public sealed class GenealogyCanvas : Control
{
    public static readonly StyledProperty<TreeLayout?>
        LayoutProperty =
            AvaloniaProperty.Register<
                GenealogyCanvas,
                TreeLayout?>(
                    nameof(Layout));

    public static readonly StyledProperty<Guid?>
        SelectedPersonIdProperty =
            AvaloniaProperty.Register<
                GenealogyCanvas,
                Guid?>(
                    nameof(SelectedPersonId));

    public TreeLayout? Layout
    {
        get =>
            GetValue(
                LayoutProperty);

        set =>
            SetValue(
                LayoutProperty,
                value);
    }

    public Guid? SelectedPersonId
    {
        get =>
            GetValue(
                SelectedPersonIdProperty);

        set =>
            SetValue(
                SelectedPersonIdProperty,
                value);
    }

    public event Action<Guid>?
        PersonClicked;

    public double Zoom { get; private set; } =
        1.0;

    public Vector Pan { get; private set; } =
        new(40, 40);

    public double ColumnWidth { get; set; } =
        150;

    public double GenerationHeight { get; set; } =
        130;

    public Size NodeSize { get; set; } =
        new(118, 66);

    private bool _panning;
    private Point _lastPointer;

    public GenealogyCanvas()
    {
        ClipToBounds =
            true;

        Focusable =
            true;

        PointerWheelChanged +=
            OnWheel;

        PointerPressed +=
            OnPointerPressed;

        PointerMoved +=
            OnPointerMoved;

        PointerReleased +=
            OnPointerReleased;

        AffectsRender<GenealogyCanvas>(
            LayoutProperty,
            SelectedPersonIdProperty);
    }

    public override void Render(
        DrawingContext context)
    {
        base.Render(
            context);

        context.FillRectangle(
            Brushes.Transparent,
            Bounds);

        var layout =
            Layout;

        if (layout is null)
            return;

        var worldViewport =
            ScreenRectToWorld(
                Bounds);

        foreach (var edge in
            layout.Edges)
        {
            DrawEdge(
                context,
                edge,
                worldViewport);
        }

        foreach (var node in
            layout.People)
        {
            var rect =
                NodeWorldRect(
                    node);

            if (!rect.Intersects(
                worldViewport))
            {
                continue;
            }

            DrawPerson(
                context,
                node,
                rect);
        }
    }

    public void FitTree()
    {
        if (Layout is null
            || Bounds.Width <= 0
            || Bounds.Height <= 0)
        {
            return;
        }

        var treeWidth =
            Math.Max(
                ColumnWidth,
                (Layout.Bounds.Width - 1)
                    * ColumnWidth
                + NodeSize.Width);

        var treeHeight =
            Math.Max(
                GenerationHeight,
                (Layout.Bounds.Height - 1)
                    * GenerationHeight
                + NodeSize.Height);

        Zoom =
            Math.Clamp(
                Math.Min(
                    (Bounds.Width - 60)
                        / treeWidth,
                    (Bounds.Height - 60)
                        / treeHeight),
                .1,
                2.5);

        Pan =
            new Vector(
                30,
                30);

        InvalidateVisual();
    }

    public void ResetZoom()
    {
        Zoom =
            1;

        Pan =
            new Vector(
                40,
                40);

        InvalidateVisual();
    }

    public void CenterOn(
        Guid personId)
    {
        if (Layout is null
            || !Layout.PositionsByPersonId.TryGetValue(
                personId,
                out var point))
        {
            return;
        }

        var world =
            GridToWorld(
                point);

        Pan =
            new Vector(
                Bounds.Width / 2
                    - world.X * Zoom,
                Bounds.Height / 2
                    - world.Y * Zoom);

        InvalidateVisual();
    }

    private void DrawPerson(
        DrawingContext context,
        PositionedPerson node,
        Rect worldRect)
    {
        var rect =
            WorldRectToScreen(
                worldRect);

        var selected =
            node.Person.Id
            == SelectedPersonId;

        var fill =
            node.Person.IsBloodline
                ? Brushes.White
                : Brushes.Gainsboro;

        var pen =
            new Pen(
                selected
                    ? Brushes.Gold
                    : node.Person.IsBloodline
                        ? Brushes.DodgerBlue
                        : Brushes.Gray,
                selected
                    ? 3
                    : 1);

        context.DrawRectangle(
            fill,
            pen,
            rect,
            6 * Zoom,
            6 * Zoom);

        if (node.Person.IsMaleLineage)
        {
            var marker =
                new Rect(
                    rect.X + 5 * Zoom,
                    rect.Y + 5 * Zoom,
                    8 * Zoom,
                    8 * Zoom);

            context.FillRectangle(
                Brushes.Black,
                marker);
        }

        var life =
            node.Person.IsAlive
                ? $"b. {node.Person.BirthYear}"
                : $"{node.Person.BirthYear}–" +
                  $"{node.Person.DeathYear?.ToString() ?? "?"}";

        DrawText(
            context,
            node.Person.DisplayName,
            rect.X + 9 * Zoom,
            rect.Y + 17 * Zoom,
            13 * Zoom,
            FontWeight.SemiBold);

        DrawText(
            context,
            life,
            rect.X + 9 * Zoom,
            rect.Y + 40 * Zoom,
            11 * Zoom,
            FontWeight.Normal);
    }

    private void DrawEdge(
        DrawingContext context,
        GenealogyEdge edge,
        Rect worldViewport)
    {
        var a =
            EdgeWorldPoint(
                edge.From,
                edge.Type,
                true);

        var b =
            EdgeWorldPoint(
                edge.To,
                edge.Type,
                false);

        var bounds =
            new Rect(
                Math.Min(
                    a.X,
                    b.X),
                Math.Min(
                    a.Y,
                    b.Y),
                Math.Abs(
                    a.X - b.X) + 1,
                Math.Abs(
                    a.Y - b.Y) + 1)
            .Inflate(
                10);

        if (!bounds.Intersects(
            worldViewport))
        {
            return;
        }

        var pen =
            new Pen(
                Brushes.Gray,
                Math.Max(
                    1,
                    Zoom));

        context.DrawLine(
            pen,
            WorldToScreen(a),
            WorldToScreen(b));
    }

    private Point EdgeWorldPoint(
        GridPoint point,
        GenealogyEdgeType type,
        bool from)
    {
        var center =
            GridToWorld(
                point);

        var halfWidth =
            NodeSize.Width / 2;

        var halfHeight =
            NodeSize.Height / 2;

        return type switch
        {
            GenealogyEdgeType.Partnership =>
                center
                + new Vector(
                    from
                        ? halfWidth
                        : -halfWidth,
                    0),

            GenealogyEdgeType.ParentChildVertical
                when from =>
                    center
                    + new Vector(
                        0,
                        halfHeight),

            GenealogyEdgeType.ParentChildVertical =>
                RailPoint(
                    point),

            GenealogyEdgeType.SiblingRail =>
                RailPoint(
                    point),

            GenealogyEdgeType.ChildDrop
                when !from =>
                    center
                    + new Vector(
                        0,
                        -halfHeight),

            GenealogyEdgeType.ChildDrop =>
                RailPoint(
                    point),

            _ =>
                center
        };
    }

    private Point RailPoint(
        GridPoint point)
    {
        return new Point(
            point.Column
                * ColumnWidth,
            point.Row
                * GenerationHeight
                - GenerationHeight / 2);
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        double x,
        double y,
        double size,
        FontWeight weight)
    {
        var formatted =
            new FormattedText(
                text,
                System.Globalization
                    .CultureInfo
                    .CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    FontFamily.Default,
                    FontStyle.Normal,
                    weight),
                size,
                Brushes.Black);

        context.DrawText(
            formatted,
            new Point(
                x,
                y));
    }

    private Rect NodeWorldRect(
        PositionedPerson node)
    {
        var center =
            GridToWorld(
                node.Grid);

        return new Rect(
            center.X
                - NodeSize.Width / 2,
            center.Y
                - NodeSize.Height / 2,
            NodeSize.Width,
            NodeSize.Height);
    }

    private Point GridToWorld(
        GridPoint point)
    {
        return new Point(
            point.Column
                * ColumnWidth,
            point.Row
                * GenerationHeight);
    }

    private Point WorldToScreen(
        Point point)
    {
        return new Point(
            point.X * Zoom
                + Pan.X,
            point.Y * Zoom
                + Pan.Y);
    }

    private Rect WorldRectToScreen(
        Rect rect)
    {
        return new Rect(
            rect.X * Zoom
                + Pan.X,
            rect.Y * Zoom
                + Pan.Y,
            rect.Width
                * Zoom,
            rect.Height
                * Zoom);
    }

    private Point ScreenToWorld(
        Point point)
    {
        return new Point(
            (point.X - Pan.X)
                / Zoom,
            (point.Y - Pan.Y)
                / Zoom);
    }

    private Rect ScreenRectToWorld(
        Rect rect)
    {
        var topLeft =
            ScreenToWorld(
                rect.TopLeft);

        var bottomRight =
            ScreenToWorld(
                rect.BottomRight);

        return new Rect(
            topLeft,
            bottomRight);
    }

    private void OnWheel(
        object? sender,
        PointerWheelEventArgs e)
    {
        var mouse =
            e.GetPosition(
                this);

        var before =
            ScreenToWorld(
                mouse);

        var factor =
            e.Delta.Y > 0
                ? 1.12
                : 1 / 1.12;

        Zoom =
            Math.Clamp(
                Zoom * factor,
                .1,
                3.0);

        Pan =
            new Vector(
                mouse.X
                    - before.X * Zoom,
                mouse.Y
                    - before.Y * Zoom);

        InvalidateVisual();

        e.Handled =
            true;
    }

    private void OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(
                this);

        _lastPointer =
            point.Position;

        if (!point.Properties
            .IsLeftButtonPressed)
        {
            return;
        }

        var hit =
            HitTestPerson(
                point.Position);

        if (hit is Guid personId)
        {
            PersonClicked?.Invoke(
                personId);

            e.Handled =
                true;

            return;
        }

        _panning =
            true;

        e.Pointer.Capture(
            this);
    }

    private void OnPointerMoved(
        object? sender,
        PointerEventArgs e)
    {
        if (!_panning)
            return;

        var point =
            e.GetPosition(
                this);

        Pan +=
            point
            - _lastPointer;

        _lastPointer =
            point;

        InvalidateVisual();
    }

    private void OnPointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        _panning =
            false;

        e.Pointer.Capture(
            null);
    }

    private Guid? HitTestPerson(
        Point screen)
    {
        if (Layout is null)
            return null;

        var world =
            ScreenToWorld(
                screen);

        for (var index =
                Layout.People.Count - 1;
            index >= 0;
            index--)
        {
            if (NodeWorldRect(
                    Layout.People[index])
                .Contains(
                    world))
            {
                return Layout
                    .People[index]
                    .Person.Id;
            }
        }

        return null;
    }
}
