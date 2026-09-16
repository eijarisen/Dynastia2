using Dynastia.Contracts;
using Dynastia.Mechanics.Relationships;

namespace Dynastia.Core.Tests;

public sealed class PartnerSearchRulesTests
{
    [Fact]
    public void EqualPartnerValuesGiveHighButNotGuaranteedAcceptance()
    {
        Assert.Equal(
            0.85,
            PartnerSearchRules.CalculateAcceptanceChance(70, 70),
            10);
    }

    [Fact]
    public void AmbitiousMatchesBecomeProgressivelyHarderAndRemainPossible()
    {
        Assert.Equal(
            0.65,
            PartnerSearchRules.CalculateAcceptanceChance(60, 70),
            10);
        Assert.Equal(
            0.45,
            PartnerSearchRules.CalculateAcceptanceChance(60, 80),
            10);
        Assert.Equal(
            0.10,
            PartnerSearchRules.CalculateAcceptanceChance(0, 100),
            10);
    }

    [Fact]
    public void FemalePartnerValueRewardsRemainingReproductiveAge()
    {
        var traits = new[] { 5, 5, 5, 5, 5, 5 };

        var young = PartnerSearchRules.CalculatePartnerValue(
            Sex.Female,
            18,
            traits,
            5,
            0);
        var older = PartnerSearchRules.CalculatePartnerValue(
            Sex.Female,
            45,
            traits,
            5,
            0);

        Assert.Equal(100, young, 10);
        Assert.Equal(80, older, 10);
    }

    [Fact]
    public void MalePartnerValueRewardsCareerAndHouseholdResources()
    {
        var traits = new[] { 5, 5, 5, 5, 5, 5 };

        var withoutResources = PartnerSearchRules.CalculatePartnerValue(
            Sex.Male,
            30,
            traits,
            5,
            5);
        var established = PartnerSearchRules.CalculatePartnerValue(
            Sex.Male,
            30,
            traits,
            5,
            5,
            householdWealth: 20_000m,
            housesOwned: 2);

        Assert.Equal(90, withoutResources, 10);
        Assert.Equal(100, established, 10);
        Assert.True(established > withoutResources);
    }

    [Fact]
    public void HusbandOriginsBroadenAcrossModernCenturiesWhileSameTownStaysMostLikely()
    {
        var eighteenth =
            HusbandOriginSelector.GetDistribution(1750);
        var nineteenth =
            HusbandOriginSelector.GetDistribution(1850);
        var twentieth =
            HusbandOriginSelector.GetDistribution(1950);
        var twentyFirst =
            HusbandOriginSelector.GetDistribution(2025);

        foreach (var distribution in new[]
                 {
                     eighteenth,
                     nineteenth,
                     twentieth,
                     twentyFirst
                 })
        {
            Assert.Equal(1.0, distribution.Total, 10);
            Assert.True(
                distribution.SameTownChance
                > distribution.NearbyTownChance);
            Assert.True(
                distribution.SameTownChance
                > distribution.RegionalCityChance);
            Assert.True(
                distribution.SameTownChance
                > distribution.NationalChance);
        }

        Assert.True(nineteenth.NationalChance > eighteenth.NationalChance);
        Assert.True(twentieth.NationalChance > nineteenth.NationalChance);
        Assert.True(twentyFirst.NationalChance > twentieth.NationalChance);
    }
}
