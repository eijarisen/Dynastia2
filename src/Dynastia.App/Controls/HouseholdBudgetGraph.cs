using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Dynastia.Contracts;

namespace Dynastia.App.Controls;

public sealed class HouseholdBudgetGraph : Control
{
    public static readonly StyledProperty<IReadOnlyList<HouseholdBudgetHistoryPoint>?>
        HistoryProperty =
            AvaloniaProperty.Register<
                HouseholdBudgetGraph,
                IReadOnlyList<HouseholdBudgetHistoryPoint>?>(
                    nameof(History));

    private static readonly IBrush GridBrush =
        new SolidColorBrush(Color.FromArgb(70, 101, 70, 33));

    private static readonly IBrush LineBrush =
        new SolidColorBrush(Color.FromRgb(227, 195, 109));

    private static readonly IBrush PointBrush =
        new SolidColorBrush(Color.FromRgb(248, 233, 200));

    public HouseholdBudgetGraph()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        AffectsRender<HouseholdBudgetGraph>(HistoryProperty);
    }

    public IReadOnlyList<HouseholdBudgetHistoryPoint>? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        const double paddingX = 5;
        const double paddingY = 5;
        var width = Math.Max(0, Bounds.Width - paddingX * 2);
        var height = Math.Max(0, Bounds.Height - paddingY * 2);
        if (width <= 1 || height <= 1)
            return;

        var points = History?
            .OrderBy(point => point.Year)
            .ToArray()
            ?? Array.Empty<HouseholdBudgetHistoryPoint>();

        if (points.Length == 0)
            return;

        var minimum = Math.Min(0m, points.Min(point => point.Wealth));
        var maximum = Math.Max(1m, points.Max(point => point.Wealth));
        if (maximum == minimum)
            maximum = minimum + 1m;

        var range = (double)(maximum - minimum);
        var gridPen = new Pen(GridBrush, 1);
        foreach (var fraction in new[] { 0.25, 0.5, 0.75 })
        {
            var y = paddingY + height * fraction;
            context.DrawLine(
                gridPen,
                new Point(paddingX, y),
                new Point(paddingX + width, y));
        }

        var firstYear = points[0].Year;
        var lastYear = points[^1].Year;
        var yearSpan = Math.Max(1, lastYear - firstYear);
        var linePen = new Pen(LineBrush, 2);

        Point? previous = null;
        Point current = default;
        foreach (var point in points)
        {
            var x = paddingX
                + width * (point.Year - firstYear) / yearSpan;
            var normalized = (double)(point.Wealth - minimum) / range;
            var y = paddingY + height * (1d - normalized);

            current = new Point(x, y);
            if (previous is not null)
                context.DrawLine(linePen, previous.Value, current);

            previous = current;
        }

        context.DrawEllipse(
            PointBrush,
            null,
            current,
            3,
            3);
    }
}
