using Dynastia.Contracts;
using Dynastia.Mechanics.Childhood;
using Dynastia.Mechanics.Community;
using Dynastia.Mechanics.Farming;

namespace Dynastia.Core.Tests;

public sealed class BalancePackages5To8Tests
{
    [Theory]
    [InlineData(9, 0)]
    [InlineData(10, 0.25)]
    [InlineData(13, 0.25)]
    [InlineData(14, 0.50)]
    [InlineData(17, 0.50)]
    [InlineData(18, 1.00)]
    [InlineData(75, 1.00)]
    public void FarmAgeContributionUsesConfiguredAgeBands(int age, double expected)
    {
        var catalog = FarmingWorkerContributionCatalog.Load(new RepositoryDataService(RepositoryRoot()));
        Assert.Equal((decimal)expected, catalog.GetAgeContribution(age));
    }

    [Fact]
    public void FarmAgeContributionRejectsGapsOverlapAndNegativeMultipliers()
    {
        Assert.Throws<InvalidDataException>(() => FarmingWorkerContributionCatalog.Validate(
        [
            new FarmingWorkerAgeBand(10, 13, 0.25m),
            new FarmingWorkerAgeBand(15, 17, 0.50m),
            new FarmingWorkerAgeBand(18, null, 1m)
        ]));

        Assert.Throws<InvalidDataException>(() => FarmingWorkerContributionCatalog.Validate(
        [
            new FarmingWorkerAgeBand(10, 14, 0.25m),
            new FarmingWorkerAgeBand(14, 17, 0.50m),
            new FarmingWorkerAgeBand(18, null, 1m)
        ]));

        Assert.Throws<InvalidDataException>(() => FarmingWorkerContributionCatalog.Validate(
        [
            new FarmingWorkerAgeBand(10, 13, -0.25m),
            new FarmingWorkerAgeBand(14, 17, 0.50m),
            new FarmingWorkerAgeBand(18, null, 1m)
        ]));
    }

    [Fact]
    public void FarmingRanksScarceWorkersByExpectedContributionAndUsesAgeInBothIncomePaths()
    {
        var farming = Read("plugins", "Dynastia.Mechanics.Farming", "StandardFarmingService.cs");
        Assert.Contains(".OrderByDescending(GetExpectedProductiveContribution)", farming);
        Assert.Contains(".ThenByDescending(worker => worker.Age)", farming);
        Assert.Contains(".ThenBy(worker => worker.Id)", farming);
        Assert.Contains("workerBaseIncome * ageContribution", farming);
        Assert.Contains("workerBaseIncome * adjustedMultiplier * ageContribution", farming);
        Assert.Contains("AnnualProductiveEffortRules.Get", farming);
    }

    [Fact]
    public void AcquaintanceRulesUseStrongerImprovementAndTwoYearMeaningfulInteractionGrace()
    {
        var rules = CommunityConnectionRules.Load(new RepositoryDataService(RepositoryRoot()));
        Assert.Equal(6, rules.ImproveFamiliarityGain);
        Assert.Equal(3, rules.ImproveSympathyGain);
        Assert.Equal(2, rules.MeaningfulInteractionDecayGraceYears);

        var component = Read("plugins", "Dynastia.Mechanics.Community", "CommunityStateComponents.cs");
        var service = Read("plugins", "Dynastia.Mechanics.Community", "CommunityConnectionService.cs");
        Assert.Contains("LastMeaningfulInteractionYear", component);
        Assert.Contains("MarkMeaningfulInteraction(connection);", service);
        Assert.Contains("connection.LastMeaningfulInteractionYear ??= _gameState.Year", service);
        Assert.Contains("!HasMeaningfulInteractionGrace(connection)", service);
        Assert.Contains("_gameState.Year <= interactionYear + _rules.MeaningfulInteractionDecayGraceYears", service);
    }

