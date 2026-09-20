using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class CareerPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>()
            ?? throw new InvalidOperationException("Game state is unavailable.");

        var family = context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException("Family service is unavailable.");

        var economy = context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException("Economy service is unavailable.");

        var stats = context.GetService<IStatsService>()
            ?? throw new InvalidOperationException("Stats service is unavailable.");

        var data = context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException("Game data service is unavailable.");

        var contextWeights = context.GetService<IContextWeightService>()
            ?? throw new InvalidOperationException("Context-weight service is unavailable.");

        var localOpportunities =
            context.GetService<ILocalCareerOpportunityService>()
            ?? throw new InvalidOperationException(
                "Local career opportunity service is unavailable.");

        var prosperity = context.GetService<ITownProsperityService>()
            ?? throw new InvalidOperationException("Town prosperity service is unavailable.");

        var economicStrength = context.GetService<ILocalEconomicStrengthService>()
            ?? throw new InvalidOperationException("Local economic-strength service is unavailable.");

        var institutions = context.GetService<ITownInstitutionService>()
            ?? throw new InvalidOperationException("Town institution service is unavailable.");

        var education = context.GetService<IEducationService>()
            ?? throw new InvalidOperationException("Education service is unavailable.");

        var health = context.GetService<IHealthService>()
            ?? throw new InvalidOperationException("Health service is unavailable.");

        var incomeRegistry = context.GetService<IIncomeProviderRegistry>()
            ?? throw new InvalidOperationException("Income provider registry is unavailable.");

        var healthModifiers = context.GetService<IAnnualHealthModifierRegistry>()
            ?? throw new InvalidOperationException("Health modifier registry is unavailable.");

        var stressModifiers = context.GetService<IStressModifierRegistry>()
            ?? throw new InvalidOperationException("Stress modifier registry is unavailable.");

        var random = context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException("Game random service is unavailable.");

        var events = context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException("Game event bus is unavailable.");

        var actions = context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException("Action registry is unavailable.");

        var systems = context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException("Year system registry is unavailable.");

        var educationProfiles =
            CareerEducationProfileCatalog.Load(
                data);

        var catalog =
            CareerCatalog.Load(
                data,
                educationProfiles);

        var careerContext = contextWeights.LoadCatalog(
            "Career/career_context_weights.csv",
            catalog.CareerIds);

        var retirementRules =
            RetirementRuleCatalog.Load(
                data);

        var presentation =
            HistoricalCareerPresentationCatalog.Load(
                data,
                catalog.CareerIds);

        var institutionRequirements =
            CareerInstitutionRequirementCatalog.Load(
                data,
                catalog.CareerIds);

        var career =
            new StandardCareerService(
                gameState,
                family,
                random,
                catalog,
                presentation,
                retirementRules,
                localOpportunities,
                stats,
                education,
                educationProfiles,
                careerContext,
                prosperity,
                economicStrength,
                institutions,
                institutionRequirements,
                () => context.GetService<ICraftService>());

        context.AddService<ICareerService>(career);
        context.AddService<ICareerPresentationService>(career);

        var reconciliation = context.GetService<IStateReconciliationLifecycle>()
            ?? throw new InvalidOperationException(
                "State reconciliation lifecycle is unavailable.");

        reconciliation.Register(
            "career.components",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction,
                ReconciliationLifecycleStage.AfterQueuedAction,
                ReconciliationLifecycleStage.AfterPersonCreated
            ],
            _ => career.ReconcileAll(gameState.People),
            order: 30);

        InitializeFromEvents(
            gameState,
            career,
            family,
            education,
            retirementRules,
            random,
            events);

        incomeRegistry.Register(
            new CareerIncomeProvider(career));

        healthModifiers.Register(
            new CareerHealthModifierProvider(career));

        stressModifiers.Register(
            new CareerStressModifierProvider(career, events));

        RegisterActions(
            actions,
            career,
            stats,
            random,
            family,
            events);

        RegisterFamilySupportActions(
            actions,
            career,
            stats,
            health,
            random,
            family,
            economy,
            events);

        systems.Register(
            new CareerExperienceYearSystem(
                career));

        systems.Register(
            new CareerLifetimeEarningsYearSystem(
                career));

        systems.Register(
            new CareerRetirementYearSystem(
                career,
                family,
                retirementRules,
                events));

        systems.Register(
            new CareerAdvancementYearSystem(
                career,
                education,
                stats,
                random,
                family,
                retirementRules,
                events));

        systems.Register(
            new CareerJobLossYearSystem(
                career,
                random,
                family,
                events));

        context.Log($"Career mechanics registered with {institutionRequirements.Count} institution-gated careers.");
    }

}
