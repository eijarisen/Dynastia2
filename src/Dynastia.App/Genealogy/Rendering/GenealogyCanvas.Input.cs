namespace Dynastia.StandardUI.Genealogy.Rendering;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Dynastia.StandardUI.Genealogy.Layout;

public sealed partial class GenealogyCanvas
{
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

        var hit =
            HitTestPerson(
                point.Position);

        if (point.Properties
                .IsRightButtonPressed)
        {
            if (hit is Guid rightClickedPersonId)
            {
                PersonRightClicked?.Invoke(
                    rightClickedPersonId);

                e.Handled =
                    true;
            }

            return;
        }

        if (!point.Properties
            .IsLeftButtonPressed)
        {
            return;
        }

        if (hit is Guid personId)
        {
            if (e.ClickCount >= 2)
            {
                PersonDoubleClicked?.Invoke(
                    personId);
            }
            else
            {
                PersonClicked?.Invoke(
                    personId);
            }

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
