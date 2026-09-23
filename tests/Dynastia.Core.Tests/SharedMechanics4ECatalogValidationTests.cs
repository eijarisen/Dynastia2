using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Career;
using Dynastia.Mechanics.Crafts;
using Dynastia.Mechanics.Hobbies;
using Dynastia.Mechanics.RareEvents;
using Dynastia.Mechanics.Relationships;
using Dynastia.Mechanics.Reproduction;
using Dynastia.Mechanics.Wellbeing;

namespace Dynastia.Core.Tests;

public sealed class SharedMechanics4ECatalogValidationTests
{
    [Fact]
    public void ContextWeightDiagnosticIdentifiesSourceRowFieldValueAndConstraint()
    {
        const string path = "Test/context.csv";
        const string csv =
            "ItemId,StartYear,EndYear,Dimension,Value,WeightMultiplier\n" +
            "sample,1700,,Temperament,Stormy,1.0";
        var service = new ContextWeightService(
            new InlineDataService(new Dictionary<string, string>
            {
                [path] = csv
            }));

        var error = Assert.Throws<InvalidDataException>(() =>
            service.LoadCatalog(path, ["sample"]));

        Assert.Contains(path, error.Message);
        Assert.Contains("row 2", error.Message);
        Assert.Contains("field 'Value'", error.Message);
        Assert.Contains("'Stormy'", error.Message);
        Assert.Contains("expected one of:", error.Message);
    }

