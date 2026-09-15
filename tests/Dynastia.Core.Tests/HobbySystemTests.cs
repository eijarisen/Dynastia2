using Dynastia.Contracts;
using Dynastia.Mechanics.Hobbies;

namespace Dynastia.Core.Tests;

public sealed class HobbySystemTests
{
    [Fact]
    public void HobbyAcquisitionUsesTwoPercentAnnualChanceAndTwoSlotMaximum()
    {
        Assert.Equal(2, HobbyBalanceRules.MaximumHobbies);
        Assert.Equal(0.02, HobbyBalanceRules.AnnualAcquisitionChance);
    }

    [Fact]
    public void HistoricalPreferenceWeightsMatchDesign()
    {
        var hobby = new HobbyDefinition(
            "fishing",
            "Fishing",
            1700,
            null,
            9,
            "Rural",
            "Male-leaning",
            "Phlegmatic",
            "Melancholic",
            "🎣");

        Assert.Equal(
            1.7,
            HobbyBalanceRules.TownMultiplier(
                "Rural",
                SettlementClass.SmallTown));

        Assert.Equal(
            0.6,
            HobbyBalanceRules.TownMultiplier(
                "Urban",
                SettlementClass.SmallTown));

        Assert.Equal(
            1.6,
            HobbyBalanceRules.GenderMultiplier(
                "Male-leaning",
                Sex.Male));

        Assert.Equal(
            0.75,
            HobbyBalanceRules.GenderMultiplier(
                "Male-leaning",
                Sex.Female));

        Assert.Equal(
            1.7,
            HobbyBalanceRules.TemperamentMultiplier(
                hobby,
                "Phlegmatic"));

        Assert.Equal(
            1.35,
            HobbyBalanceRules.TemperamentMultiplier(
                hobby,
                "Melancholic"));
    }

    [Fact]
    public void CatalogueSupportsHistoricalAvailabilityAndThoughtArrays()
    {
        var catalog = HobbyCatalog.Load(
            new InlineDataService());

        var reading = catalog.Find("reading");
        Assert.NotNull(reading);
        Assert.True(reading!.IsAvailable(1700, 7));
        Assert.False(reading.IsAvailable(1699, 7));
        Assert.False(reading.IsAvailable(1700, 6));
        Assert.True(reading.IsHistoricallyAvailable(1700));
        Assert.Equal(
            "I like reading stories.",
            catalog.GetThoughts("reading").Child.Single());
    }

    private sealed class InlineDataService :
        IGameDataService
    {
        private const string Csv =
            "Id,Name,StartYear,EndYear,MinimumAge,TownPreference,GenderPreference,PrimaryTemperament,SecondaryTemperament,Emoji\n" +
            "reading,Reading,1700,,7,Universal,Neutral,Melancholic,Phlegmatic,📚\n";

        private const string Json =
            "{\"reading\":{\"Child\":[\"I like reading stories.\"],\"Adolescent\":[\"Reading.\"],\"AdultRough\":[\"Read.\"],\"AdultNormal\":[\"Reading.\"],\"AdultElaborate\":[\"Reading quietly.\"]}}";

        public IReadOnlyList<string> GetStringList(
            string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(
            string relativePath) =>
            throw new NotSupportedException();

        public string ReadText(
            string relativePath) =>
            relativePath.EndsWith(
                "hobbies.csv",
                StringComparison.OrdinalIgnoreCase)
                    ? Csv
                    : relativePath.EndsWith(
                        "hobby_thoughts.json",
                        StringComparison.OrdinalIgnoreCase)
                        ? Json
                        : throw new FileNotFoundException(relativePath);
    }
}
