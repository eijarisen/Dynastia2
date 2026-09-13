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

}
