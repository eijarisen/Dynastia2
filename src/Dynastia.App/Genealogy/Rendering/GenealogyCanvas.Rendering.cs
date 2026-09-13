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
            GetCoverImageRect(
                bitmap,
                BackgroundZoom));
    }

    private void DrawParallaxImage(
        DrawingContext context,
        Bitmap? bitmap,
        double factor)
    {
        if (bitmap is null)
            return;

        var baseRect =
            GetCoverImageRect(
                bitmap,
                ParallaxZoom);

        var desiredX =
            _parallaxTravel.X
            * factor;

        var desiredY =
            _parallaxTravel.Y
            * factor;

        // Keep the parallax layer covering the complete viewport at all
        // times. Unlike the old "show the full image" clamp, this uses the
        // overscanned crop as safe travel room. Both X and Y therefore move,
        // but never far enough to expose empty space beyond an image edge.
        var minimumX =
            Bounds.Right
            - baseRect.Right;

        var maximumX =
            Bounds.Left
            - baseRect.Left;

        var minimumY =
            Bounds.Bottom
            - baseRect.Bottom;

        var maximumY =
            Bounds.Top
            - baseRect.Top;

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

    private Rect GetCoverImageRect(
        Bitmap bitmap,
        double zoom)
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

        var coverScale =
            Math.Max(
                Bounds.Width
                    / imageSize.Width,
                Bounds.Height
                    / imageSize.Height);

        var scale =
            coverScale
            * Math.Max(
                1,
                zoom);

        var width =
            imageSize.Width
            * scale;

        var height =
            imageSize.Height
            * scale;

        return new Rect(
            Bounds.X
                + (
                    Bounds.Width
                    - width
                ) / 2,
            Bounds.Y
                + (
                    Bounds.Height
                    - height
                ) / 2,
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

}
