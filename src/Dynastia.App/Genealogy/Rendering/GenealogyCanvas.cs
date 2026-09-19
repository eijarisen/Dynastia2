namespace Dynastia.StandardUI.Genealogy.Rendering;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Dynastia.StandardUI.Genealogy.Layout;

public sealed partial class GenealogyCanvas :
    Control,
    IDisposable
{
    private const double Parallax1Factor =
        0.0075;

    private const double Parallax2Factor =
        0.05;

    private const double Parallax3Factor =
        0.10;

    // All scenery is deliberately zoomed beyond a simple "fit" so the
    // viewport is fully covered and parallax layers have safe travel room
    // in both horizontal and vertical directions.
    private const double BackgroundZoom =
        1.08;

    private const double ParallaxZoom =
        1.18;

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

    private static readonly IBrush DeceasedSepiaOverlay =
        new SolidColorBrush(
            Color.FromArgb(
                0x52,
                139,
                98,
                47));

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

    private static readonly IBrush SatisfactionMiserable =
        new SolidColorBrush(
            Color.FromRgb(198, 40, 40));

    private static readonly IBrush SatisfactionUnhappy =
        new SolidColorBrush(
            Color.FromRgb(230, 81, 0));

    private static readonly IBrush SatisfactionContent =
        new SolidColorBrush(
            Color.FromRgb(226, 201, 76));

    private static readonly IBrush SatisfactionSatisfied =
        new SolidColorBrush(
            Color.FromRgb(154, 205, 50));

    private static readonly IBrush SatisfactionThriving =
        new SolidColorBrush(
            Color.FromRgb(67, 160, 71));

    private static readonly IBrush SatisfactionNeutral =
        new SolidColorBrush(
            Color.FromRgb(191, 174, 139));

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

    public static readonly StyledProperty<bool>
        UsePortraitsProperty =
            AvaloniaProperty.Register<
                GenealogyCanvas,
                bool>(
                    nameof(UsePortraits),
                    defaultValue: true);

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
            SelectedPersonIdProperty,
            UsePortraitsProperty);
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

    public bool UsePortraits
    {
        get => GetValue(UsePortraitsProperty);
        set => SetValue(UsePortraitsProperty, value);
    }

    public event Action<Guid>?
        PersonClicked;

    public event Action<Guid>?
        PersonDoubleClicked;

    public event Action<Guid>?
        PersonRightClicked;

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

}
