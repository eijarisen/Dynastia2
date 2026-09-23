using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Locations;

namespace Dynastia.Core.Tests;

public sealed class HistoricalTownCatalogV12CorrectionTests
{
    private const string OlsztynCzestochowski = "p_7cfc3011031b4bf4";
    private const string Kozieglowy = "p_7214fd4d31db2f0d";
    private const string Zgorzelec = "p_d15854090782643a";
    private const string Luban = "p_c5f89b78b4b3b407";
    private const string PruszczGdanski = "p_d106717b938bff1c";
    private const string NowyDworGdanski = "p_b5d06b0e625b43d2";
    private const string CzechowiceDziedzice = "p_834e3f88249848a4";
    private const string BrzescNadBugiem = "p_c7cea4d5b924f3ff";
    private const string KrynicaZdroj = "p_a1fa7dec364d5b7e";
    private const string PiwnicznaZdroj = "p_5d8fef0506fd91f3";
    private const string SuchaBeskidzka = "p_66cf5302386ca8bc";
    private const string StarogardGdanski = "p_62e34d1c0170214e";
    private const string Krystynopol = "p_5eec4a58f6a3e111";

    private static readonly string[] AddedEasternPlaces =
    [
        "p_dcc7c1e678c773f3", // Mścisław
        "p_ebfcea88f9c7fdb3", // Brasław
        "p_e2d0982020b4d5b9", // Newel
        "p_9b04daf12a181223", // Siebież
        "p_b60e1736217edc0c"  // Wieliż
    ];

    [Fact]
    public void V12EasternAdditionsRemainPresentInLaterCatalogueReleases()
    {
        var catalog = Load();

        foreach (var placeId in AddedEasternPlaces)
        {
            var town = catalog.GetTown(placeId, 1700);
            Assert.NotNull(town);
            Assert.Equal(placeId, town!.Id);
        }

        Assert.Equal("PLC", catalog.GetTown("p_dcc7c1e678c773f3", 1771)!.PolityId);
        Assert.Equal("RU_EMPIRE", catalog.GetTown("p_dcc7c1e678c773f3", 1772)!.PolityId);
        Assert.Equal("PLC", catalog.GetTown("p_ebfcea88f9c7fdb3", 1794)!.PolityId);
        Assert.Equal("RU_EMPIRE", catalog.GetTown("p_ebfcea88f9c7fdb3", 1795)!.PolityId);
    }

    [Fact]
    public void V12CorrectsKnownTerritorialTransitions()
    {
        var catalog = Load();

        Assert.Equal("PL", catalog.GetTown(OlsztynCzestochowski, 1918)!.PolityId);

        Assert.Equal("PLC", catalog.GetTown(Kozieglowy, 1794)!.PolityId);
        Assert.Equal("PRUSSIA", catalog.GetTown(Kozieglowy, 1795)!.PolityId);

        Assert.Equal("SAXONY", catalog.GetTown(Zgorzelec, 1814)!.PolityId);
        Assert.Equal("PRUSSIA", catalog.GetTown(Zgorzelec, 1815)!.PolityId);
        Assert.Equal("SAXONY", catalog.GetTown(Luban, 1814)!.PolityId);
        Assert.Equal("PRUSSIA", catalog.GetTown(Luban, 1815)!.PolityId);

        Assert.Equal("FREE_DANZIG_NAPOLEONIC", catalog.GetTown(PruszczGdanski, 1807)!.PolityId);
        Assert.Equal("FREE_DANZIG", catalog.GetTown(PruszczGdanski, 1920)!.PolityId);
        Assert.Equal("FREE_DANZIG", catalog.GetTown(NowyDworGdanski, 1920)!.PolityId);
    }

