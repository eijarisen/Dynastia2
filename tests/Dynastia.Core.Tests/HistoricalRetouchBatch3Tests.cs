using Dynastia.Contracts;
using Dynastia.Mechanics.Historical;
using Dynastia.Mechanics.Loans;
using Dynastia.Mechanics.Relationships;

namespace Dynastia.Core.Tests;

public sealed class HistoricalRetouchBatch3Tests
{
    private const string RelationshipEraData =
        "StartYear,EndYear,MarriageChanceMultiplier,ArrangedMarriageMultiplier,AutomaticDivorceMultiplier\n" +
        "1700,1799,1.10,1.25,0.20\n" +
        "1800,1849,1.08,1.20,0.25\n" +
        "1850,1913,1.05,1.15,0.35\n" +
        "1914,1945,1.03,1.10,0.50\n" +
        "1946,1989,1.00,1.05,0.75\n" +
        "1990,,1.00,1.00,1.00\n";

    private const string RelationshipEventVariantData =
        "[" +
        "{\"eventId\":\"relationship.same_sex_union\",\"startYear\":1700,\"endYear\":1899,\"eventType\":\"relationship.partnered\",\"textTemplate\":\"{person} formed a lasting companionship with {spouse}.\",\"biographyTemplate\":\"formed a lasting companionship with {spouse}\"}," +
        "{\"eventId\":\"relationship.same_sex_union\",\"startYear\":1900,\"endYear\":1989,\"eventType\":\"relationship.partnered\",\"textTemplate\":\"{person} formed a private partnership with {spouse}.\",\"biographyTemplate\":\"formed a private partnership with {spouse}\"}," +
        "{\"eventId\":\"relationship.same_sex_union\",\"startYear\":1990,\"endYear\":null,\"eventType\":\"relationship.partnered\",\"textTemplate\":\"{person} entered a partnership with {spouse}.\",\"biographyTemplate\":\"entered a partnership with {spouse}\"}" +
        "]";

    private const string LoanEraData =
        "StartYear,EndYear,ExternalCreditorLabel,TakeLoanEventPhrase\n" +
        "1700,1799,Moneylender,borrowed money from a private moneylender\n" +
        "1800,1849,Lender,took a loan from a private lender\n" +
        "1850,,Bank,took a bank loan\n";

    private const string HistoricalActionData =
        "[" +
        "{\"actionId\":\"relationship.find_spouse\",\"startYear\":1700,\"endYear\":1899,\"label\":\"Seek a Match\",\"description\":\"Early\",\"narrative\":\"sought a match\"}," +
        "{\"actionId\":\"relationship.find_spouse\",\"startYear\":1900,\"endYear\":null,\"label\":\"Find a Spouse\",\"description\":\"Modern\",\"narrative\":\"looked for a spouse\"}," +
        "{\"actionId\":\"relationship.marry_off_daughter\",\"startYear\":1700,\"endYear\":1945,\"label\":\"Arrange a Marriage for Daughter\",\"description\":\"Early\",\"narrative\":\"arranged a marriage\"}," +
        "{\"actionId\":\"relationship.marry_off_daughter\",\"startYear\":1946,\"endYear\":null,\"label\":\"Marry Off Daughter\",\"description\":\"Modern\",\"narrative\":\"helped find a husband\"}," +
        "{\"actionId\":\"reproduction.try_for_baby\",\"startYear\":1700,\"endYear\":1945,\"label\":\"Try for a Child\",\"description\":\"Early\",\"narrative\":\"prioritized a child\"}," +
        "{\"actionId\":\"reproduction.try_for_baby\",\"startYear\":1946,\"endYear\":null,\"label\":\"Try for a Baby\",\"description\":\"Modern\",\"narrative\":\"prioritized a baby\"}," +
        "{\"actionId\":\"household.hire_nanny\",\"startYear\":1700,\"endYear\":1849,\"label\":\"Hire a Nursemaid\",\"description\":\"Early\",\"narrative\":\"hired a nursemaid\"}," +
        "{\"actionId\":\"household.hire_nanny\",\"startYear\":1850,\"endYear\":null,\"label\":\"Hire a Nanny\",\"description\":\"Modern\",\"narrative\":\"hired a nanny\"}," +
        "{\"actionId\":\"loan.take\",\"startYear\":1700,\"endYear\":1799,\"label\":\"Take a Loan\",\"description\":\"Moneylender\",\"narrative\":\"borrowed from a moneylender\"}," +
        "{\"actionId\":\"loan.take\",\"startYear\":1800,\"endYear\":1849,\"label\":\"Take a Loan\",\"description\":\"Lender\",\"narrative\":\"borrowed from a lender\"}," +
        "{\"actionId\":\"loan.take\",\"startYear\":1850,\"endYear\":null,\"label\":\"Take a Loan\",\"description\":\"Bank\",\"narrative\":\"borrowed from a bank\"}" +
        "]";

