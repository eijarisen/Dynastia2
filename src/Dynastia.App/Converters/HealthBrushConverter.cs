using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Dynastia.App.Converters;

public sealed class HealthBrushConverter :
    IValueConverter
{
    private static readonly IBrush Critical =
        new SolidColorBrush(
            Color.FromRgb(
                198,
                40,
                40));

    private static readonly IBrush Poor =
        new SolidColorBrush(
            Color.FromRgb(
                230,
                81,
                0));

    private static readonly IBrush Fair =
        new SolidColorBrush(
            Color.FromRgb(
                249,
                168,
                37));

    private static readonly IBrush Good =
        new SolidColorBrush(
            Color.FromRgb(
                192,
                202,
                51));

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
        var health =
            value switch
            {
                double number =>
                    number,

                float number =>
                    number,

                int number =>
                    number,

                _ =>
                    0
            };

        return health switch
        {
            <= 20 =>
                Critical,

            <= 40 =>
                Poor,

            <= 60 =>
                Fair,

            <= 80 =>
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
