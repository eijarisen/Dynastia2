using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Education;
using Dynastia.Mechanics.Historical;
using Dynastia.Mechanics.RareEvents;

namespace Dynastia.Core.Tests;

public sealed class HistoricalRetouchBatch2Tests
{
    private const string EducationData =
        "StartYear,EndYear,PassiveChanceMultiplier,PassiveMaxLevel,HelpedMaxLevel,FounderMinLevel,FounderMaxLevel,GeneratedAdultMinLevel,GeneratedAdultMaxLevel\n" +
        "1700,1799,0.25,2,3,0,2,0,2\n" +
        "1800,1849,0.35,2,3,0,2,0,2\n" +
        "1850,1899,0.55,3,4,1,2,0,3\n" +
        "1900,1945,0.75,4,4,1,2,1,3\n" +
        "1946,1989,0.9,5,5,1,3,1,4\n" +
        "1990,,1.0,5,5,2,4,2,5\n";

    private const string HistoricalActionData =
        "[" +
        "{\"actionId\":\"stats.improve_strength\",\"startYear\":1700,\"endYear\":null,\"label\":\"Physical Training\",\"description\":\"Strength\",\"narrative\":\"trained\"}," +
        "{\"actionId\":\"wellbeing.therapy\",\"startYear\":1900,\"endYear\":null,\"label\":\"Psychotherapy\",\"description\":\"Therapy\",\"narrative\":\"underwent psychotherapy\"}," +
        "{\"actionId\":\"stats.improve_appeal\",\"startYear\":1920,\"endYear\":null,\"label\":\"Plastic Surgery\",\"description\":\"Appeal\",\"narrative\":\"surgery\"}," +
        "{\"actionId\":\"stats.improve_fertility\",\"startYear\":1950,\"endYear\":null,\"label\":\"Fertility Treatment\",\"description\":\"Fertility\",\"narrative\":\"treatment\"}" +
        "]";

    private const string RareEventData =
        "EventId,Name,Pool,Category,StartYear,EndYear,BaseWeight,MinimumAge,MaximumAge,RequiresFinanceHousehold,MinimumHouseholdWealth,RequiresEmployment,RequiresOwnedHouse,RequiresFarmland,RequiresCraft,MinimumStress,StatId,StatDirection,TownPreference,MinimumSettlementClass,RequiredOpportunityTags,PreferredOpportunityTags,PreferredCareerFamilies,HandlerId\n" +
        "rare.traffic_accident,Road Accident,Personal,Accident,1700,,12,10,,False,0,False,False,False,False,0,-,None,Universal,SmallTown,-,-,transport,bespoke.traffic_accident\n" +
        "rare.lottery_win,Lottery Win,Personal,Fortune,1957,,2,18,,True,0,False,False,False,False,0,-,None,Universal,SmallTown,-,-,-,bespoke.lottery_win\n";

    [Fact]
    public void EarlyModernEducationUsesQuarterPassiveChanceAndLowCeilings()
    {
        var catalog = EducationEraCatalog.Load(
            new InlineDataService(new Dictionary<string, string>
            {
                ["Education/education_eras.csv"] = EducationData
            }));

        var era = catalog.GetRule(1700);

        Assert.Equal(0.25, era.PassiveChanceMultiplier, 6);
        Assert.Equal(2, EducationProgressionRules.GetPassiveChildhoodCeiling(5, era.PassiveMaxLevel));
        Assert.Equal(3, EducationProgressionRules.GetHelpedChildhoodCeiling(5, era.HelpedMaxLevel));
        Assert.Equal(0.20, EducationProgressionRules.ApplyPassiveChanceMultiplier(0.80, era), 6);
        Assert.InRange(era.GeneratedAdultMinLevel, 0, 2);
        Assert.Equal(2, era.GeneratedAdultMaxLevel);
    }

    [Fact]
    public void HistoricalActionAvailabilityUsesSuppliedStartYears()
    {
        var service = HistoricalActionVariantService.Load(
            new InlineDataService(new Dictionary<string, string>
            {
                ["Common/historical_action_variants.json"] = HistoricalActionData
            }));

        Assert.Equal(
            "Physical Training",
            service.GetVariant("stats.improve_strength", 1700)?.Label);

        Assert.Null(service.GetVariant("wellbeing.therapy", 1899));
        Assert.Equal("Psychotherapy", service.GetVariant("wellbeing.therapy", 1900)?.Label);

        Assert.Null(service.GetVariant("stats.improve_appeal", 1919));
        Assert.NotNull(service.GetVariant("stats.improve_appeal", 1920));

        Assert.Null(service.GetVariant("stats.improve_fertility", 1949));
        Assert.NotNull(service.GetVariant("stats.improve_fertility", 1950));
    }

    [Fact]
    public void RareEventsRespectHistoricalAvailability()
    {
        var catalog = RareEventAvailabilityCatalog.Load(
            new InlineDataService(new Dictionary<string, string>
            {
                ["RareEvents/rare_events.csv"] = RareEventData
            }));

        Assert.False(catalog.IsAvailable("rare.traffic_accident", 1699));
        Assert.True(catalog.IsAvailable("rare.traffic_accident", 1700));
        Assert.False(catalog.IsAvailable("rare.lottery_win", 1956));
        Assert.True(catalog.IsAvailable("rare.lottery_win", 1957));
    }

    [Fact]
    public void RestoredQueuedActionGetsOneCompatibilityPass()
    {
        var state = new GameState();
        var actor = state.CreatePerson("Jan", "Nowak", 30);
        var target = actor;
        var executed = false;

        var registry = new ActionRegistry(
            state,
            new GameEventBus(),
            new GameRandom(123),
            new ActionGuardRegistry());

        registry.Register(
            new GameActionDefinition
            {
                Id = "historical.test",
                Label = "Historical Test",
                Description = "Test",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = context =>
                    ActionCompatibilityParameters.IsRestoredQueuedAction(
                        context.Parameters),
                Execute = _ =>
                {
                    executed = true;
                    return new GameActionResult(true);
                }
            });

        registry.RestoreQueuedActions(
            [
                new QueuedActionInfo(
                    "historical.test",
                    "Historical Test",
                    YearPhase.QueuedActionsEarly,
                    actor.Id,
                    target.Id)
            ]);

        registry.ExecuteQueued(YearPhase.QueuedActionsEarly);

        Assert.True(executed);
    }

    private sealed class InlineDataService : IGameDataService
    {
        private readonly IReadOnlyDictionary<string, string> _text;

        public InlineDataService(
            IReadOnlyDictionary<string, string> text)
        {
            _text = text;
        }

        public IReadOnlyList<string> GetStringList(string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(string relativePath) =>
            throw new NotSupportedException();

        public string ReadText(string relativePath) =>
            _text.TryGetValue(relativePath, out var value)
                ? value
                : throw new FileNotFoundException(relativePath);
    }
}
