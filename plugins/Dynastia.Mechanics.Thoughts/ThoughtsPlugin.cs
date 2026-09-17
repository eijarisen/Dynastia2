using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

public sealed class ThoughtsPlugin :
    IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var gameState =
            Require<IGameState>(
                context,
                "Game state");

        var family =
            Require<IFamilyService>(
                context,
                "Family service");

        var stats =
            Require<IStatsService>(
                context,
                "Stats service");

        var health =
            Require<IHealthService>(
                context,
                "Health service");

        var career =
            Require<ICareerService>(
                context,
                "Career service");

        var crafts =
            Require<ICraftService>(
                context,
                "Craft service");

        var households =
            Require<IHouseholdService>(
                context,
                "Household service");

        var justice =
            Require<IJusticeService>(
                context,
                "Justice service");

        var education =
            Require<IEducationService>(
                context,
                "Education service");

        var economy =
            Require<IEconomyService>(
                context,
                "Economy service");

        var adoption =
            Require<IAdoptionService>(
                context,
                "Adoption service");

        var marriageSatisfaction =
            Require<IMarriageSatisfactionService>(
                context,
                "Marriage Satisfaction service");

        var events =
            Require<IGameEventBus>(
                context,
                "Event bus");

        var systems =
            Require<IYearSystemRegistry>(
                context,
                "Year-system registry");

        var providers =
            new ThoughtProviderRegistry();

        providers.Register(
            new FamilyThoughtProvider());

        providers.Register(
            new RelationshipThoughtProvider());

        providers.Register(
            new HealthThoughtProvider());

        providers.Register(
            new CareerThoughtProvider());

        providers.Register(
            new CraftThoughtProvider(crafts));

        providers.Register(
            new HouseholdThoughtProvider());

        providers.Register(
            new JusticeEducationThoughtProvider());

        providers.Register(
            new EventThoughtProvider());

        context.AddService<IThoughtProviderRegistry>(
            providers);

        var thoughts =
            new StandardThoughtService(
                gameState,
                family,
                stats,
                health,
                career,
                households,
                justice,
                education,
                economy,
                adoption,
                marriageSatisfaction,
                events,
                providers);

        context.AddService<IThoughtService>(
            thoughts);

        var reconciliation = Require<IStateReconciliationLifecycle>(
            context,
            "State reconciliation lifecycle");

        reconciliation.Register(
            "thoughts.current_state",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction
            ],
            _ => thoughts.EnsureCurrentThoughts(),
            order: 90);

        reconciliation.Register(
            "thoughts.load_state",
            [ReconciliationLifecycleStage.AfterLoad],
            _ => thoughts.ResetAfterLoad(),
            order: 90);

        systems.Register(
            new ThoughtYearSystem(
                thoughts));

        context.Log(
            "Thoughts and authoritative person-emoji mechanics registered.");
    }

    private static T Require<T>(
        IGamePluginContext context,
        string name)
        where T : class
    {
        return context.GetService<T>()
            ?? throw new InvalidOperationException(
                $"{name} is unavailable.");
    }
}
