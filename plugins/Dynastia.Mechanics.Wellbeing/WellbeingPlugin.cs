using Dynastia.Contracts;

namespace Dynastia.Mechanics.Wellbeing;

public sealed partial class WellbeingPlugin : IGamePlugin
{
    private const decimal TherapyCost =
        1500m;

    private const decimal HealCost =
        2000m;

    private const double DrinkHealthPenalty =
        10;

    private const double AlcoholismChanceFromDrinking =
        0.20;


    public void Initialize(
        IGamePluginContext context)
    {
        var gameState =
            context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var health =
            context.GetService<IHealthService>()
            ?? throw new InvalidOperationException(
                "Health service is unavailable.");

        var career =
            context.GetService<ICareerService>()
            ?? throw new InvalidOperationException(
                "Career service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Random service is unavailable.");

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var historical =
            context.GetService<IHistoricalActionVariantService>()
            ?? throw new InvalidOperationException(
                "Historical action variant service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var healthModifiers =
            context.GetService<IAnnualHealthModifierRegistry>()
            ?? throw new InvalidOperationException(
                "Annual health modifier registry is unavailable.");

        var recoveryActivities =
            RecoveryActivityCatalog.Load(
                data);

        var healthcareEras =
            HealthcareEraCatalog.Load(
                data);

        healthModifiers.Register(
            new WellbeingHealthModifierProvider());

        RegisterRecover(
            actions,
            family,
            career,
            random,
            events,
            recoveryActivities);

        RegisterDrink(
            actions,
            family,
            career,
            health,
            random,
            events);

        RegisterTherapy(
            actions,
            family,
            health,
            economy,
            stats,
            random,
            events,
            historical);

        RegisterHeal(
            actions,
            family,
            health,
            economy,
            events,
            gameState,
            historical,
            healthcareEras);

        systems.Register(
            new WellbeingCleanupYearSystem());

        context.Log(
            "Wellbeing mechanics registered.");
    }

}
