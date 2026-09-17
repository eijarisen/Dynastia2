using Dynastia.Contracts;

namespace Dynastia.Mechanics.Succession;

public sealed class SuccessionPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState =
            context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var selection =
            context.GetService<ISelectionService>()
            ?? throw new InvalidOperationException(
                "Selection service is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var succession =
            new StandardSuccessionService(
                gameState,
                family,
                economy,
                selection);

        context.AddService<ISuccessionService>(
            succession);

        systems.Register(
            new SuccessionYearSystem(
                succession));

        context.Log(
            "Succession mechanics registered.");
    }
}
