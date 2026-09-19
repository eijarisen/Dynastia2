using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Dynastia.App.Controls;

public sealed class TreeParallaxBackground : Control
{
    private const double BackgroundZoom = 1.08;
    private const double ParallaxZoom = 1.18;

    private readonly Bitmap? _background = LoadBitmap(
        "avares://Dynastia.App/Assets/Genealogy/tree_bg.png");
    private readonly Bitmap? _parallax1 = LoadBitmap(
        "avares://Dynastia.App/Assets/Genealogy/tree_parallax_1.png");
    private readonly Bitmap? _parallax2 = LoadBitmap(
        "avares://Dynastia.App/Assets/Genealogy/tree_parallax_2.png");
    private readonly Bitmap? _parallax3 = LoadBitmap(
        "avares://Dynastia.App/Assets/Genealogy/tree_parallax_3.png");

    private readonly DispatcherTimer _animationTimer;
    private Vector _travel;
    private Vector _targetTravel;

    public TreeParallaxBackground()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(2, 8, 9)), Bounds);
        DrawFixed(context, _background);
        DrawParallax(context, _parallax1, 0.0075);
        DrawParallax(context, _parallax2, 0.05);
        DrawParallax(context, _parallax3, 0.10);
    }

    public void SetPointerPosition(Point point)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var center = new Point(
            Bounds.Width / 2,
            Bounds.Height / 2);

        _targetTravel = new Vector(
            (point.X - center.X) * 0.9,
            (point.Y - center.Y) * 0.9);
    }

    public void ResetPointer()
    {
        _targetTravel = default;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        const double easing = 0.10;

        var delta = _targetTravel - _travel;
        if (Math.Abs(delta.X) < 0.05
            && Math.Abs(delta.Y) < 0.05)
        {
            if (Math.Abs(delta.X) > 0.0001
                || Math.Abs(delta.Y) > 0.0001)
            {
                _travel = _targetTravel;
                InvalidateVisual();
            }

            return;
        }

        _travel = new Vector(
            _travel.X + delta.X * easing,
            _travel.Y + delta.Y * easing);

        InvalidateVisual();
    }

    private void DrawFixed(DrawingContext context, Bitmap? bitmap)
    {
        if (bitmap is null)
            return;

        context.DrawImage(bitmap, GetCoverRect(bitmap, BackgroundZoom));
    }

    private void DrawParallax(
        DrawingContext context,
        Bitmap? bitmap,
        double factor)
    {
        if (bitmap is null)
            return;

        var baseRect = GetCoverRect(bitmap, ParallaxZoom);
        var minimumX = Bounds.Right - baseRect.Right;
        var maximumX = Bounds.Left - baseRect.Left;
        var minimumY = Bounds.Bottom - baseRect.Bottom;
        var maximumY = Bounds.Top - baseRect.Top;

        var offsetX = Math.Clamp(
            _travel.X * factor,
            minimumX,
            maximumX);
        var offsetY = Math.Clamp(
            _travel.Y * factor,
            minimumY,
            maximumY);

        context.DrawImage(
            bitmap,
            new Rect(
                baseRect.X + offsetX,
                baseRect.Y + offsetY,
                baseRect.Width,
                baseRect.Height));
    }

    private Rect GetCoverRect(Bitmap bitmap, double zoom)
    {
        if (bitmap.Size.Width <= 0
            || bitmap.Size.Height <= 0
            || Bounds.Width <= 0
            || Bounds.Height <= 0)
        {
            return Bounds;
        }

        var coverScale = Math.Max(
            Bounds.Width / bitmap.Size.Width,
            Bounds.Height / bitmap.Size.Height);
        var scale = coverScale * Math.Max(1, zoom);
        var width = bitmap.Size.Width * scale;
        var height = bitmap.Size.Height * scale;

        return new Rect(
            Bounds.X + (Bounds.Width - width) / 2,
            Bounds.Y + (Bounds.Height - height) / 2,
            width,
            height);
    }

    private static Bitmap? LoadBitmap(string uriText)
    {
        var uri = new Uri(uriText, UriKind.Absolute);
        if (!AssetLoader.Exists(uri))
            return null;

        using var stream = AssetLoader.Open(uri);
        return new Bitmap(stream);
    }
}
