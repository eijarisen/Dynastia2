using System.Collections;
using System.Reflection;
using Dynastia.App.ViewModels;
using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Plugins;
using Dynastia.Mechanics.Career;
using Dynastia.Mechanics.Community;
using Dynastia.Mechanics.Crafts;
using Dynastia.Mechanics.Education;
using Dynastia.Mechanics.FamilyRelations;
using Dynastia.Mechanics.Households;
using Dynastia.Mechanics.Loans;
using Dynastia.Mechanics.StatImprovements;
using Dynastia.Mechanics.TurnActions;

namespace Dynastia.App.Tests;

public sealed class ProductionActionPresentationTests
{
    [Fact]
    public void ProductionFactoriesMatchLegacyCategoriesIconsVisibilityAndOrdering()
    {
        using var f = new ActionPanelFixture();
        var dependencies = Dependencies(f);
        Invoke(typeof(CareerPlugin), "RegisterActions", dependencies);
        Invoke(typeof(CareerPlugin), "RegisterFamilySupportActions", dependencies);
        foreach (var method in new[] { "CreateEducationAction", "CreatePrivateTutorAction", "CreateHelpLearningAction" })
            f.Actions.Register(Assert.IsType<GameActionDefinition>(Invoke(typeof(EducationPlugin), method, dependencies)));
        Invoke(RelationType("FamilyRelationActions"), "Register", dependencies);
        Invoke(RelationType("LegacyFamilyRelationActions"), "Register", dependencies);
        Invoke(typeof(CommunityPlugin).Assembly.GetType("Dynastia.Mechanics.Community.CommunityConnectionActions", true)!,
            "Register", dependencies);
        var context = new GamePluginContext();
        context.AddService<IActionRegistry>(f.Actions);
        new TurnActionsPlugin().Initialize(context);

        var definitions = f.Actions.GetCandidateActions(f.Head, f.Head);
        Assert.Equal(35, definitions.Count); // 8 career + 3 education + 10 relations + 6 legacy + 7 connections + Pass.
        AssertParity(definitions);
        Assert.True(definitions.Single(action => action.Id == "education.private_tutor")
            .Presentation.ShowInPrimaryActionList);
        Assert.True(definitions.Single(action => action.Id == "education.help_learning")
            .Presentation.ShowInPrimaryActionList);
        Assert.Equal(0, f.Random.ConsumedCount);
    }

    [Fact]
    public void EveryCatalogCraftAndOwnedTownMoveReceivesPresentation()
    {
        using var f = new ActionPanelFixture();
        var dependencies = Dependencies(f);
        var catalog = CraftCatalog.Load(new JsonGameDataService(RepositoryFiles.Path("data")));
        dependencies["catalog"] = catalog;
        Invoke(typeof(CraftsPlugin), "RegisterActions", dependencies);
        Invoke(typeof(HouseholdsPlugin), "RegisterPropertyActions", dependencies);
        var elsewhere = f.Economy.Residence with { Id = "elsewhere", Town = "Elsewhere" };
        f.Economy.Houses.Add(new HousePropertyInfo(Guid.Parse("10000000-0000-0000-0000-000000000001"), elsewhere, false, true));
        var definitions = f.Actions.GetCandidateActions(f.Head, f.Head);
        Assert.Equal(catalog.All.Count, definitions.Count(action => action.Id.StartsWith("craft.start.")));
        Assert.Equal(catalog.All.Count, definitions.Count(action => action.Id.StartsWith("craft.teach.")));
        Assert.Contains(definitions, action => action.Id == "household.move.elsewhere");
        AssertParity(definitions);
        f.Economy.Houses.Clear();
        Assert.DoesNotContain(f.Actions.GetCandidateActions(f.Head, f.Head), action => action.Id.StartsWith("household.move."));
        Assert.Equal(0, f.Random.ConsumedCount);
    }

    [Fact]
    public void HistoricalLoanAndNannyCopiesRetainMetadataAcrossYears()
    {
        using var f = new ActionPanelFixture();
        var dependencies = Dependencies(f);
        Invoke(typeof(LoansPlugin), "RegisterActions", dependencies);
        Invoke(typeof(HouseholdsPlugin), "RegisterNannyActions", dependencies);
        foreach (var year in new[] { 1700, 1900, 2026 })
        {
            f.State.Year = year;
            var definitions = f.Actions.GetCandidateActions(f.Head, f.Head);
            Assert.Equal(5, definitions.Count);
            Assert.All(definitions, action => Assert.EndsWith($"/{year}", action.Label));
            AssertParity(definitions);
        }
        Assert.Equal(0, f.Random.ConsumedCount);
    }

