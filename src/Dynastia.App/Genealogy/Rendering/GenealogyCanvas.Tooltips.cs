namespace Dynastia.StandardUI.Genealogy.Rendering;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Dynastia.App.ViewModels;
using Dynastia.StandardUI.Genealogy.Layout;

public sealed partial class GenealogyCanvas
{
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
                ResolveTooltipLineBrush(
                    line),
                width - 20);

            textY +=
                18;
        }
    }


    private static IBrush ResolveTooltipLineBrush(
        string line)
    {
        var separator =
            line.IndexOf(':');

        var label =
            separator >= 0
                ? line[(separator + 1)..].Trim()
                : string.Empty;

        if (line.StartsWith(
                "Renown:",
                StringComparison.OrdinalIgnoreCase)
            || line.StartsWith(
                "Reputation:",
                StringComparison.OrdinalIgnoreCase))
        {
            return StatusPresentation.BrushForLabel(label);
        }

        if (!line.StartsWith(
                "Career:",
                StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith(
                "Marriage:",
                StringComparison.OrdinalIgnoreCase))
        {
            return TooltipText;
        }

        return label.ToLowerInvariant() switch
        {
            "miserable" => SatisfactionMiserable,
            "unhappy" => SatisfactionUnhappy,
            "content" => SatisfactionContent,
            "satisfied" => SatisfactionSatisfied,
            "thriving" => SatisfactionThriving,
            _ => SatisfactionNeutral
        };
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

}