    [Fact]
    public void ChildhoodStableCareRulesLoadAndTraumaBlockIsPersisted()
    {
        var rules = ChildhoodBalanceRules.Load(new RepositoryDataService(RepositoryRoot()));
        Assert.Equal(0.25, rules.StableCareRecoveryChance, 10);
        Assert.Equal(75d, rules.StableCareHealthMinimum, 10);
        Assert.Equal(3, rules.StableCareTarget);
        Assert.Equal(2, rules.MajorTraumaRecoveryBlockYears);

        var component = Read("plugins", "Dynastia.Mechanics.Childhood", "ChildHappinessComponent.cs");
        var system = Read("plugins", "Dynastia.Mechanics.Childhood", "ChildHappinessYearSystem.cs");
        var plugin = Read("plugins", "Dynastia.Mechanics.Childhood", "ChildhoodPlugin.cs");
        Assert.Contains("RecoveryBlockedThroughYear", component);
        Assert.Contains("finance.BasicNeedsShortfall > 0m", system);
        Assert.Contains("householdStatus.IsLargeFamilyStrained", system);
        Assert.Contains("householdStatus.IsOvercrowded", system);
        Assert.Contains("HasAvailableResidentCaregiver", system);
        Assert.Contains("gameState.Year <= state.RecoveryBlockedThroughYear", system);
        Assert.Contains("health.serious_illness", plugin);
        Assert.Contains("life.death", plugin);
        Assert.Contains("BlockStableCareRecovery", plugin);
        Assert.Contains("SharesHousehold(parent, child, economy)", plugin);
    }

    [Fact]
    public void OccupationalCurvePreservesMeanAndCompressesTailForEveryMastery()
    {
        var expectedMaximums = new[] { 20m, 20.3045m, 21.0555m, 22.6387m, 26.6615m };

        for (var mastery = 1; mastery <= 5; mastery++)
        {
            decimal legacyTotal = 0m;
            decimal compressedTotal = 0m;
            decimal previous = 0m;
            for (var roll = 0; roll <= 94; roll++)
            {
                var legacy = OccupationalIncomeCurve.GetLegacyMultiplier(roll, mastery);
                var compressed = OccupationalIncomeCurve.GetMultiplier(roll, mastery);
                Assert.True(compressed > 0m);
                Assert.True(compressed >= previous);
                previous = compressed;
                legacyTotal += legacy;
                compressedTotal += compressed;
            }

            Assert.InRange(Math.Abs(legacyTotal / 95m - compressedTotal / 95m), 0m, 0.0000000001m);
            Assert.InRange(
                Math.Abs(OccupationalIncomeCurve.GetMaximumMultiplier(mastery) - expectedMaximums[mastery - 1]),
                0m,
                0.0001m);
        }

        Assert.Equal(20m, OccupationalIncomeCurve.GetMaximumMultiplier(1));
        Assert.True(OccupationalIncomeCurve.GetMaximumMultiplier(5) < 27m);
        Assert.True(OccupationalIncomeCurve.GetExpectedMultiplier(5)
            > OccupationalIncomeCurve.GetExpectedMultiplier(4));
    }

    [Fact]
    public void OccupationalCurveSupportsConfiguredRollRangesWithoutChangingRngContract()
    {
        const int minimum = 5;
        const int maximum = 20;
        decimal legacyTotal = 0m;
        decimal compressedTotal = 0m;
        for (var roll = minimum; roll <= maximum; roll++)
        {
            legacyTotal += OccupationalIncomeCurve.GetLegacyMultiplier(roll, 5, minimum, maximum);
            compressedTotal += OccupationalIncomeCurve.GetMultiplier(roll, 5, minimum, maximum);
        }
        Assert.InRange(Math.Abs(legacyTotal - compressedTotal), 0m, 0.0000000001m);

        var craft = Read("plugins", "Dynastia.Mechanics.Crafts", "StandardCraftService.cs");
        var crime = Read("plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationService.cs");
        Assert.Contains("_random.NextInt(0, 94)", craft);
        Assert.Contains("_random.NextInt(", crime);
        Assert.Contains("IncomeRollMinimum", crime);
        Assert.Contains("IncomeRollMaximumInclusive", crime);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

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

    private sealed class RepositoryDataService(string root) : IGameDataService
    {
        public IReadOnlyList<string> GetStringList(string relativePath) =>
            File.ReadAllLines(Path.Combine(root, "data", relativePath));

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(string relativePath) =>
            throw new NotSupportedException();

        public string ReadText(string relativePath) =>
            File.ReadAllText(Path.Combine(root, "data", relativePath));
    }
}
