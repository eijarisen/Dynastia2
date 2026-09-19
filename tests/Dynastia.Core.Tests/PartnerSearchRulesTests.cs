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
            PartnerSearchRules.CalculateAcceptanceChance(
                70,
                70,
                Sex.Female),
            10);
        Assert.Equal(
            0.87,
            PartnerSearchRules.CalculateAcceptanceChance(
                70,
                70,
                Sex.Male),
            10);
    }

    [Fact]
    public void AmbitiousMatchesBecomeProgressivelyHarderAndRemainPossible()
    {
        Assert.Equal(
            0.65,
            PartnerSearchRules.CalculateAcceptanceChance(
                60,
                70,
                Sex.Female),
            10);
        Assert.Equal(
            0.45,
            PartnerSearchRules.CalculateAcceptanceChance(
                60,
                80,
                Sex.Female),
            10);
        Assert.Equal(
            0.10,
            PartnerSearchRules.CalculateAcceptanceChance(
                0,
                100,
                Sex.Female),
            10);
    }


    [Fact]
    public void MaleCandidatesAreMoreWillingWithoutDefaultingToNinetyFivePercent()
    {
        var equal = PartnerSearchRules.CalculateAcceptanceChance(
            70,
            70,
            Sex.Male);
        var slightlyAdvantageous = PartnerSearchRules.CalculateAcceptanceChance(
            75,
            70,
            Sex.Male);
        var ambitious = PartnerSearchRules.CalculateAcceptanceChance(
            60,
            75,
            Sex.Male);

        Assert.Equal(0.87, equal, 10);
        Assert.Equal(0.885, slightlyAdvantageous, 10);
        Assert.Equal(0.825, ambitious, 10);
        Assert.True(slightlyAdvantageous < 0.95);
    }

    [Fact]
    public void CandidateEducationTracksIntellectWithinHistoricalRange()
    {
        var low = PartnerCandidateProfileRules.ResolveEducationLevel(
            1,
            4,
            intellect: 1,
            roll: 0.5);
        var average = PartnerCandidateProfileRules.ResolveEducationLevel(
            1,
            4,
            intellect: 3,
            roll: 0.5);
        var high = PartnerCandidateProfileRules.ResolveEducationLevel(
            1,
            4,
            intellect: 5,
            roll: 0.5);

        Assert.Equal(1, low);
        Assert.Equal(3, average);
        Assert.Equal(4, high);
    }

    [Fact]
    public void CandidateCareerStandingRewardsEducationAndAbility()
    {
        var weak = PartnerCandidateProfileRules.ResolveDesiredJobLevel(
            age: 30,
            educationLevel: 0,
            strength: 1,
            intellect: 1,
            roll: 0.70);
        var established = PartnerCandidateProfileRules.ResolveDesiredJobLevel(
            age: 30,
            educationLevel: 4,
            strength: 4,
            intellect: 4,
            roll: 0.70);

        Assert.True(established >= weak);
        Assert.InRange(weak, 0, 2);
        Assert.InRange(established, 1, 3);
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
            householdWealth: 40_000m,
            housesOwned: 2);

        Assert.Equal(90, withoutResources, 10);
        Assert.Equal(100, established, 10);
        Assert.True(established > withoutResources);
    }

    [Fact]
    public void MalePartnerValueRewardsFarmlandOwnership()
    {
        var traits = new[] { 3, 3, 3, 3, 3, 3 };

        var landless = PartnerSearchRules.CalculatePartnerValue(
            Sex.Male,
            30,
            traits,
            2,
            1);
        var landed = PartnerSearchRules.CalculatePartnerValue(
            Sex.Male,
            30,
            traits,
            2,
            1,
            farmlandOwned: 2);

        Assert.Equal(5, landed - landless, 10);
        Assert.True(landed > landless);
    }


    [Fact]
    public void HusbandOriginNeverReturnsHistoricalHomeOutsideCurrentDestinationPool()
    {
        var historicalHome = new TownInfo(
            "Lwów",
            "Lwów",
            24.0316,
            49.8429,
            312000)
        {
            Id = "p_old",
            IsDestinationAvailable = false
        };
        var currentTowns = new[]
        {
            new TownInfo("Rzeszów", "Rzeszów", 22.0047, 50.0412, 50000)
            {
                Id = "p_current_1",
                IsDestinationAvailable = true
            },
            new TownInfo("Kraków", "Kraków", 19.9450, 50.0647, 300000)
            {
                Id = "p_current_2",
                IsDestinationAvailable = true
            }
        };

        var selected = HusbandOriginSelector.Choose(
            historicalHome,
            currentTowns,
            1950,
            new ZeroRandom());

        Assert.DoesNotContain(currentTowns, town => town.Id == historicalHome.Id);
        Assert.Contains(currentTowns, town => town.Id == selected.Id);
        Assert.True(selected.IsDestinationAvailable);
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

    private sealed class ZeroRandom : IGameRandom
    {
        public int NextInt(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0.0;
        public bool Chance(double probability) => probability > 0;
    }
}