    [Fact]
    public void V12CorrectsHistoricalNameTimelines()
    {
        var catalog = Load();

        Assert.Equal("Czechowice", catalog.GetTown(CzechowiceDziedzice, 1951)!.Town);
        Assert.Equal("Czechowice-Dziedzice", catalog.GetTown(CzechowiceDziedzice, 1959)!.Town);

        Assert.Equal("Brześć Litewski", catalog.GetTown(BrzescNadBugiem, 1922)!.Town);
        Assert.Equal("Brześć nad Bugiem", catalog.GetTown(BrzescNadBugiem, 1923)!.Town);

        Assert.Equal("Krynica", catalog.GetTown(KrynicaZdroj, 1900)!.Town);
        Assert.Equal("Krynica-Zdrój", catalog.GetTown(KrynicaZdroj, 2020)!.Town);
        Assert.Equal("Piwniczna", catalog.GetTown(PiwnicznaZdroj, 1900)!.Town);
        Assert.Equal("Piwniczna-Zdrój", catalog.GetTown(PiwnicznaZdroj, 2020)!.Town);
        Assert.Equal("Sucha", catalog.GetTown(SuchaBeskidzka, 1900)!.Town);
        Assert.Equal("Sucha Beskidzka", catalog.GetTown(SuchaBeskidzka, 2020)!.Town);
        Assert.Equal("Starogard", catalog.GetTown(StarogardGdanski, 1949)!.Town);
        Assert.Equal("Starogard Gdański", catalog.GetTown(StarogardGdanski, 1950)!.Town);
    }

    [Fact]
    public void V12LegalInterwarTownCountsMatchHistoricalCheckpoints()
    {
        var catalog = Load();

        var towns1937 = catalog
            .GetAvailableTowns(1937, TownMapMode.PolishPolities)
            .Count(town => town.UrbanStatus.Equals("town", StringComparison.OrdinalIgnoreCase));
        var towns1939 = catalog
            .GetAvailableTowns(1939, TownMapMode.PolishPolities)
            .Count(town => town.UrbanStatus.Equals("town", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(603, towns1937);
        Assert.Equal(604, towns1939);
        Assert.Equal("historic_market_town", catalog.GetTown(Krystynopol, 1937)!.UrbanStatus);
        Assert.Equal("historic_market_town", catalog.GetTown(Krystynopol, 1939)!.UrbanStatus);
    }

    [Fact]
    public void V12KeepsOnlySourcedAutomaticMergerChronology()
    {
        var catalog = Load();

        const string kroscienko = "p_81b0145cd1911cdc";
        const string szczawnica = "p_1415f189f4307dca";
        const string rzochow = "p_c8b07568046cd551";
        const string mielec = "p_c023ff565cade74b";

        Assert.Equal(kroscienko, catalog.ResolveMunicipality(kroscienko, 1972));
        Assert.Equal(szczawnica, catalog.ResolveMunicipality(kroscienko, 1973));
        Assert.Equal(kroscienko, catalog.ResolveMunicipality(kroscienko, 1982));

        Assert.Equal(rzochow, catalog.ResolveMunicipality(rzochow, 1984));
        Assert.Equal(mielec, catalog.ResolveMunicipality(rzochow, 1985));
    }

    [Fact]
    public void V12RepairsMajorEasternPopulationAnchors()
    {
        var catalog = Load();

        Assert.True(catalog.GetTown("p_bbd34a79844377da", 2020)!.Population > 1_000_000); // Mińsk
        Assert.True(catalog.GetTown("p_ccd43e53d02b472b", 2020)!.Population > 250_000); // Kowno
        Assert.True(catalog.GetTown("p_af5b68a40405ca3e", 2020)!.Population > 50_000); // Dyneburg
        Assert.True(catalog.GetTown("p_cc2fd86c527f1fb0", 2020)!.Population > 200_000); // Żytomierz
    }

    [Fact]
    public void V12TownOpportunityRowsDoNotPrecedeMergedTownIdentities()
    {
        var root = RepositoryFiles.Root;
        var rows = File.ReadAllLines(Path.Combine(root, "data", "Towns", "town_opportunities.csv"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Skip(1)
            .Select(line => line.Split(','))
            .ToArray();

        Assert.All(
            rows.Where(parts => parts[0] == "p_39338ef213f804ee"),
            parts => Assert.True(int.Parse(parts[2]) >= 1951)); // Bielsko-Biała
        Assert.All(
            rows.Where(parts => parts[0] == "p_853c3f1e44364c65"),
            parts => Assert.True(int.Parse(parts[2]) >= 1959)); // Ruda Śląska
    }

    private static HistoricalTownCatalog Load()
    {
        var root = RepositoryFiles.Root;
        return HistoricalTownCatalog.Load(
            new JsonGameDataService(Path.Combine(root, "data")));
    }

}
