using Avalonia.Media;

namespace Dynastia.App.ViewModels;

public static class ChancePresentation
{
    private static readonly IBrush StrongBrush =
        new SolidColorBrush(Color.Parse("#2F6F3E"));
    private static readonly IBrush GoodBrush =
        new SolidColorBrush(Color.Parse("#6E7622"));
    private static readonly IBrush ModerateBrush =
        new SolidColorBrush(Color.Parse("#A26719"));
    private static readonly IBrush LowBrush =
        new SolidColorBrush(Color.Parse("#A13A2B"));

    public static IBrush ForProbability(double probability) =>
        Math.Clamp(probability, 0, 1) switch
        {
            >= 0.70 => StrongBrush,
            >= 0.50 => GoodBrush,
            >= 0.30 => ModerateBrush,
            _ => LowBrush
        };
}
