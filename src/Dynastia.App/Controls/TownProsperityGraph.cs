using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Dynastia.Contracts;

namespace Dynastia.App.Controls;

public sealed class TownProsperityGraph : Control
{
    public static readonly StyledProperty<IReadOnlyList<TownProsperityHistoryPoint>?>
        HistoryProperty =
            AvaloniaProperty.Register<
                TownProsperityGraph,
                IReadOnlyList<TownProsperityHistoryPoint>?>(
                    nameof(History));

    private static readonly IBrush GridBrush =
        new SolidColorBrush(Color.FromArgb(70, 101, 70, 33));

    private static readonly IBrush BaselineBrush =
        new SolidColorBrush(Color.FromArgb(130, 122, 86, 30));

    private static readonly IBrush LineBrush =
        new SolidColorBrush(Color.FromRgb(122, 86, 30));

    private static readonly IBrush PointBrush =
        new SolidColorBrush(Color.FromRgb(74, 49, 24));

    public TownProsperityGraph()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        AffectsRender<TownProsperityGraph>(HistoryProperty);
    }

    public IReadOnlyList<TownProsperityHistoryPoint>? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        const double paddingX = 6;
        const double paddingY = 6;
        const double minimumIndex = 75;
        const double maximumIndex = 125;

        var width = Math.Max(0, Bounds.Width - paddingX * 2);
        var height = Math.Max(0, Bounds.Height - paddingY * 2);
        if (width <= 1 || height <= 1)
            return;

        var gridPen = new Pen(GridBrush, 1);
        var baselinePen = new Pen(BaselineBrush, 1.2);

        foreach (var value in new[] { 75d, 100d, 125d })
        {
            var y = ToY(value, paddingY, height, minimumIndex, maximumIndex);
            context.DrawLine(
                value == 100d ? baselinePen : gridPen,
                new Point(paddingX, y),
                new Point(paddingX + width, y));
        }

        var points = History?
            .OrderBy(point => point.Year)
            .ToArray()
            ?? Array.Empty<TownProsperityHistoryPoint>();

        if (points.Length == 0)
            return;

        var firstYear = points[0].Year;
        var lastYear = points[^1].Year;
        var yearSpan = Math.Max(1, lastYear - firstYear);
        var linePen = new Pen(LineBrush, 2.2);

        Point? previous = null;
        Point current = default;
        foreach (var point in points)
        {
            var x = paddingX
                + width * (point.Year - firstYear) / yearSpan;
            var y = ToY(
                point.Index,
                paddingY,
                height,
                minimumIndex,
                maximumIndex);

            current = new Point(x, y);
            if (previous is not null)
                context.DrawLine(linePen, previous.Value, current);

            previous = current;
        }

        context.DrawEllipse(
            PointBrush,
            null,
            current,
            3.2,
            3.2);
    }

    private static double ToY(
        double value,
        double top,
        double height,
        double minimum,
        double maximum)
    {
        var clamped = Math.Clamp(value, minimum, maximum);
        var normalized = (clamped - minimum) / (maximum - minimum);
        return top + height * (1d - normalized);
    }
}
