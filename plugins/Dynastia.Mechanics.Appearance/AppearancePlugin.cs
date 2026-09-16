using Dynastia.Contracts;

namespace Dynastia.Mechanics.Appearance;

public sealed class AppearancePlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var family = context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var gameState = context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var service = new StandardAppearanceService(family);

        foreach (var person in gameState.People)
            service.EnsureAppearance(person);

        context.AddService<IAppearanceService>(service);

        context.Log("Emoji portrait appearance mechanics registered.");
    }
}
