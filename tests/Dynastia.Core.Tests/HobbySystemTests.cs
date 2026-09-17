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
    public void PreferenceWeightsRemainFlavorOriented()
    {
        var hobby = new HobbyDefinition(
            "fishing",
            "Fishing",
            1700,
            null,
            9,
            1.0,
            "Rural",
            "strength",
            "intellect",
            "Phlegmatic",
            "Melancholic",
            "🎣");

        Assert.Equal(1.7, HobbyBalanceRules.TownMultiplier("Rural", SettlementClass.SmallTown));
        Assert.Equal(0.6, HobbyBalanceRules.TownMultiplier("Urban", SettlementClass.SmallTown));
        Assert.Equal(1.7, HobbyBalanceRules.TemperamentMultiplier(hobby, "Phlegmatic"));
        Assert.Equal(1.35, HobbyBalanceRules.TemperamentMultiplier(hobby, "Melancholic"));

        var strong = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = 5,
            ["intellect"] = 3,
            ["appeal"] = 3
        };
        var weak = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = 1,
            ["intellect"] = 3,
            ["appeal"] = 3
        };
        Assert.True(HobbyBalanceRules.StatMultiplier(hobby, strong)
            > HobbyBalanceRules.StatMultiplier(hobby, weak));
    }

    [Fact]
    public void CatalogueSupportsMinimumAgeBaseWeightAndThoughtArrays()
    {
        var catalog = HobbyCatalog.Load(new InlineDataService());

        var reading = catalog.Find("reading");
        Assert.NotNull(reading);
        Assert.True(reading!.IsAvailable(1700, 7));
        Assert.False(reading.IsAvailable(1699, 7));
        Assert.False(reading.IsAvailable(1700, 6));
        Assert.Equal(1.8, reading.BaseWeight, 10);
        Assert.Equal("intellect", reading.PrimaryStat);
        Assert.Equal("I like reading stories.", catalog.GetThoughts("reading").Child.Single());
    }

    [Fact]
    public void MinimumAgePreventsAdultHobbyFromEnteringChildSelectionPool()
    {
        var tavernGames = new HobbyDefinition(
            "tavern_games",
            "Tavern Games",
            1700,
            null,
            18,
            1.0,
            "Universal",
            "appeal",
            "intellect",
            "Sanguine",
            "Choleric",
            "🍻");

        Assert.False(tavernGames.IsAvailable(1700, 5));
        Assert.False(tavernGames.IsAvailable(1700, 17));
        Assert.True(tavernGames.IsAvailable(1700, 18));
    }

    private sealed class InlineDataService : IGameDataService
    {
        private const string Csv =
            "Id,Name,StartYear,EndYear,MinimumAge,BaseWeight,TownPreference,PrimaryStat,SecondaryStat,PrimaryTemperament,SecondaryTemperament,Emoji\n" +
            "reading,Reading,1700,,7,1.80,Universal,intellect,-,Melancholic,Phlegmatic,📚\n";

        private const string ContextCsv =
            "ItemId,StartYear,EndYear,Dimension,Value,WeightMultiplier\n" +
            "reading,1700,,AgeBand,Child,1.10\n";

        private const string Json =
            "{\"reading\":{\"Child\":[\"I like reading stories.\"],\"Adolescent\":[\"Reading.\"],\"AdultRough\":[\"Read.\"],\"AdultNormal\":[\"Reading.\"],\"AdultElaborate\":[\"Reading quietly.\"]}}";

        public IReadOnlyList<string> GetStringList(string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(string relativePath) =>
            throw new NotSupportedException();

        public string ReadText(string relativePath) =>
            relativePath.EndsWith("hobbies.csv", StringComparison.OrdinalIgnoreCase)
                ? Csv
                : relativePath.EndsWith("hobby_context_weights.csv", StringComparison.OrdinalIgnoreCase)
                    ? ContextCsv
                    : relativePath.EndsWith("hobby_thoughts.json", StringComparison.OrdinalIgnoreCase)
                        ? Json
                        : throw new FileNotFoundException(relativePath);
    }
}
