using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Dynastia.App.Converters;

public sealed class SatisfactionBrushConverter :
    IValueConverter
{
    private static readonly IBrush Miserable =
        new SolidColorBrush(
            Color.FromRgb(198, 40, 40));

    private static readonly IBrush Unhappy =
        new SolidColorBrush(
            Color.FromRgb(230, 81, 0));

    private static readonly IBrush Content =
        new SolidColorBrush(
            Color.FromRgb(226, 201, 76));

    private static readonly IBrush Satisfied =
        new SolidColorBrush(
            Color.FromRgb(154, 205, 50));

    private static readonly IBrush Thriving =
        new SolidColorBrush(
            Color.FromRgb(67, 160, 71));

    private static readonly IBrush Neutral =
        new SolidColorBrush(
            Color.FromRgb(191, 174, 139));

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        var label =
            value?.ToString()?.Trim()
            ?? string.Empty;

        return label.ToLowerInvariant() switch
        {
            "miserable" => Miserable,
            "unhappy" => Unhappy,
            "content" => Content,
            "satisfied" => Satisfied,
            "happy" => Satisfied,
            "thriving" => Thriving,
            _ => Neutral
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
