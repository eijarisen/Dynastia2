using Dynastia.Contracts;

namespace Dynastia.Mechanics.Adoption;

public sealed class AdoptionPlugin :
    IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
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

        var healthModifiers =
            context.GetService<IAnnualHealthModifierRegistry>()
            ?? throw new InvalidOperationException(
                "Annual health modifier registry is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Random service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var adoption =
            new StandardAdoptionService(
                gameState,
                family,
                economy);

        context.AddService<IAdoptionService>(
            adoption);

        healthModifiers.Register(
            new OrphanHealthModifierProvider());

        systems.Register(
            new AdoptionYearSystem(
                adoption,
                family,
                economy,
                random,
                events));

        context.Log(
            "Adoption and orphan-care mechanics registered.");
    }
}
