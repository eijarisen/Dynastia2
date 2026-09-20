using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

public sealed class TownLifePlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var gameState = context.GetService<IGameState>()
            ?? throw new InvalidOperationException("Game state is unavailable.");
        var data = context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException("Game data service is unavailable.");
        var locations = context.GetService<ILocationService>()
            ?? throw new InvalidOperationException("Location service is unavailable.");
        var historicalTowns = context.GetService<IHistoricalTownCatalog>()
            ?? throw new InvalidOperationException("Historical town catalog is unavailable.");
        var opportunities = context.GetService<ILocalCareerOpportunityService>()
            ?? throw new InvalidOperationException("Local opportunity service is unavailable.");
        var systems = context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException("Year-system registry is unavailable.");
        var random = context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException("Game random service is unavailable.");

        var institutionCatalog = TownInstitutionCatalog.Load(data, historicalTowns);
        var institutionCareers = TownInstitutionCareerCatalog.Load(data);
        var institutions = new StandardTownInstitutionService(
            institutionCatalog,
            opportunities);
        var facilityQuality = new StandardTownFacilityQualityService(
            institutions,
            TownFacilityQualityCatalog.Load(data));
        var prosperity = new StandardTownProsperityService(
            gameState,
            TownProsperityRules.Load(data));
        var economicCatalog = TownEconomicStrengthCatalog.Load(data);
        var economicStrength = new StandardLocalEconomicStrengthService(
            opportunities,
            economicCatalog);
        var townLife = new StandardTownLifeService(
            gameState,
            locations,
            opportunities,
            institutions,
            prosperity,
            facilityQuality,
            institutionCareers);

        context.AddService<ITownInstitutionService>(institutions);
        context.AddService<ITownFacilityQualityService>(facilityQuality);
        context.AddService<ITownProsperityService>(prosperity);
        context.AddService<ILocalEconomicStrengthService>(economicStrength);
        context.AddService<ITownLifeService>(townLife);

        var prosperityYearSystem = new TownProsperityYearSystem(
            context,
            random,
            prosperity,
            economicCatalog.HistoricalEffects);

        var reconciliation = context.GetService<IStateReconciliationLifecycle>();
        reconciliation?.Register(
            "townlife.prosperity_tracking",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction,
                ReconciliationLifecycleStage.AfterQueuedAction
            ],
            _ => prosperity.TrackActiveHouseholdTowns(context),
            order: 200);

        reconciliation?.Register(
            "townlife.historical_prosperity",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad
            ],
            _ => prosperityYearSystem.ReconcileHistoricalEffects(gameState),
            order: 210);

        systems.Register(prosperityYearSystem);

        context.Log(
            $"Town / City Affairs registered: {institutionCatalog.InstitutionTypes.Count} institution types, " +
            $"{institutionCatalog.InferenceRules.Count} inference rules, {institutionCatalog.Overrides.Count} overrides and " +
            $"{economicCatalog.CareerFamilyTags.Count} career-family opportunity mappings. Batch 4 banking and healthcare effects are enabled.");
    }
}
