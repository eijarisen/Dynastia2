using System.Text.Json;
using Dynastia.Mechanics.FamilyRelations;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed class AdultSonsHouseholdReworkTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(14, true)]
    public void OvercrowdingStartsAboveEightResidents(
        int residents,
        bool expected)
    {
        Assert.Equal(
            expected,
            HouseholdCrowdingRules.IsOvercrowded(residents));
    }

    [Fact]
    public void OvercrowdingUsesDesignedAnnualPenalties()
    {
        Assert.Equal(8, HouseholdCrowdingRules.OvercrowdingThreshold);
        Assert.Equal(3, HouseholdCrowdingRules.AnnualHealthPenalty);
        Assert.Equal(2, HouseholdCrowdingRules.AnnualStressPenalty);
    }

    [Theory]
    [InlineData("Sanguine", "Good", 0.20)]
    [InlineData("Phlegmatic", "Neutral", 0.20)]
    [InlineData("Melancholic", "Good", 0.35)]
    [InlineData("Choleric", "Neutral", 0.40)]
    [InlineData("Sanguine", "Evil", 0.40)]
    [InlineData("Melancholic", "Evil", 0.55)]
    [InlineData("Choleric", "Evil", 0.60)]
    public void MoveOutRefusalMatchesDesignedPersonalityTable(
        string temperament,
        string morals,
        double expected)
    {
        Assert.Equal(
            expected,
            MoveOutRules.CalculateRefusalChance(
                temperament,
                morals),
            precision: 8);
    }

    [Fact]
    public void HistoricalCatalogContainsBothSonMarriageEras()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "data",
            "Common",
            "historical_action_variants.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var variants = document.RootElement.EnumerateArray()
            .Where(item => item.GetProperty("actionId").GetString()
                == "relationship.marry_off_son")
            .ToList();

        Assert.Equal(2, variants.Count);
        Assert.Contains(variants, item =>
            item.GetProperty("startYear").GetInt32() == 1700
            && item.GetProperty("endYear").GetInt32() == 1945
            && item.GetProperty("label").GetString()
                == "Arrange a Marriage for Son");
        Assert.Contains(variants, item =>
            item.GetProperty("startYear").GetInt32() == 1946
            && item.GetProperty("endYear").ValueKind == JsonValueKind.Null
            && item.GetProperty("label").GetString()
                == "Help Son Find a Wife");
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dynastia.slnx")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