    [Fact]
    public void RelationshipEraRulesApplyHistoricalMultipliersInTheExpectedOrder()
    {
        var service = StandardRelationshipEraService.Load(
            new InlineDataService(new Dictionary<string, string>
            {
                ["Relationships/relationship_eras.csv"] = RelationshipEraData
            }));

        var earlyModern = service.GetRule(1700);
        Assert.Equal(0.11, earlyModern.ApplyMarriageChance(0.10), 6);
        Assert.Equal(0.625, earlyModern.ApplyArrangedMarriageChance(0.50), 6);
        Assert.Equal(0.02, earlyModern.ApplyAutomaticDivorceChance(0.10), 6);

        Assert.Equal(1.08, service.GetRule(1800).MarriageChanceMultiplier, 6);
        Assert.Equal(1.15, service.GetRule(1850).ArrangedMarriageMultiplier, 6);
        Assert.Equal(0.50, service.GetRule(1914).AutomaticDivorceMultiplier, 6);
        Assert.Equal(0.75, service.GetRule(1946).AutomaticDivorceMultiplier, 6);
        Assert.Equal(1.00, service.GetRule(1990).MarriageChanceMultiplier, 6);

        Assert.Equal(
            0.95,
            earlyModern.ApplyArrangedMarriageChance(0.80),
            6);
    }