    [Fact]
    public void DataDrivenStatFactoriesRetainExactIconsAndTreatmentOrder()
    {
        using var f = new ActionPanelFixture();
        var dependencies = Dependencies(f);
        var rulesType = typeof(StatImprovementsPlugin).Assembly.GetType(
            "Dynastia.Mechanics.StatImprovements.StatImprovementRules", true)!;
        var rows = Assert.IsAssignableFrom<IEnumerable>(Invoke(rulesType, "Load", dependencies));
        foreach (var row in rows)
        {
            dependencies["definition"] = row;
            var definition = Assert.IsType<GameActionDefinition>(Invoke(typeof(StatImprovementsPlugin), "CreateAction", dependencies));
            Assert.NotNull(definition.DisplayCost);
            f.Actions.Register(definition);
        }
        var definitions = f.Actions.GetCandidateActions(f.Head, f.Head);
        Assert.Equal(6, definitions.Count);
        AssertParity(definitions);
        Assert.Equal(new[] { "stats.improve_strength", "stats.improve_intellect", "stats.improve_immunity",
            "stats.improve_appeal", "stats.improve_longevity", "stats.improve_fertility" },
            ActionPresentationPolicy.Order(definitions).Select(action => action.Id));
        Assert.All(definitions, definition => Assert.False(definition.Presentation.ShowInPrimaryActionList));
        Assert.Equal(0, f.Random.ConsumedCount);
    }

    private static void AssertParity(IReadOnlyList<GameActionDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            Assert.NotSame(ActionPresentationMetadata.Empty, definition.Presentation);
            Assert.False(string.IsNullOrWhiteSpace(definition.Presentation.Emoji), definition.Id);
            Assert.Equal(ActionPresentationPolicy.GetCategories(definition.Id).OrderBy(category => category),
                ActionPresentationPolicy.GetCategories(definition).OrderBy(category => category));
            Assert.Equal(definition.Presentation.Emoji, ActionEmojiMap.GetEmoji(definition));
            var legacy = ActionPresentationPolicy.Resolve(Legacy(definition));
            Assert.Equal(legacy.AdjacencyGroup, definition.Presentation.AdjacencyGroup);
            Assert.Equal(legacy.GroupOrder, definition.Presentation.GroupOrder);
            Assert.Equal(legacy.PlacementAnchor, definition.Presentation.PlacementAnchor);
            Assert.Equal(legacy.PlaceAfterAnchor, definition.Presentation.PlaceAfterAnchor);
            Assert.Equal(legacy.PlaceLast, definition.Presentation.PlaceLast);
            var expectedPrimaryVisibility =
                definition.Id.StartsWith("stats.improve_", StringComparison.OrdinalIgnoreCase)
                    ? false
                    : legacy.ShowInPrimaryActionList;
            Assert.Equal(expectedPrimaryVisibility, definition.Presentation.ShowInPrimaryActionList);
        }

        // Compare rotations, reversal and incomplete groups without drawing from game RNG.
        for (var offset = 0; offset < definitions.Count; offset++)
        {
            var input = definitions.Skip(offset).Concat(definitions.Take(offset))
                .Where((_, index) => (index + offset) % 4 != 0).Reverse().ToArray();
            Assert.Equal(ActionPresentationPolicy.Order(input.Select(Legacy).ToArray()).Select(action => action.Id),
                ActionPresentationPolicy.Order(input).Select(action => action.Id));
        }
    }

    private static GameActionDefinition Legacy(GameActionDefinition definition) => new()
    {
        Id = definition.Id, Label = definition.Label, Description = definition.Description,
        Mode = definition.Mode, QueuePhase = definition.QueuePhase, DisplayCost = definition.DisplayCost,
        BypassGuards = definition.BypassGuards, IsAvailable = definition.IsAvailable,
        EvaluateAvailability = definition.EvaluateAvailability, Execute = definition.Execute
    };

    private static Type RelationType(string name) => typeof(FamilyRelationsPlugin).Assembly
        .GetType($"Dynastia.Mechanics.FamilyRelations.{name}", true)!;

    private static Dictionary<string, object?> Dependencies(ActionPanelFixture f) => new()
    {
        ["actions"] = f.Actions, ["gameState"] = f.State, ["random"] = f.Random,
        ["economy"] = f.Economy, ["locations"] = f.Locations, ["events"] = f.Events,
        ["historical"] = new HistoricalLabels(), ["economyBalance"] = new EconomyBalance(),
        ["facilities"] = new MedicalFacilities(), ["presentationTarget"] = f.Head,
        ["data"] = new JsonGameDataService(RepositoryFiles.Path("data"))
    };

    // Invoke only the production registration/factory boundary, never eligibility or
    // execution delegates. Unused mechanics dependencies remain null so accidental
    // eager simulation work fails the test instead of being hidden by loose mocks.
    private static object? Invoke(Type owner, string name, IReadOnlyDictionary<string, object?> dependencies)
    {
        var method = owner.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(null, method.GetParameters()
            .Select(parameter => dependencies.TryGetValue(parameter.Name!, out var value) ? value : null).ToArray());
    }

    private sealed class HistoricalLabels : IHistoricalActionVariantService
    {
        public HistoricalActionVariant GetVariant(string actionId, int year) =>
            new(actionId, 0, null, $"{actionId}/{year}", "Historical description", "Historical narrative");
        public HistoricalActionVariant GetCanonicalVariant(string actionId) => GetVariant(actionId, 0);
    }

    private sealed class EconomyBalance : IEconomyBalanceService
    {
        public decimal OrdinaryLivingCostUnit => 1000m;
        public decimal NannyAnnualCost => 1000m;
    }

    private sealed class MedicalFacilities : ITownFacilityQualityService
    {
        public MedicalQualityInfo GetMedicalQuality(TownInfo town, int year) => new(5, "Fixture", 0, 1m);
        public BankOfferQualityInfo GetBankQuality(TownInfo town, int year) => throw new NotSupportedException();
    }
}
