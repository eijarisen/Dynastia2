using System.Text.Json;
using Xunit;

namespace Dynastia.Core.Tests;

public sealed class Development11LocalityRecoverForeignBirthplacesTests
{
    [Fact]
    public void LocalServiceClustersUseHistoricalProxyRelationships()
    {
        var root = RepositoryRoot();
        var resolver = File.ReadAllText(Path.Combine(root,
            "plugins", "Dynastia.Mechanics.Locations", "StandardLocalServiceTownResolver.cs"));
        var catalog = File.ReadAllText(Path.Combine(root,
            "plugins", "Dynastia.Mechanics.Locations", "HistoricalTownCatalog.cs"));

        Assert.Contains("GetProxyPlaceId", catalog);
        Assert.Contains("ProxyPlaceId", catalog);
        Assert.Contains("OrderByDescending(item => item.Town.Population)", resolver);
        Assert.Contains("ThenByDescending(item => item.Id.Equals(root", resolver);
    }

    [Fact]
    public void AskToRecoverSupportsCareerCraftAndFarmWork()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "plugins", "Dynastia.Mechanics.Career", "CareerPlugin.FamilySupportActions.cs"));

        Assert.Contains("craftResolver()?.IsSelfEmployed(target)", source);
        Assert.Contains("farmingResolver()?.IsWorkingFarmWorker(target, actor)", source);
        Assert.Contains("target.Tags.Add(\n                            \"modifier.recover\")", source);
        Assert.DoesNotContain("maximumSatisfaction", source);
    }

    [Fact]
    public void ForeignBirthplaceDataRemainPresentationOnly()
    {
        var root = RepositoryRoot();
        var dataRoot = Path.Combine(root, "data", "Locations");
        Assert.True(File.Exists(Path.Combine(dataRoot, "foreign_cities.csv")));
        Assert.True(File.Exists(Path.Combine(dataRoot, "nationality_country_weights.csv")));

        using var rules = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(dataRoot, "foreign_birthplace_rules.json")));
        Assert.True(rules.RootElement
            .GetProperty("behavior")
            .GetProperty("foreignBirthplaceIsPresentationIdentityOnly")
            .GetBoolean());

        var locationComponent = File.ReadAllText(Path.Combine(root,
            "plugins", "Dynastia.Mechanics.Locations", "LocationComponent.cs"));
        Assert.Contains("ForeignBirthplaceCity", locationComponent);
        Assert.Contains("ForeignBirthplaceCountry", locationComponent);
    }

    [Fact]
    public void OverlapLocationPickerWaitsHalfSecondForDoubleClick()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "src", "Dynastia.App", "Map", "Views", "TownMapPanel.cs"));

        Assert.Contains("e.ClickCount >= 2", source);
        Assert.Contains("Task.Delay(500)", source);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dynastia.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
