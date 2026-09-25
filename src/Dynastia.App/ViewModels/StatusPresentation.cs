using Avalonia.Media;

namespace Dynastia.App.ViewModels;

internal static class StatusPresentation
{
    private static readonly IBrush DefaultBrush =
        new SolidColorBrush(Color.Parse("#806633"));

    private static readonly IReadOnlyDictionary<string, IBrush> Brushes =
        new Dictionary<string, IBrush>(StringComparer.OrdinalIgnoreCase)
        {
            ["Disgraced"] = new SolidColorBrush(Color.Parse("#A33636")),
            ["Bad"] = new SolidColorBrush(Color.Parse("#B95736")),
            ["Questionable"] = new SolidColorBrush(Color.Parse("#B57A33")),
            ["Neutral"] = DefaultBrush,
            ["Good"] = new SolidColorBrush(Color.Parse("#5F7C3B")),
            ["Respected"] = new SolidColorBrush(Color.Parse("#3E713A")),
            ["Esteemed"] = new SolidColorBrush(Color.Parse("#2F6938")),
            ["Obscure"] = new SolidColorBrush(Color.Parse("#7A7062")),
            ["Known"] = new SolidColorBrush(Color.Parse("#8A7041")),
            ["Established"] = DefaultBrush,
            ["Prominent"] = new SolidColorBrush(Color.Parse("#587640")),
            ["Notable"] = new SolidColorBrush(Color.Parse("#46703A")),
            ["Eminent"] = new SolidColorBrush(Color.Parse("#8A5D18"))
        };

    internal static IBrush BrushForLabel(string? label) =>
        !string.IsNullOrWhiteSpace(label)
        && Brushes.TryGetValue(label, out var brush)
            ? brush
            : DefaultBrush;
}
