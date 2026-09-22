using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.RareEvents;

namespace Dynastia.Core.Tests;

public sealed class ContentReworkBatch4RareEventsTests
{
    [Fact]
    public void RareEventCatalogUsesThirtyStableRowsAndSeparatePools()
    {
        var catalog = RareEventCatalog.Load(CreateRepositoryData());
        Assert.Equal(28, catalog.Events.Count);
        Assert.Equal(8, catalog.GetPool("Household").Count);
        Assert.Equal(19, catalog.GetPool("Personal").Count);
        Assert.Single(catalog.GetPool("Special"));
        Assert.Equal("rare.suicide", catalog.GetPool("Special")[0].EventId);
    }

    [Fact]
    public void OrdinaryGateFrequencyIsIndependentOfCatalogSize()
    {
        var rules = RareEventPoolRulesCatalog.Load(CreateRepositoryData());
        Assert.Equal(0.0013, rules.GetGateChance("Household"), 10);
        Assert.Equal(0.00095, rules.GetGateChance("Personal"), 10);
        Assert.Equal(0.0, rules.GetGateChance("Special"), 10);
    }

    [Fact]
    public void HistoricalAvailabilityAndAgeLimitsMatchBatchTargets()
    {
        var catalog = RareEventCatalog.Load(CreateRepositoryData());
        var traffic = catalog.Find("rare.traffic_accident")!;
        var lottery = catalog.Find("rare.lottery_win")!;
        var scholarship = catalog.Find("rare.scholarship")!;
        var prize = catalog.Find("rare.prize_award")!;

        Assert.Equal(1700, traffic.StartYear);
        Assert.Equal(1957, lottery.StartYear);
        Assert.Equal(1800, scholarship.StartYear);
        Assert.Equal(29, scholarship.MaximumAge);
        Assert.False(scholarship.IsAvailable(1850, 30));
        Assert.Equal(1850, prize.StartYear);
    }

    [Fact]
    public void FarmingAndCraftEventsUseCapabilityRequirements()
    {
        var catalog = RareEventCatalog.Load(CreateRepositoryData());
        Assert.Null(catalog.Find("rare.exceptional_harvest"));
        Assert.Null(catalog.Find("rare.crop_failure"));
        Assert.NotNull(catalog.Find("rare.local_epidemic"));
        Assert.True(catalog.Find("rare.craft_setback")!.RequiresCraft);
        Assert.True(catalog.Find("rare.craft_commission")!.RequiresCraft);
    }

    [Fact]
    public void SimpleEffectDataIncludesBoundedAccidentDeathRisk()
    {
        var data = CreateRepositoryData();
        var catalog = RareEventCatalog.Load(data);
        var effects = RareEventSimpleEffectCatalog.Load(data, catalog);

        var water = effects.GetEffects("rare.water_accident");
        Assert.Contains(water, effect => effect.EffectType == "HealthDamage" && effect.MinimumValue == 20 && effect.MaximumValue == 60);
        Assert.Contains(water, effect => effect.EffectType == "DeathChance" && effect.MinimumValue == 0.03);

        var animal = effects.GetEffects("rare.animal_accident");
        Assert.Contains(animal, effect => effect.EffectType == "DeathChance" && effect.MinimumValue == 0.01);
    }

    [Fact]
    public void StatDirectionIsMildAndNeverDeterministic()
    {
        Assert.Equal(0.8, RareEventRules.GetStatMultiplier("High", 1), 10);
        Assert.Equal(1.2, RareEventRules.GetStatMultiplier("High", 5), 10);
        Assert.Equal(1.2, RareEventRules.GetStatMultiplier("Low", 1), 10);
        Assert.Equal(0.8, RareEventRules.GetStatMultiplier("Low", 5), 10);
    }

    [Fact]
    public void ContextWeightsFavorConfiguredAgeAndSettlementPatterns()
    {
        var data = CreateRepositoryData();
        var catalog = RareEventCatalog.Load(data);
        var context = new ContextWeightService(data)
            .LoadCatalog("RareEvents/rare_event_context_weights.csv", catalog.Events.Select(item => item.EventId));

        var youngCity = new ContextWeightContext(1900, 25, Sex.Male, "Choleric", "Neutral", SettlementClass.City);
        var elderlyCity = youngCity with { Age = 75 };
        Assert.True(context.GetDimensionMultiplier("rare.assault", youngCity, "AgeBand")
                    > context.GetDimensionMultiplier("rare.assault", elderlyCity, "AgeBand"));

        var smallTown = youngCity with { SettlementClass = SettlementClass.SmallTown };
        Assert.True(context.GetDimensionMultiplier("rare.mugging", youngCity, "SettlementClass")
                    > context.GetDimensionMultiplier("rare.mugging", smallTown, "SettlementClass"));
    }

    [Fact]
    public void EpidemicPoolRespectsHistoricalConditionEndDates()
    {
        var epidemics = RareEventEpidemicCatalog.Load(CreateRepositoryData());
        var smallpox = epidemics.Entries.Single(entry => entry.ConditionId == "smallpox");
        var polio = epidemics.Entries.Single(entry => entry.ConditionId == "polio");
        Assert.True(smallpox.IsAvailable(1979));
        Assert.False(smallpox.IsAvailable(1980));
        Assert.True(polio.IsAvailable(2002));
        Assert.False(polio.IsAvailable(2003));
    }

    [Fact]
    public void HistoricalTransportVariantUsesOneStableEventId()
    {
        var data = CreateRepositoryData();
        var catalog = RareEventCatalog.Load(data);
        var variants = RareEventVariantCatalog.Load(data, catalog);
        var traffic = catalog.Find("rare.traffic_accident")!;
        Assert.Equal("Horse & Carriage Accident", variants.ResolveName(traffic, 1800));
        Assert.Equal("Road & Carriage Accident", variants.ResolveName(traffic, 1900));
        Assert.Equal("Road Traffic Accident", variants.ResolveName(traffic, 1950));
    }

    [Fact]
    public void SuicideRequiresStressAndRecognizesDrugDependenceWithinCap()
    {
        var below = RareEventRules.GetSuicideChance(4.99, true, true, true, true, true, true, true, true, "Melancholic");
        Assert.Equal(0.0, below, 10);

        var withoutDrug = RareEventRules.GetSuicideChance(5, false, false, false, false, false, false, false, false, "Melancholic");
        var withDrug = RareEventRules.GetSuicideChance(5, false, false, false, true, false, false, false, false, "Melancholic");
        Assert.True(withDrug > withoutDrug);
        Assert.InRange(withDrug, 0.0, RareEventRules.MaximumSuicideChance);

        var extreme = RareEventRules.GetSuicideChance(100, true, true, true, true, true, true, true, true, "Melancholic");
        Assert.Equal(RareEventRules.MaximumSuicideChance, extreme, 10);
    }

    private static IGameDataService CreateRepositoryData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var dataPath = Path.Combine(directory.FullName, "data");
            if (File.Exists(Path.Combine(dataPath, "RareEvents", "rare_events.csv")))
                return new JsonGameDataService(dataPath);
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository data directory from test output.");
    }
}
