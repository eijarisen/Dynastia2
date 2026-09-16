namespace Dynastia.StandardUI.Map.Rendering;

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Dynastia.StandardUI.Map.Models;

public sealed class TownMapControl :
    Control,
    IDisposable
{
    private const double MinimumZoom = 1.0;
    private const double MaximumZoom = 20.0;
    private const double ZoomStep = 1.12;

    // The supplied artwork contains generous decorative margins. These
    // normalized bounds describe the actual geographic Poland silhouette
    // inside map.png. Projected town bounds are fitted inside this rectangle.
    private const double GeographyLeft = 0.1914;
    private const double GeographyTop = 0.0382;
    private const double GeographyRight = 0.7871;
    private const double GeographyBottom = 0.9040;

    private static readonly IBrush SceneFill =
        new SolidColorBrush(
            Color.FromRgb(2, 12, 13));

    private static readonly IBrush OrdinaryTownFill =
        new SolidColorBrush(
            Color.FromArgb(0xEE, 214, 196, 126));

    private static readonly IBrush DynastyTownFill =
        new SolidColorBrush(
            Color.FromRgb(236, 196, 91));

    private static readonly IBrush PlayableTownFill =
        new SolidColorBrush(
            Color.FromRgb(255, 220, 112));

    private static readonly IBrush CurrentTownFill =
        new SolidColorBrush(
            Color.FromRgb(255, 226, 124));

    private static readonly IBrush PropertyBrush =
        new SolidColorBrush(
            Color.FromRgb(205, 224, 201));

    private static readonly IBrush LabelBrush =
        new SolidColorBrush(
            Color.FromRgb(241, 226, 190));

    private static readonly IBrush LabelShadow =
        new SolidColorBrush(
            Color.FromArgb(0xD9, 2, 9, 9));

    private static readonly IBrush HoverBrush =
        new SolidColorBrush(
            Color.FromRgb(250, 239, 194));

    private static readonly IBrush SelectedBrush =
        new SolidColorBrush(
            Color.FromRgb(255, 207, 73));

    private static readonly IBrush TooltipFill =
        new SolidColorBrush(
            Color.FromArgb(0xF2, 5, 20, 19));

    private static readonly IBrush TooltipBorder =
        new SolidColorBrush(
            Color.FromRgb(177, 134, 55));

    private readonly Bitmap? _background;
    private readonly Dictionary<string, Point>
        _townWorldPositions =
            new(StringComparer.OrdinalIgnoreCase);

    private TownMapSnapshot? _snapshot;
    private string? _selectedTownId;
    private string? _hoveredTownId;

    private bool _panning;
    private bool _disposed;
    private Point _lastPointer;
    private Point _hoverPointer;

    private double _centerX;
    private double _centerY;

    public TownMapControl()
    {
        ClipToBounds = true;
        Focusable = true;

        _background =
            LoadBitmap(
                "avares://Dynastia.App/Assets/Map/map.png");

        var world =
            WorldSize;

        _centerX =
            world.Width / 2.0;

        _centerY =
            world.Height / 2.0;

        PointerWheelChanged +=
            OnPointerWheelChanged;

        PointerPressed +=
            OnPointerPressed;

        PointerMoved +=
            OnPointerMoved;

        PointerReleased +=
            OnPointerReleased;

        PointerExited +=
            OnPointerExited;

        PointerCaptureLost +=
            OnPointerCaptureLost;
    }

    public double Zoom { get; private set; } =
        MinimumZoom;

    public string? SelectedTownId =>
        _selectedTownId;

    public event Action<string>?
        TownSelected;

    public void SetSnapshot(
        TownMapSnapshot snapshot)
    {
        _snapshot =
            snapshot;

        RebuildTownWorldPositions();

        if (_selectedTownId is not null
            && !_townWorldPositions.ContainsKey(
                _selectedTownId))
        {
            _selectedTownId = null;
        }

        InvalidateVisual();
    }

    public void ResetView()
    {
        var world =
            WorldSize;

        Zoom =
            MinimumZoom;

        _centerX =
            world.Width / 2.0;

        _centerY =
            world.Height / 2.0;

        ClampCamera();
        InvalidateVisual();
    }

    public bool SelectTown(
        string townId,
        bool center = false,
        double? zoom = null)
    {
        if (!_townWorldPositions.TryGetValue(
                townId,
                out var position))
        {
            return false;
        }

        _selectedTownId =
            townId;

        if (zoom is double requestedZoom)
        {
            Zoom =
                Math.Clamp(
                    requestedZoom,
                    MinimumZoom,
                    MaximumZoom);
        }

        if (center)
        {
            _centerX =
                position.X;

            _centerY =
                position.Y;

            ClampCamera();
        }

        InvalidateVisual();
        TownSelected?.Invoke(townId);
        return true;
    }

    public bool CenterOnTown(
        string townId,
        double zoom = 4.0) =>
            SelectTown(
                townId,
                center: true,
                zoom);

    public override void Render(
        DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(
            SceneFill,
            Bounds);

        ClampCamera();
        DrawMapImage(context);
        DrawTowns(context);
        DrawHoverTooltip(context);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        PointerWheelChanged -=
            OnPointerWheelChanged;

        PointerPressed -=
            OnPointerPressed;

        PointerMoved -=
            OnPointerMoved;

        PointerReleased -=
            OnPointerReleased;

        PointerExited -=
            OnPointerExited;

        PointerCaptureLost -=
            OnPointerCaptureLost;

        _background?.Dispose();
    }

    private Size WorldSize =>
        _background?.Size
        ?? new Size(2048, 1152);

    private double FitScale
    {
        get
        {
            var world =
                WorldSize;

            if (Bounds.Width <= 0
                || Bounds.Height <= 0
                || world.Width <= 0
                || world.Height <= 0)
            {
                return 1.0;
            }

            return Math.Min(
                Bounds.Width / world.Width,
                Bounds.Height / world.Height);
        }
    }

    private double ActualScale =>
        FitScale * Zoom;

    private void DrawMapImage(
        DrawingContext context)
    {
        if (_background is null)
            return;

        var topLeft =
            WorldToScreen(
                new Point(0, 0));

        var scale =
            ActualScale;

        var destination =
            new Rect(
                topLeft.X,
                topLeft.Y,
                WorldSize.Width * scale,
                WorldSize.Height * scale);

        context.DrawImage(
            _background,
            destination);
    }

    private void DrawTowns(
        DrawingContext context)
    {
        var snapshot =
            _snapshot;

        if (snapshot is null)
            return;

        var labelRects =
            new List<Rect>();

        var visibleTowns =
            snapshot.Towns
                .Where(item =>
                    _townWorldPositions.ContainsKey(
                        item.TownId))
                .OrderByDescending(
                    LabelPriority)
                .ThenByDescending(
                    item => item.Population)
                .ToArray();

        foreach (var item in visibleTowns)
        {
            var world =
                _townWorldPositions[item.TownId];

            var screen =
                WorldToScreen(world);

            if (!IsNearViewport(screen, 24))
                continue;

            DrawTownMarker(
                context,
                item,
                screen);

            if (!ShouldDrawLabel(item))
                continue;

            DrawTownLabel(
                context,
                item,
                screen,
                labelRects);
        }
    }

    private void DrawTownMarker(
        DrawingContext context,
        TownMapItem item,
        Point screen)
    {
        var populationRadius =
            PopulationRadius(item.Population);

        var stateRadius =
            item.IsCurrentHouseholdTown
                ? 5.9
                : item.HasPlayableHousehold
                    ? 5.0
                    : item.HasDynastyResidents
                        ? 4.1
                        : 0.0;

        var radius =
            Math.Max(
                populationRadius,
                stateRadius);

        var fill =
            item.IsCurrentHouseholdTown
                ? CurrentTownFill
                : item.HasPlayableHousehold
                    ? PlayableTownFill
                    : item.HasDynastyResidents
                        ? DynastyTownFill
                        : OrdinaryTownFill;

        context.DrawEllipse(
            fill,
            new Pen(
                LabelShadow,
                1),
            screen,
            radius,
            radius);

        if (item.HasOwnedHouse)
        {
            context.DrawEllipse(
                null,
                new Pen(
                    PropertyBrush,
                    1.5),
                screen,
                radius + 2.4,
                radius + 2.4);
        }

        if (string.Equals(
                item.TownId,
                _hoveredTownId,
                StringComparison.OrdinalIgnoreCase))
        {
            context.DrawEllipse(
                null,
                new Pen(
                    HoverBrush,
                    1.8),
                screen,
                radius + 4.2,
                radius + 4.2);
        }

        if (string.Equals(
                item.TownId,
                _selectedTownId,
                StringComparison.OrdinalIgnoreCase))
        {
            context.DrawEllipse(
                null,
                new Pen(
                    SelectedBrush,
                    2.2),
                screen,
                radius + 6.2,
                radius + 6.2);
        }
    }

    private void DrawTownLabel(
        DrawingContext context,
        TownMapItem item,
        Point screen,
        List<Rect> occupied)
    {
        var forced =
            IsForcedLabel(item);

        var width =
            Math.Clamp(
                14.0 + item.Name.Length * 6.6,
                45,
                180);

        var height =
            17.0;

        var rect =
            new Rect(
                screen.X + 7,
                screen.Y - height / 2.0,
                width,
                height);

        if (!forced
            && occupied.Any(existing =>
                existing.Intersects(rect)))
        {
            return;
        }

        occupied.Add(rect);

        var text =
            CreateFormattedText(
                item.Name,
                forced
                    ? 12.5
                    : 11.5,
                forced
                    ? FontWeight.SemiBold
                    : FontWeight.Normal,
                forced
                    ? HoverBrush
                    : LabelBrush);

        context.DrawText(
            text,
            new Point(
                rect.X,
                rect.Y));
    }

    private void DrawHoverTooltip(
        DrawingContext context)
    {
        if (_snapshot is null
            || _hoveredTownId is null)
        {
            return;
        }

        var item =
            _snapshot.Towns.FirstOrDefault(
                town =>
                    string.Equals(
                        town.TownId,
                        _hoveredTownId,
                        StringComparison.OrdinalIgnoreCase));

        if (item is null)
            return;

        var detailParts =
            new List<string>();

        if (item.DynastyResidents.Count > 0)
        {
            detailParts.Add(
                $"dynasty: {item.DynastyResidents.Count}");
        }

        if (item.OwnedHouses > 0)
        {
            detailParts.Add(
                $"houses: {item.OwnedHouses}");
        }

        var detail =
            detailParts.Count == 0
                ? string.Empty
                : " · " + string.Join(" · ", detailParts);

        var textValue =
            item.Name + detail;

        var width =
            Math.Clamp(
                28.0 + textValue.Length * 6.4,
                90,
                330);

        const double height =
            32;

        var x =
            Math.Min(
                _hoverPointer.X + 15,
                Math.Max(
                    4,
                    Bounds.Width - width - 6));

        var y =
            Math.Min(
                _hoverPointer.Y + 15,
                Math.Max(
                    4,
                    Bounds.Height - height - 6));

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

        context.DrawText(
            CreateFormattedText(
                textValue,
                11.5,
                FontWeight.SemiBold,
                LabelBrush),
            new Point(
                rect.X + 9,
                rect.Y + 8));
    }

    private static double PopulationRadius(
        int population) =>
        population switch
        {
            >= 250000 => 5.4,
            >= 100000 => 4.7,
            >= 50000 => 4.1,
            >= 20000 => 3.6,
            >= 5000 => 3.1,
            _ => 2.6
        };

    private bool ShouldDrawLabel(
        TownMapItem item)
    {
        if (IsForcedLabel(item))
            return true;

        if (Zoom < 1.45)
            return false;

        if (Zoom < 2.4)
        {
            return item.Population >= 100000
                   || item.HasPlayableHousehold
                   || item.HasDynastyResidents;
        }

        if (Zoom < 4.0)
        {
            return item.Population >= 20000
                   || item.HasDynastyResidents;
        }

        if (Zoom < 6.0)
        {
            return item.Population >= 5000
                   || item.HasDynastyResidents;
        }

        return true;
    }

    private bool IsForcedLabel(
        TownMapItem item) =>
            item.IsCurrentHouseholdTown
            || string.Equals(
                item.TownId,
                _selectedTownId,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                item.TownId,
                _hoveredTownId,
                StringComparison.OrdinalIgnoreCase);

    private static int LabelPriority(
        TownMapItem item) =>
            (item.IsCurrentHouseholdTown ? 10000 : 0)
            + (item.HasPlayableHousehold ? 5000 : 0)
            + (item.HasDynastyResidents ? 2500 : 0)
            + (item.HasOwnedHouse ? 1000 : 0)
            + Math.Min(
                item.Population / 1000,
                900);

    private bool IsNearViewport(
        Point point,
        double padding) =>
            point.X >= -padding
            && point.Y >= -padding
            && point.X <= Bounds.Width + padding
            && point.Y <= Bounds.Height + padding;

    private void RebuildTownWorldPositions()
    {
        _townWorldPositions.Clear();

        var snapshot =
            _snapshot;

        if (snapshot is null)
            return;

        var world =
            WorldSize;

        var content =
            new Rect(
                world.Width * GeographyLeft,
                world.Height * GeographyTop,
                world.Width
                    * (GeographyRight - GeographyLeft),
                world.Height
                    * (GeographyBottom - GeographyTop));

        var projectedWidth =
            snapshot.MaxX - snapshot.MinX;

        var projectedHeight =
            snapshot.MaxY - snapshot.MinY;

        if (projectedWidth <= 0
            || projectedHeight <= 0)
        {
            return;
        }

        // map.png is intentionally painterly and slightly wider than a
        // geometrically exact Poland outline. Keep EPSG:2180 as the
        // authoritative geographic projection, then register that projected
        // space to the supplied artwork independently on each axis. The
        // camera itself remains uniform, so zoom/pan never distort the image.
        var scaleX =
            content.Width / projectedWidth;

        var scaleY =
            content.Height / projectedHeight;

        foreach (var town in snapshot.Towns)
        {
            var x =
                content.X
                + (town.ProjectedX - snapshot.MinX)
                * scaleX;

            var y =
                content.Y
                + (snapshot.MaxY - town.ProjectedY)
                * scaleY;

            _townWorldPositions[town.TownId] =
                new Point(
                    x,
                    y);
        }
    }

    private Point WorldToScreen(
        Point world)
    {
        var scale =
            ActualScale;

        return new Point(
            Bounds.Width / 2.0
                + (world.X - _centerX) * scale,
            Bounds.Height / 2.0
                + (world.Y - _centerY) * scale);
    }

    private Point ScreenToWorld(
        Point screen)
    {
        var scale =
            Math.Max(
                0.000001,
                ActualScale);

        return new Point(
            _centerX
                + (screen.X - Bounds.Width / 2.0)
                / scale,
            _centerY
                + (screen.Y - Bounds.Height / 2.0)
                / scale);
    }

    private void ClampCamera()
    {
        var world =
            WorldSize;

        var scale =
            Math.Max(
                0.000001,
                ActualScale);

        var visibleHalfWidth =
            Bounds.Width
            / (2.0 * scale);

        var visibleHalfHeight =
            Bounds.Height
            / (2.0 * scale);

        _centerX =
            ClampCenterAxis(
                _centerX,
                world.Width,
                visibleHalfWidth);

        _centerY =
            ClampCenterAxis(
                _centerY,
                world.Height,
                visibleHalfHeight);
    }

    private static double ClampCenterAxis(
        double center,
        double worldSize,
        double visibleHalf)
    {
        if (visibleHalf * 2.0
            >= worldSize)
        {
            return worldSize / 2.0;
        }

        return Math.Clamp(
            center,
            visibleHalf,
            worldSize - visibleHalf);
    }

    private string? HitTestTown(
        Point screen)
    {
        var snapshot =
            _snapshot;

        if (snapshot is null)
            return null;

        string? bestTownId =
            null;

        var bestDistanceSquared =
            10.0 * 10.0;

        foreach (var item in snapshot.Towns)
        {
            if (!_townWorldPositions.TryGetValue(
                    item.TownId,
                    out var world))
            {
                continue;
            }

            var point =
                WorldToScreen(world);

            if (!IsNearViewport(point, 12))
                continue;

            var dx =
                screen.X - point.X;

            var dy =
                screen.Y - point.Y;

            var distanceSquared =
                dx * dx + dy * dy;

            if (distanceSquared
                > bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared =
                distanceSquared;

            bestTownId =
                item.TownId;
        }

        return bestTownId;
    }

    private void OnPointerWheelChanged(
        object? sender,
        PointerWheelEventArgs e)
    {
        var pointer =
            e.GetPosition(this);

        var worldUnderPointer =
            ScreenToWorld(pointer);

        var factor =
            e.Delta.Y > 0
                ? ZoomStep
                : 1.0 / ZoomStep;

        var nextZoom =
            Math.Clamp(
                Zoom * factor,
                MinimumZoom,
                MaximumZoom);

        if (Math.Abs(nextZoom - Zoom)
            < 0.000001)
        {
            e.Handled = true;
            return;
        }

        Zoom =
            nextZoom;

        var scale =
            ActualScale;

        _centerX =
            worldUnderPointer.X
            - (pointer.X - Bounds.Width / 2.0)
            / scale;

        _centerY =
            worldUnderPointer.Y
            - (pointer.Y - Bounds.Height / 2.0)
            / scale;

        ClampCamera();
        InvalidateVisual();

        e.Handled =
            true;
    }

    private void OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        var point =
            e.GetCurrentPoint(this);

        if (!point.Properties.IsLeftButtonPressed)
            return;

        _lastPointer =
            point.Position;

        var hit =
            HitTestTown(
                point.Position);

        if (hit is not null)
        {
            SelectTown(hit);
            e.Handled = true;
            return;
        }

        _panning =
            true;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(
        object? sender,
        PointerEventArgs e)
    {
        var position =
            e.GetPosition(this);

        _hoverPointer =
            position;

        if (_panning)
        {
            var delta =
                position - _lastPointer;

            var scale =
                Math.Max(
                    0.000001,
                    ActualScale);

            _centerX -=
                delta.X / scale;

            _centerY -=
                delta.Y / scale;

            ClampCamera();

            _lastPointer =
                position;

            _hoveredTownId =
                null;

            InvalidateVisual();
            return;
        }

        var hovered =
            HitTestTown(position);

        if (string.Equals(
                hovered,
                _hoveredTownId,
                StringComparison.OrdinalIgnoreCase))
        {
            if (hovered is not null)
            {
                InvalidateVisual();
            }

            return;
        }

        _hoveredTownId =
            hovered;

        InvalidateVisual();
    }

    private void OnPointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        _panning =
            false;

        e.Pointer.Capture(null);

        _hoveredTownId =
            HitTestTown(
                e.GetPosition(this));

        InvalidateVisual();
    }

    private void OnPointerExited(
        object? sender,
        PointerEventArgs e)
    {
        if (_panning)
            return;

        _hoveredTownId =
            null;

        InvalidateVisual();
    }

    private void OnPointerCaptureLost(
        object? sender,
        PointerCaptureLostEventArgs e)
    {
        _panning =
            false;
    }

    private static FormattedText CreateFormattedText(
        string text,
        double size,
        FontWeight weight,
        IBrush brush)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                FontFamily.Default,
                FontStyle.Normal,
                weight),
            size,
            brush);
    }

    private static Bitmap? LoadBitmap(
        string uriText)
    {
        var uri =
            new Uri(
                uriText,
                UriKind.Absolute);

        if (!AssetLoader.Exists(uri))
            return null;

        using var stream =
            AssetLoader.Open(uri);

        return new Bitmap(stream);
    }
}
