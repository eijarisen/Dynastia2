using Dynastia.Contracts;
using Dynastia.Mechanics.Career;

namespace Dynastia.Core.Tests;

public sealed class HistoricalRetouchBatch6Tests
{
    [Fact]
    public void HistoricalCareerTitlesUseEraOverrides()
    {
        var catalog = HistoricalCareerPresentationCatalog.Load(
            CreateData(),
            ["healthcare_services", "banking"]);

        Assert.Equal(
            "Senior Physician",
            catalog.ResolveCareerTitle(
                "healthcare_services",
                4,
                "Clinic Director",
                1750));

        Assert.Equal(
            "Banking House Partner",
            catalog.ResolveCareerTitle(
                "banking",
                4,
                "Bank Director",
                1820));
    }

    [Fact]
    public void CareerPresentationFallsBackToBaseAfterVariantEnds()
    {
        var catalog = HistoricalCareerPresentationCatalog.Load(
            CreateData(),
            ["healthcare_services", "banking"]);

        Assert.Equal(
            "Clinic Director",
            catalog.ResolveCareerTitle(
                "healthcare_services",
                4,
                "Clinic Director",
                1850));

        Assert.Equal(
            "Bank Director",
            catalog.ResolveCareerTitle(
                "banking",
                4,
                "Bank Director",
                1850));
    }

    [Theory]
    [InlineData(1750, "Pupil")]
    [InlineData(1900, "Schoolchild")]
    [InlineData(2000, "Student")]
    public void StudentStatusUsesHistoricalTerminology(
        int year,
        string expected)
    {
        var catalog = HistoricalCareerPresentationCatalog.Load(
            CreateData(),
            ["healthcare_services", "banking"]);

        Assert.Equal(
            expected,
            catalog.ResolveStatus(
                "status.student",
                "Student",
                year));
    }

    [Fact]
    public void NannyAndHousewifeStatusesUseHistoricalTerminology()
    {
        var catalog = HistoricalCareerPresentationCatalog.Load(
            CreateData(),
            ["healthcare_services", "banking"]);

        Assert.Equal(
            "Nursemaid",
            catalog.ResolveStatus(
                "role.nanny",
                "Nanny",
                1750));

        Assert.Equal(
            "Keeping House",
            catalog.ResolveStatus(
                "status.housewife",
                "Housewife",
                1750));

        Assert.Equal(
            "Caring for Children",
            catalog.ResolveStatus(
                "role.family_nanny",
                "Family Nanny",
                1750));
    }

    [Fact]
    public void MissingCareerVariantFallsBackToBasePresentation()
    {
        var catalog = HistoricalCareerPresentationCatalog.Load(
            CreateData(),
            ["healthcare_services", "banking"]);

        Assert.Equal(
            "Medicine & Healthcare",
            catalog.ResolveCareerName(
                "healthcare_services",
                "Medicine & Healthcare",
                1900));
    }

    [Fact]
    public void HistoricalPresentationRejectsUnknownCareerIds()
    {
        var data = CreateData(
            careerVariants:
                "CareerId,StartYear,EndYear,DisplayName,Level1Title,Level2Title,Level3Title,Level4Title,Level5Title\n" +
                "unknown,1700,1849,Unknown,A,B,C,D,E\n");

        Assert.Throws<InvalidDataException>(() =>
            HistoricalCareerPresentationCatalog.Load(
                data,
                ["healthcare_services", "banking"]));
    }

    [Fact]
    public void HistoricalPresentationRejectsOverlappingStatusRanges()
    {
        var data = CreateData(
            statusVariants:
                "StatusId,StartYear,EndYear,Label\n" +
                "status.student,1700,1850,Pupil\n" +
                "status.student,1850,,Student\n");

        Assert.Throws<InvalidDataException>(() =>
            HistoricalCareerPresentationCatalog.Load(
                data,
                ["healthcare_services", "banking"]));
    }

    private static InlineDataService CreateData(
        string? careerVariants = null,
        string? statusVariants = null)
    {
        return new InlineDataService(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Career/career_title_variants.csv"] =
                    careerVariants
                    ?? "CareerId,StartYear,EndYear,DisplayName,Level1Title,Level2Title,Level3Title,Level4Title,Level5Title\n" +
                       "healthcare_services,1700,1849,Medicine,Medical Attendant,Medical Apprentice,Physician,Senior Physician,Medical Practice Proprietor\n" +
                       "banking,1800,1849,Banking,Bank Messenger,Bank Clerk,Banker,Banking House Partner,Banking House Proprietor\n",
                ["Common/person_status_variants.csv"] =
                    statusVariants
                    ?? "StatusId,StartYear,EndYear,Label\n" +
                       "status.preschool,1700,1945,Young Child\n" +
                       "status.preschool,1946,,Preschool\n" +
                       "status.student,1700,1849,Pupil\n" +
                       "status.student,1850,1945,Schoolchild\n" +
                       "status.student,1946,,Student\n" +
                       "status.unemployed,1700,1849,Without Occupation\n" +
                       "status.unemployed,1850,,Unemployed\n" +
                       "status.housewife,1700,1899,Keeping House\n" +
                       "status.housewife,1900,,Housewife\n" +
                       "role.nanny,1700,1849,Nursemaid\n" +
                       "role.nanny,1850,,Nanny\n" +
                       "role.family_nanny,1700,1849,Caring for Children\n" +
                       "role.family_nanny,1850,,Family Nanny\n"
            });
    }

    private sealed class InlineDataService : IGameDataService
    {
        private readonly IReadOnlyDictionary<string, string> _data;

        public InlineDataService(
            IReadOnlyDictionary<string, string> data)
        {
            _data = data;
        }

        public IReadOnlyList<string> GetStringList(
            string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(
            string relativePath) =>
            throw new NotSupportedException();

        public string ReadText(
            string relativePath) =>
            _data.TryGetValue(relativePath, out var text)
                ? text
                : throw new FileNotFoundException(relativePath);
    }
}