    [Fact]
    public void MalformedCsvRowDiagnosticIdentifiesActualAndExpectedFieldCount()
    {
        const string csv =
            "StartYear,EndYear,MaleRetirementAge,FemaleRetirementAge,PensionRate\n" +
            "1700,,65,60";

        var error = Assert.Throws<InvalidDataException>(() =>
            RetirementRuleCatalog.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["Career/retirement_rules.csv"] = csv
                })));

        Assert.Contains("Career/retirement_rules.csv", error.Message);
        Assert.Contains("row 2", error.Message);
        Assert.Contains("field 'FieldCount'", error.Message);
        Assert.Contains("'4'", error.Message);
        Assert.Contains("expected exactly 5 fields", error.Message);
    }

    [Fact]
    public void MissingReferenceDiagnosticIdentifiesUnknownBirthCondition()
    {
        const string csv =
            "ConditionId,StartYear,EndYear,MotherAgeBand,WeightMultiplier\n" +
            "unknown_condition,1700,,Adult,1.0";

        var error = Assert.Throws<InvalidDataException>(() =>
            BirthConditionContextCatalog.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["Health/birth_condition_context_weights.csv"] = csv
                }),
                ["known_condition"]));

        Assert.Contains("Health/birth_condition_context_weights.csv", error.Message);
        Assert.Contains("row 2", error.Message);
        Assert.Contains("field 'ConditionId'", error.Message);
        Assert.Contains("'unknown_condition'", error.Message);
        Assert.Contains("expected a ConditionId present in Common/birth_conditions.json", error.Message);
    }

    [Fact]
    public void MalformedIdentifierDiagnosticIdentifiesBlankRareEventPoolId()
    {
        const string csv =
            "Pool,AnnualGateChance,Notes\n" +
            ",0.01,Missing pool ID";

        var error = Assert.Throws<InvalidDataException>(() =>
            RareEventPoolRulesCatalog.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["RareEvents/rare_event_pool_rules.csv"] = csv
                })));

        Assert.Contains("RareEvents/rare_event_pool_rules.csv", error.Message);
        Assert.Contains("row 2", error.Message);
        Assert.Contains("field 'Pool'", error.Message);
        Assert.Contains("<empty>", error.Message);
        Assert.Contains("expected a non-empty pool ID", error.Message);
    }

    [Fact]
    public void RetirementDiagnosticIdentifiesInvalidPensionRate()
    {
        const string csv =
            "StartYear,EndYear,MaleRetirementAge,FemaleRetirementAge,PensionRate\n" +
            "1700,,65,60,1.25";

        var error = Assert.Throws<InvalidDataException>(() =>
            RetirementRuleCatalog.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["Career/retirement_rules.csv"] = csv
                })));

        Assert.Contains("Career/retirement_rules.csv", error.Message);
        Assert.Contains("row 2", error.Message);
        Assert.Contains("field 'PensionRate'", error.Message);
        Assert.Contains("'1.25'", error.Message);
        Assert.Contains("expected a number from 0 through 1", error.Message);
    }

    [Fact]
    public void EraGapDiagnosticIdentifiesOffendingStartYearAndExpectedNextYear()
    {
        const string csv =
            "StartYear,EndYear,MarriageChanceMultiplier,ArrangedMarriageMultiplier,AutomaticDivorceMultiplier\n" +
            "1700,1799,1,1,1\n" +
            "1801,,1,1,1";

        var error = Assert.Throws<InvalidDataException>(() =>
            StandardRelationshipEraService.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["Relationships/relationship_eras.csv"] = csv
                })));

        Assert.Contains("Relationships/relationship_eras.csv", error.Message);
        Assert.Contains("row 3", error.Message);
        Assert.Contains("field 'StartYear'", error.Message);
        Assert.Contains("'1801'", error.Message);
        Assert.Contains("expected 1800", error.Message);
    }

    [Fact]
    public void RareEventDiagnosticIdentifiesRangeFieldAndOwningItem()
    {
        const string csv =
            "ConditionId,StartYear,EndYear,BaseWeight,MinimumAffected,MaximumAffected\n" +
            "influenza,1700,,1.0,3,2";

        var error = Assert.Throws<InvalidDataException>(() =>
            RareEventEpidemicCatalog.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["RareEvents/rare_event_epidemic_conditions.csv"] = csv
                })));

        Assert.Contains("RareEvents/rare_event_epidemic_conditions.csv", error.Message);
        Assert.Contains("row 2", error.Message);
        Assert.Contains("item 'influenza'", error.Message);
        Assert.Contains("field 'MaximumAffected'", error.Message);
        Assert.Contains("'2'", error.Message);
        Assert.Contains("MinimumAffected (3)", error.Message);
    }

    [Fact]
    public void MalformedJsonDiagnosticIdentifiesFileAndJsonProperty()
    {
        const string json =
            "[{\"eventId\":\"marriage\",\"startYear\":\"not-a-year\",\"endYear\":null," +
            "\"eventType\":\"relationship.married\",\"textTemplate\":\"x\",\"biographyTemplate\":\"y\"}]";

        var error = Assert.Throws<InvalidDataException>(() =>
            RelationshipEventVariantCatalog.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["Relationships/relationship_event_variants.json"] = json
                })));

        Assert.Contains("Relationships/relationship_event_variants.json", error.Message);
        Assert.Contains("field '$[0].startYear'", error.Message);
        Assert.Contains("invalid JSON", error.Message);
    }

    [Fact]
    public void CurrentRepositoryCatalogsStillLoadWithSameValidData()
    {
        var data = CreateRepositoryData();

        Assert.NotNull(RetirementRuleCatalog.Load(data).GetRule(1900));
        Assert.NotNull(StandardRelationshipEraService.Load(data).GetRule(1900));
        Assert.True(HealthcareEraCatalog.Load(data).GetHealAmount(1900) > 0);
        CraftVocationDataValidation.Validate(data);
        Assert.Equal(29, CraftCatalog.Load(data).All.Count);
        Assert.NotEmpty(HobbyCatalog.Load(data).Hobbies);
        Assert.NotEmpty(RareEventEpidemicCatalog.Load(data).Entries);
    }

    private static IGameDataService CreateRepositoryData() =>
        new JsonGameDataService(RepositoryFiles.Path("data"));

    private sealed class InlineDataService : IGameDataService
    {
        private readonly IReadOnlyDictionary<string, string> _text;

        public InlineDataService(IReadOnlyDictionary<string, string> text)
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
