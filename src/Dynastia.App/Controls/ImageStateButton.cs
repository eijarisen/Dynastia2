using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Dynastia.App.Controls;

/// <summary>
/// A normal Avalonia Button whose visible content is supplied by idle/hover
/// image assets. If an asset is missing, a styled text fallback keeps the
/// control usable.
/// </summary>
public sealed class ImageStateButton : Button
{
    private readonly Grid _root =
        new();

    private readonly Border _fallbackBorder =
        new()
        {
            Background =
                new SolidColorBrush(
                    Color.Parse(
                        "#103126")),

            BorderBrush =
                new SolidColorBrush(
                    Color.Parse(
                        "#C99A39")),

            BorderThickness =
                new Thickness(1),

            CornerRadius =
                new CornerRadius(4)
        };

    private readonly TextBlock _fallbackText =
        new()
        {
            HorizontalAlignment =
                HorizontalAlignment.Center,

            VerticalAlignment =
                VerticalAlignment.Center,

            FontFamily =
                new FontFamily(
                    "Georgia"),

            FontSize =
                14,

            FontWeight =
                FontWeight.SemiBold,

            Foreground =
                new SolidColorBrush(
                    Color.Parse(
                        "#F4E5BE")),

            TextAlignment =
                TextAlignment.Center
        };

    private readonly Image _image =
        new()
        {
            Stretch =
                Stretch.Uniform,

            IsHitTestVisible =
                false
        };

    private Bitmap? _idle;
    private Bitmap? _hover;

    public string? IdleSource { get; set; }

    public string? HoverSource { get; set; }

    public string? FallbackText { get; set; }

    public ImageStateButton()
    {
        _fallbackBorder.Child =
            _fallbackText;

        _root.Children.Add(
            _fallbackBorder);

        _root.Children.Add(
            _image);

        Background =
            Brushes.Transparent;

        BorderBrush =
            Brushes.Transparent;

        BorderThickness =
            new Thickness(0);

        Padding =
            new Thickness(0);

        HorizontalContentAlignment =
            HorizontalAlignment.Stretch;

        VerticalContentAlignment =
            VerticalAlignment.Stretch;

        Content =
            _root;

        AttachedToVisualTree +=
            (_, _) =>
            {
                LoadBitmaps();
                ShowIdleState();
            };

        DetachedFromVisualTree +=
            (_, _) =>
                DisposeBitmaps();

        PointerEntered +=
            (_, _) =>
            {
                if (IsEnabled)
                {
                    ShowHoverState();
                }
            };

        PointerExited +=
            (_, _) =>
                ShowIdleState();

        PointerPressed +=
            (_, _) =>
            {
                if (IsEnabled)
                {
                    ShowHoverState();
                }
            };

        PointerReleased +=
            (_, _) =>
            {
                if (IsEnabled
                    && IsPointerOver)
                {
                    ShowHoverState();
                }
                else
                {
                    ShowIdleState();
                }
            };
    }

    private void LoadBitmaps()
    {
        DisposeBitmaps();

        _idle =
            LoadBitmap(
                IdleSource);

        _hover =
            LoadBitmap(
                HoverSource);

        _fallbackText.Text =
            FallbackText
            ?? string.Empty;
    }

    private void ShowIdleState()
    {
        // Never substitute hover artwork for a missing idle asset.
        // A missing idle image should fall back to the normal text button
        // so the control still has visibly distinct idle/hover states.
        ApplyVisual(
            _idle);
    }

    private void ShowHoverState()
    {
        ApplyVisual(
            _hover
            ?? _idle);
    }

    private void ApplyVisual(
        Bitmap? bitmap)
    {
        _image.Source =
            bitmap;

        var hasImage =
            bitmap is not null;

        _image.IsVisible =
            hasImage;

        _fallbackBorder.IsVisible =
            !hasImage;

        _fallbackBorder.Background =
            new SolidColorBrush(
                Color.Parse(
                    IsPointerOver
                        ? "#184438"
                        : "#103126"));

        _fallbackBorder.BorderBrush =
            new SolidColorBrush(
                Color.Parse(
                    IsPointerOver
                        ? "#E0B64C"
                        : "#C99A39"));
    }

    private static Bitmap? LoadBitmap(
        string? source)
    {
        if (string.IsNullOrWhiteSpace(
                source))
        {
            return null;
        }

        var uri =
            new Uri(
                source,
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

    private void DisposeBitmaps()
    {
        _image.Source =
            null;

        _idle?.Dispose();
        _hover?.Dispose();

        _idle =
            null;

        _hover =
            null;
    }
}
