using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Dynastia.App.Controls;

/// <summary>
/// A normal Avalonia Button whose visible content is supplied by three
/// image assets: idle, hover and pressed. It keeps ordinary Button command
/// and click behavior while avoiding theme chrome around the supplied art.
/// </summary>
public sealed class ImageStateButton : Button
{
    private readonly Image _image =
        new()
        {
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };

    private Bitmap? _idle;
    private Bitmap? _hover;
    private Bitmap? _pressed;

    public string? IdleSource { get; set; }

    public string? HoverSource { get; set; }

    public string? PressedSource { get; set; }

    public ImageStateButton()
    {
        Background =
            Brushes.Transparent;

        BorderBrush =
            Brushes.Transparent;

        BorderThickness =
            new Thickness(0);

        Padding =
            new Thickness(0);

        HorizontalContentAlignment =
            Avalonia.Layout.HorizontalAlignment.Stretch;

        VerticalContentAlignment =
            Avalonia.Layout.VerticalAlignment.Stretch;

        Content =
            _image;

        AttachedToVisualTree +=
            (_, _) =>
            {
                LoadBitmaps();

                _image.Source =
                    _idle;
            };

        DetachedFromVisualTree +=
            (_, _) =>
                DisposeBitmaps();

        PointerEntered +=
            (_, _) =>
            {
                if (IsEnabled)
                {
                    _image.Source =
                        _hover ?? _idle;
                }
            };

        PointerExited +=
            (_, _) =>
                _image.Source =
                    _idle;

        PointerPressed +=
            (_, _) =>
            {
                if (IsEnabled)
                {
                    _image.Source =
                        _pressed
                        ?? _hover
                        ?? _idle;
                }
            };

        PointerReleased +=
            (_, _) =>
            {
                if (IsEnabled)
                {
                    _image.Source =
                        _hover ?? _idle;
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

        _pressed =
            LoadBitmap(
                PressedSource);
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
        _pressed?.Dispose();

        _idle = null;
        _hover = null;
        _pressed = null;
    }
}