    [Fact]
    public void RelationshipEventVariantsUseEraAppropriateSameSexUnionText()
    {
        var catalog = RelationshipEventVariantCatalog.Load(
            new InlineDataService(new Dictionary<string, string>
            {
                ["Relationships/relationship_event_variants.json"] = RelationshipEventVariantData
            }));

        var early = catalog.GetVariant("relationship.same_sex_union", 1700);
        var twentiethCentury = catalog.GetVariant("relationship.same_sex_union", 1900);
        var contemporary = catalog.GetVariant("relationship.same_sex_union", 1990);

        Assert.Equal(
            "Jan formed a lasting companionship with Adam.",
            early.FormatText("Jan", "Adam"));
        Assert.Equal(
            "formed a private partnership with Adam",
            twentiethCentury.FormatBiographyVerb("Adam"));
        Assert.Equal(
            "Jan entered a partnership with Adam.",
            contemporary.FormatText("Jan", "Adam"));
        Assert.Equal(
            "Jan entered a partnership with Adam.",
            catalog.GetVariant("relationship.same_sex_union", 2000)
                .FormatText("Jan", "Adam"));

        Assert.DoesNotContain(
            "came out",
            early.TextTemplate + " " +
            twentiethCentury.TextTemplate + " " +
            contemporary.TextTemplate,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoanEraRulesUseMoneylenderThenLenderThenBank()
    {
        var catalog = LoanEraCatalog.Load(
            new InlineDataService(new Dictionary<string, string>
            {
                ["Loans/loan_eras.csv"] = LoanEraData
            }));

        Assert.Equal("Moneylender", catalog.GetRule(1700).ExternalCreditorLabel);
        Assert.Equal("Lender", catalog.GetRule(1800).ExternalCreditorLabel);
        Assert.Equal("Bank", catalog.GetRule(1850).ExternalCreditorLabel);
        Assert.Equal("Bank", catalog.GetRule(1900).ExternalCreditorLabel);
        Assert.Equal("Bank", catalog.GetRule(2000).ExternalCreditorLabel);
        Assert.Equal(
            "borrowed money from a private moneylender",
            catalog.GetRule(1799).TakeLoanEventPhrase);
        Assert.Equal(
            "took a loan from a private lender",
            catalog.GetRule(1849).TakeLoanEventPhrase);
        Assert.Equal(
            "took a bank loan",
            catalog.GetRule(1850).TakeLoanEventPhrase);
    }

    [Fact]
    public void HistoricalActionWordingChangesAtBatchThreeBoundaries()
    {
        var service = HistoricalActionVariantService.Load(
            new InlineDataService(new Dictionary<string, string>
            {
                ["Common/historical_action_variants.json"] = HistoricalActionData
            }));

        Assert.Equal("Seek a Match", service.GetVariant("relationship.find_spouse", 1899)?.Label);
        Assert.Equal("Find a Spouse", service.GetVariant("relationship.find_spouse", 1900)?.Label);

        Assert.Equal("Arrange a Marriage for Daughter", service.GetVariant("relationship.marry_off_daughter", 1945)?.Label);
        Assert.Equal("Marry Off Daughter", service.GetVariant("relationship.marry_off_daughter", 1946)?.Label);

        Assert.Equal("Try for a Child", service.GetVariant("reproduction.try_for_baby", 1945)?.Label);
        Assert.Equal("Try for a Baby", service.GetVariant("reproduction.try_for_baby", 1946)?.Label);

        Assert.Equal("Hire a Nursemaid", service.GetVariant("household.hire_nanny", 1849)?.Label);
        Assert.Equal("Hire a Nanny", service.GetVariant("household.hire_nanny", 1850)?.Label);

        Assert.Equal("Moneylender", service.GetVariant("loan.take", 1799)?.Description);
        Assert.Equal("Lender", service.GetVariant("loan.take", 1800)?.Description);
        Assert.Equal("Bank", service.GetVariant("loan.take", 1850)?.Description);
    }

    [Fact]
    public void EraCatalogsRejectCoverageGaps()
    {
        var relationshipGap =
            "StartYear,EndYear,MarriageChanceMultiplier,ArrangedMarriageMultiplier,AutomaticDivorceMultiplier\n" +
            "1700,1799,1,1,0.2\n" +
            "1801,,1,1,1\n";

        var loanGap =
            "StartYear,EndYear,ExternalCreditorLabel,TakeLoanEventPhrase\n" +
            "1700,1799,Moneylender,borrowed from a moneylender\n" +
            "1801,,Bank,took a bank loan\n";

        var relationshipEventGap =
            "[" +
            "{\"eventId\":\"relationship.same_sex_union\",\"startYear\":1700,\"endYear\":1899,\"eventType\":\"relationship.partnered\",\"textTemplate\":\"{person} formed a companionship with {spouse}.\",\"biographyTemplate\":\"formed a companionship with {spouse}\"}," +
            "{\"eventId\":\"relationship.same_sex_union\",\"startYear\":1901,\"endYear\":null,\"eventType\":\"relationship.partnered\",\"textTemplate\":\"{person} entered a partnership with {spouse}.\",\"biographyTemplate\":\"entered a partnership with {spouse}\"}" +
            "]";

        var overlappingActions =
            "[" +
            "{\"actionId\":\"relationship.find_spouse\",\"startYear\":1700,\"endYear\":1900,\"label\":\"Seek a Match\",\"description\":\"Early\",\"narrative\":\"sought\"}," +
            "{\"actionId\":\"relationship.find_spouse\",\"startYear\":1900,\"endYear\":null,\"label\":\"Find a Spouse\",\"description\":\"Modern\",\"narrative\":\"searched\"}" +
            "]";

        Assert.Throws<InvalidDataException>(() =>
            StandardRelationshipEraService.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["Relationships/relationship_eras.csv"] = relationshipGap
                })));

        Assert.Throws<InvalidDataException>(() =>
            LoanEraCatalog.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["Loans/loan_eras.csv"] = loanGap
                })));

        Assert.Throws<InvalidDataException>(() =>
            RelationshipEventVariantCatalog.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["Relationships/relationship_event_variants.json"] =
                        relationshipEventGap
                })));

        Assert.Throws<InvalidDataException>(() =>
            HistoricalActionVariantService.Load(
                new InlineDataService(new Dictionary<string, string>
                {
                    ["Common/historical_action_variants.json"] =
                        overlappingActions
                })));
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
