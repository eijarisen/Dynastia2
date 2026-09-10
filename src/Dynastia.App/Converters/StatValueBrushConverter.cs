using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Dynastia.App.Converters;

public sealed class StatValueBrushConverter :
    IValueConverter
{
    private static readonly IBrush VeryLow =
        new SolidColorBrush(
            Color.FromRgb(
                156,
                39,
                32));

    private static readonly IBrush Low =
        new SolidColorBrush(
            Color.FromRgb(
                211,
                78,
                47));

    private static readonly IBrush BelowAverage =
        new SolidColorBrush(
            Color.FromRgb(
                230,
                126,
                34));

    private static readonly IBrush Average =
        new SolidColorBrush(
            Color.FromRgb(
                226,
                201,
                76));

    private static readonly IBrush Good =
        new SolidColorBrush(
            Color.FromRgb(
                154,
                205,
                50));

    private static readonly IBrush Excellent =
        new SolidColorBrush(
            Color.FromRgb(
                67,
                160,
                71));

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        var score =
            value switch
            {
                int integer =>
                    integer,

                _ =>
                    0
            };

        return score switch
        {
            <= 0 =>
                VeryLow,

            1 =>
                Low,

            2 =>
                BelowAverage,

            3 =>
                Average,

            4 =>
                Good,

            _ =>
                Excellent
        };
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
