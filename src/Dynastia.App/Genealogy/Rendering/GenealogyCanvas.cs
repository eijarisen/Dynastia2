namespace Dynastia.StandardUI.Genealogy.Rendering;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Dynastia.StandardUI.Genealogy.Layout;

public sealed class GenealogyCanvas :
    Control,
    IDisposable
{
    private const double Parallax1Factor =
        0.08;

    private const double Parallax2Factor =
        0.16;

    private const double Parallax3Factor =
        0.26;

    private static readonly IBrush SceneFill =
        new SolidColorBrush(
            Color.FromRgb(
                2,
                8,
                9));

    private static readonly IBrush TileFill =
        new SolidColorBrush(
            Color.FromArgb(
                0xD9,
                7,
                20,
                19));

    private static readonly IBrush TileSelectedFill =
        new SolidColorBrush(
            Color.FromArgb(
                0xE5,
                32,
                58,
                50));

    private static readonly IBrush TileText =
        new SolidColorBrush(
            Color.FromRgb(
                242,
                230,
                203));

    private static readonly IBrush TileSecondaryText =
        new SolidColorBrush(
            Color.FromRgb(
                202,
                190,
                160));

    private static readonly IBrush TileBorder =
        new SolidColorBrush(
            Color.FromRgb(
                93,
                75,
                45));

    private static readonly IBrush TileBloodlineBorder =
        new SolidColorBrush(
            Color.FromRgb(
                153,
                116,
                46));

    private static readonly IBrush TileActiveBorder =
        new SolidColorBrush(
            Color.FromRgb(
                242,
                201,
                76));

    private static readonly IBrush TileSelectedBorder =
        new SolidColorBrush(
            Color.FromRgb(
                217,
                176,
                74));

    private static readonly IBrush EdgeBrush =
        new SolidColorBrush(
            Color.FromArgb(
                0xE0,
                166,
                126,
                54));

    private static readonly IBrush HealthTrack =
        new SolidColorBrush(
            Color.FromArgb(
                0xB0,
                35,
                27,
                27));

    private static readonly IBrush HealthCritical =
        new SolidColorBrush(
            Color.FromRgb(
                198,
                40,
                40));

    private static readonly IBrush HealthPoor =
        new SolidColorBrush(
            Color.FromRgb(
                230,
                81,
                0));

    private static readonly IBrush HealthFair =
        new SolidColorBrush(
            Color.FromRgb(
                249,
                168,
                37));

    private static readonly IBrush HealthGood =
        new SolidColorBrush(
            Color.FromRgb(
                192,
                202,
                51));

    private static readonly IBrush HealthExcellent =
        new SolidColorBrush(
            Color.FromRgb(
                67,
                160,
                71));

    private static readonly IBrush TooltipFill =
        new SolidColorBrush(
            Color.FromArgb(
                0xF2,
                10,
                21,
                20));

    private static readonly IBrush TooltipBorder =
        new SolidColorBrush(
            Color.FromRgb(
                184,
                137,
                52));

    private static readonly IBrush TooltipText =
        new SolidColorBrush(
            Color.FromRgb(
                245,
                230,
                198));

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

    private readonly Bitmap? _background;
    private readonly Bitmap? _parallax1;
    private readonly Bitmap? _parallax2;
    private readonly Bitmap? _parallax3;

    private bool _panning;
    private bool _disposed;

    private Point _lastPointer;
    private Point _hoverPointer;

    private Guid? _hoveredPersonId;

    // Only manual tree dragging contributes to the parallax displacement.
    // Zoom-to-cursor and programmatic centering therefore do not make the
    // scenery jump unexpectedly.
    private Vector _parallaxTravel;

    public GenealogyCanvas()
    {
        ClipToBounds =
            true;

        Focusable =
            true;

        _background =
            LoadBitmap(
                "avares://Dynastia.App/Assets/Genealogy/tree_bg.png");

        _parallax1 =
            LoadBitmap(
                "avares://Dynastia.App/Assets/Genealogy/tree_parallax_1.png");

        _parallax2 =
            LoadBitmap(
                "avares://Dynastia.App/Assets/Genealogy/tree_parallax_2.png");

        _parallax3 =
            LoadBitmap(
                "avares://Dynastia.App/Assets/Genealogy/tree_parallax_3.png");

        PointerWheelChanged +=
            OnWheel;

        PointerPressed +=
            OnPointerPressed;

        PointerMoved +=
            OnPointerMoved;

        PointerReleased +=
            OnPointerReleased;

        PointerExited +=
            OnPointerExited;

        AffectsRender<GenealogyCanvas>(
            LayoutProperty,
            SelectedPersonIdProperty);
    }

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
        1;

    public Vector Pan { get; private set; } =
        new(40, 40);

    public double ColumnWidth { get; set; } =
        156;

    public double GenerationHeight { get; set; } =
        176;

    public Size NodeSize { get; set; } =
        new(116, 132);

    public override void Render(
        DrawingContext context)
    {
        base.Render(
            context);

        context.FillRectangle(
            SceneFill,
            Bounds);

        DrawScenery(
            context);

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
            var worldRect =
                NodeWorldRect(
                    node);

            if (!worldRect.Intersects(
                worldViewport))
            {
                continue;
            }

            DrawPerson(
                context,
                node,
                worldRect);
        }

        DrawHoveredTooltip(
            context);
    }

    public void FitTree()
    {
        if (Layout is null
            || Bounds.Width <= 0
            || Bounds.Height <= 0)
        {
            return;
        }

        var treeBounds =
            GetTreeWorldBounds();

        Zoom =
            Math.Clamp(
                Math.Min(
                    (Bounds.Width - 90)
                        / treeBounds.Width,
                    (Bounds.Height - 90)
                        / treeBounds.Height),
                0.12,
                2.5);

        Pan =
            new Vector(
                Bounds.X
                    + Bounds.Width / 2
                    - (
                        treeBounds.X
                        + treeBounds.Width / 2
                    ) * Zoom,
                Bounds.Y
                    + Bounds.Height / 2
                    - (
                        treeBounds.Y
                        + treeBounds.Height / 2
                    ) * Zoom);

        _parallaxTravel =
            default;

        InvalidateVisual();
    }

    public void ResetZoom()
    {
        Zoom =
            1;

        if (Layout is not null)
        {
            var treeBounds =
                GetTreeWorldBounds();

            Pan =
                new Vector(
                    Bounds.X
                        + Bounds.Width / 2
                        - (
                            treeBounds.X
                            + treeBounds.Width / 2
                        ),
                    Bounds.Y
                        + Bounds.Height / 2
                        - (
                            treeBounds.Y
                            + treeBounds.Height / 2
                        ));
        }
        else
        {
            Pan =
                new Vector(
                    40,
                    40);
        }

        _parallaxTravel =
            default;

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

        _parallaxTravel =
            default;

        InvalidateVisual();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed =
            true;

        _background?.Dispose();
        _parallax1?.Dispose();
        _parallax2?.Dispose();
        _parallax3?.Dispose();
    }

    private void DrawScenery(
        DrawingContext context)
    {
        DrawFixedImage(
            context,
            _background);

        DrawParallaxImage(
            context,
            _parallax1,
            Parallax1Factor);

        DrawParallaxImage(
            context,
            _parallax2,
            Parallax2Factor);

        DrawParallaxImage(
            context,
            _parallax3,
            Parallax3Factor);
    }

    private void DrawFixedImage(
        DrawingContext context,
        Bitmap? bitmap)
    {
        if (bitmap is null)
            return;

        context.DrawImage(
            bitmap,
            GetFullyVisibleImageRect(
                bitmap));
    }

    private void DrawParallaxImage(
        DrawingContext context,
        Bitmap? bitmap,
        double factor)
    {
        if (bitmap is null)
            return;

        var baseRect =
            GetFullyVisibleImageRect(
                bitmap);

        // Because the image is fitted with Uniform semantics, any leftover
        // viewport space is the only legal travel range. Clamping to that
        // slack guarantees that the complete layer remains on screen.
        var desiredX =
            _parallaxTravel.X
            * factor;

        var desiredY =
            _parallaxTravel.Y
            * factor;

        var minimumX =
            Bounds.Left
            - baseRect.Left;

        var maximumX =
            Bounds.Right
            - baseRect.Right;

        var minimumY =
            Bounds.Top
            - baseRect.Top;

        var maximumY =
            Bounds.Bottom
            - baseRect.Bottom;

        var offsetX =
            Math.Clamp(
                desiredX,
                minimumX,
                maximumX);

        var offsetY =
            Math.Clamp(
                desiredY,
                minimumY,
                maximumY);

        var destination =
            new Rect(
                baseRect.X + offsetX,
                baseRect.Y + offsetY,
                baseRect.Width,
                baseRect.Height);

        context.DrawImage(
            bitmap,
            destination);
    }

    private Rect GetFullyVisibleImageRect(
        Bitmap bitmap)
    {
        var imageSize =
            bitmap.Size;

        if (imageSize.Width <= 0
            || imageSize.Height <= 0
            || Bounds.Width <= 0
            || Bounds.Height <= 0)
        {
            return Bounds;
        }

        var scale =
            Math.Min(
                Bounds.Width
                    / imageSize.Width,
                Bounds.Height
                    / imageSize.Height);

        var width =
            imageSize.Width
            * scale;

        var height =
            imageSize.Height
            * scale;

        return new Rect(
            Bounds.X
                + Bounds.Width / 2
                - width / 2,
            Bounds.Y
                + Bounds.Height / 2
                - height / 2,
            width,
            height);
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
            selected
                ? TileSelectedFill
                : TileFill;

        IBrush border =
            selected
                ? TileSelectedBorder
                : node.Person.IsActiveHouseholdHead
                    ? TileActiveBorder
                    : node.Person.IsBloodline
                        ? TileBloodlineBorder
                        : TileBorder;

        var borderThickness =
            selected
                ? Math.Max(
                    2,
                    2.5 * Zoom)
                : node.Person.IsActiveHouseholdHead
                    ? Math.Max(
                        1.5,
                        2 * Zoom)
                    : Math.Max(
                        1,
                        Zoom);

        context.DrawRectangle(
            fill,
            new Pen(
                border,
                borderThickness),
            rect,
            Math.Max(
                3,
                5 * Zoom),
            Math.Max(
                3,
                5 * Zoom));

        var z =
            Zoom;

        DrawCenteredText(
            context,
            node.Person.AvatarText,
            rect,
            rect.Y + 5 * z,
            28 * z,
            FontWeight.Normal,
            TileText,
            maxLines:
                1);

        DrawCenteredText(
            context,
            node.Person.FirstName,
            rect,
            rect.Y + 38 * z,
            11.5 * z,
            FontWeight.SemiBold,
            TileText,
            maxLines:
                1);

        DrawCenteredText(
            context,
            node.Person.Surname,
            rect,
            rect.Y + 53 * z,
            11.5 * z,
            FontWeight.SemiBold,
            TileText,
            maxLines:
                1);

        var contentY =
            70.0;

        if (node.Person.IsAlive
            && node.Person.HealthValue
                is double health)
        {
            DrawHealthBar(
                context,
                rect,
                health,
                contentY * z);

            contentY +=
                10;
        }

        DrawCenteredText(
            context,
            node.Person.LifeSpanText,
            rect,
            rect.Y + contentY * z,
            9 * z,
            FontWeight.Normal,
            TileSecondaryText,
            maxLines:
                1);

        contentY +=
            14;

        DrawCenteredText(
            context,
            node.Person.TownText,
            rect,
            rect.Y + contentY * z,
            8.5 * z,
            FontWeight.Normal,
            TileSecondaryText,
            maxLines:
                1);

        contentY +=
            14;

        if (node.Person.ShowOccupation)
        {
            DrawCenteredText(
                context,
                node.Person.OccupationText,
                rect,
                rect.Y + contentY * z,
                8.2 * z,
                FontWeight.Normal,
                TileSecondaryText,
                maxLines:
                    1);
        }
    }

    private void DrawHealthBar(
        DrawingContext context,
        Rect tileRect,
        double health,
        double localY)
    {
        var z =
            Zoom;

        var width =
            42 * z;

        var height =
            Math.Max(
                3,
                5 * z);

        var x =
            tileRect.X
            + tileRect.Width / 2
            - width / 2;

        var y =
            tileRect.Y
            + localY;

        var track =
            new Rect(
                x,
                y,
                width,
                height);

        context.FillRectangle(
            HealthTrack,
            track);

        var percentage =
            Math.Clamp(
                health,
                0,
                100);

        if (percentage <= 0)
            return;

        var fill =
            new Rect(
                x,
                y,
                width
                    * percentage
                    / 100,
                height);

        context.FillRectangle(
            HealthBrush(
                percentage),
            fill);
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
                EdgeBrush,
                Math.Max(
                    1,
                    1.25 * Zoom));

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

    private void DrawHoveredTooltip(
        DrawingContext context)
    {
        if (_hoveredPersonId
            is not Guid hoveredId
            || Layout is null
            || _panning)
        {
            return;
        }

        var person =
            Layout.People
                .FirstOrDefault(
                    node =>
                        node.Person.Id
                        == hoveredId)
                ?.Person;

        if (person is null
            || string.IsNullOrWhiteSpace(
                person.InfoTooltipText))
        {
            return;
        }

        var lines =
            person.InfoTooltipText
                .Split(
                    Environment.NewLine,
                    StringSplitOptions
                        .RemoveEmptyEntries);

        if (lines.Length == 0)
            return;

        const double width =
            350;

        var height =
            18
            + lines.Length
                * 18;

        var x =
            _hoverPointer.X
            + 16;

        var y =
            _hoverPointer.Y
            + 16;

        if (x + width
            > Bounds.Right - 8)
        {
            x =
                _hoverPointer.X
                - width
                - 16;
        }

        if (y + height
            > Bounds.Bottom - 8)
        {
            y =
                _hoverPointer.Y
                - height
                - 16;
        }

        x =
            Math.Clamp(
                x,
                Bounds.Left + 8,
                Math.Max(
                    Bounds.Left + 8,
                    Bounds.Right
                        - width
                        - 8));

        y =
            Math.Clamp(
                y,
                Bounds.Top + 8,
                Math.Max(
                    Bounds.Top + 8,
                    Bounds.Bottom
                        - height
                        - 8));

        var rect =
            new Rect(
                x,
                y,
                width,
                height);

        context.DrawRectangle(
            TooltipFill,
            new Pen(
                TooltipBorder,
                1),
            rect,
            4,
            4);

        var textY =
            y + 8;

        foreach (var line in
            lines)
        {
            DrawLeftText(
                context,
                line,
                x + 10,
                textY,
                11,
                FontWeight.Normal,
                TooltipText,
                width - 20);

            textY +=
                18;
        }
    }

    private static void DrawCenteredText(
        DrawingContext context,
        string text,
        Rect rect,
        double y,
        double size,
        FontWeight weight,
        IBrush brush,
        int maxLines)
    {
        if (string.IsNullOrWhiteSpace(
            text))
        {
            return;
        }

        var padding =
            Math.Max(
                4,
                6 * size / 11);

        var formatted =
            CreateFormattedText(
                text,
                size,
                weight,
                brush);

        formatted.MaxTextWidth =
            Math.Max(
                1,
                rect.Width
                    - padding * 2);

        formatted.TextAlignment =
            TextAlignment.Center;

        formatted.MaxLineCount =
            Math.Max(
                1,
                maxLines);

        formatted.Trimming =
            TextTrimming.CharacterEllipsis;

        context.DrawText(
            formatted,
            new Point(
                rect.X + padding,
                y));
    }

    private static void DrawLeftText(
        DrawingContext context,
        string text,
        double x,
        double y,
        double size,
        FontWeight weight,
        IBrush brush,
        double maxWidth)
    {
        var formatted =
            CreateFormattedText(
                text,
                size,
                weight,
                brush);

        formatted.MaxTextWidth =
            Math.Max(
                1,
                maxWidth);

        formatted.MaxLineCount =
            1;

        formatted.Trimming =
            TextTrimming.CharacterEllipsis;

        context.DrawText(
            formatted,
            new Point(
                x,
                y));
    }

    private static FormattedText CreateFormattedText(
        string text,
        double size,
        FontWeight weight,
        IBrush brush)
    {
        return new FormattedText(
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
            brush);
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

    private Rect GetTreeWorldBounds()
    {
        if (Layout is null)
        {
            return new Rect(
                0,
                0,
                NodeSize.Width,
                NodeSize.Height);
        }

        var left =
            Layout.Bounds.Left
                * ColumnWidth
            - NodeSize.Width / 2;

        var top =
            Layout.Bounds.Top
                * GenerationHeight
            - NodeSize.Height / 2;

        var right =
            Layout.Bounds.Right
                * ColumnWidth
            + NodeSize.Width / 2;

        var bottom =
            Layout.Bounds.Bottom
                * GenerationHeight
            + NodeSize.Height / 2;

        return new Rect(
            left,
            top,
            Math.Max(
                NodeSize.Width,
                right - left),
            Math.Max(
                NodeSize.Height,
                bottom - top));
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
                0.12,
                3);

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

        _hoveredPersonId =
            null;

        _panning =
            true;

        e.Pointer.Capture(
            this);

        InvalidateVisual();
    }

    private void OnPointerMoved(
        object? sender,
        PointerEventArgs e)
    {
        var point =
            e.GetPosition(
                this);

        _hoverPointer =
            point;

        if (_panning)
        {
            var delta =
                point
                - _lastPointer;

            Pan +=
                delta;

            _parallaxTravel +=
                delta;

            _lastPointer =
                point;

            InvalidateVisual();

            return;
        }

        var hovered =
            HitTestPerson(
                point);

        if (_hoveredPersonId
            == hovered)
        {
            if (hovered is not null)
            {
                // The tooltip follows the pointer.
                InvalidateVisual();
            }

            return;
        }

        _hoveredPersonId =
            hovered;

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

        _hoveredPersonId =
            HitTestPerson(
                e.GetPosition(
                    this));

        InvalidateVisual();
    }

    private void OnPointerExited(
        object? sender,
        PointerEventArgs e)
    {
        if (_panning)
            return;

        _hoveredPersonId =
            null;

        InvalidateVisual();
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

    private static IBrush HealthBrush(
        double health)
    {
        return health switch
        {
            <= 20 =>
                HealthCritical,

            <= 40 =>
                HealthPoor,

            <= 60 =>
                HealthFair,

            <= 80 =>
                HealthGood,

            _ =>
                HealthExcellent
        };
    }

    private static Bitmap? LoadBitmap(
        string uriText)
    {
        var uri =
            new Uri(
                uriText,
                UriKind.Absolute);

        if (!AssetLoader.Exists(
            uri))
        {
            return null;
        }

        using var stream =
            AssetLoader.Open(
                uri);

        return new Bitmap(
            stream);
    }
}
