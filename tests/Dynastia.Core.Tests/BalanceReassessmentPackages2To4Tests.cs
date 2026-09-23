using Dynastia.Contracts;
using Dynastia.Mechanics.Community;
using Dynastia.Mechanics.Loans;

namespace Dynastia.Core.Tests;

public sealed class BalanceReassessmentPackages2To4Tests
{
    [Fact]
    public void ExternalLendingOffersScaleInterestByHalfWithoutChangingGenericLoanCurve()
    {
        var oldLendingTerms = LoanTermsCalculator.Calculate(10000m, 10, 1.20m);
        var newLendingTerms = LoanTermsCalculator.Calculate(10000m, 10, 1.20m * 0.50m);

        Assert.Equal(oldLendingTerms.TotalInterestRate * 0.50m, newLendingTerms.TotalInterestRate);
        Assert.Equal(1.20m, oldLendingTerms.InterestMultiplier);
        Assert.Equal(0.60m, newLendingTerms.InterestMultiplier);

        var service = Read("plugins", "Dynastia.Mechanics.Loans", "StandardLoanService.cs");
        var calculator = Read("plugins", "Dynastia.Mechanics.Loans", "LoanTermsCalculator.cs");
        Assert.Contains("ExternalLendingInterestScale = 0.50m", service);
        Assert.Contains("if (isGivingLoan)\n                interestMultiplier *= ExternalLendingInterestScale;", service);
        Assert.DoesNotContain("ExternalLendingInterestScale", calculator);
        Assert.Contains("terms.InterestMultiplier", Read("plugins", "Dynastia.Mechanics.Loans", "LoansPlugin.cs"));
    }

    [Fact]
    public void MoneyRequestGateUsesWarmRelationAndEstablishedHouseholdRenownBoundary()
    {
        var rules = CommunityConnectionRules.Load(new RepositoryDataService(RepositoryRoot()));
        Assert.Equal("Warm", rules.MoneyMinimumRelation);
        Assert.Equal(30d, rules.MoneyMinimumHouseholdRenown);

        (string Relation, double Renown, bool Expected)[] cases =
        [
            ("Neutral", 29, false),
            ("Neutral", 30, false),
            ("Warm", 29, false),
            ("Warm", 30, true),
            ("Warm", 49, true),
            ("Close", 30, true),
            ("Close", 100, true)
        ];

        foreach (var item in cases)
        {
            Assert.Equal(
                item.Expected,
                CommunityConnectionService.MeetsRequestEligibility(
                    item.Relation,
                    item.Renown,
                    rules.MoneyMinimumRelation,
                    rules.MoneyMinimumHouseholdRenown));
        }
    }

    [Fact]
    public void MajorAssetGateRequiresCloseRelationAndProminentHouseholdRenownBoundary()
    {
        var rules = CommunityConnectionRules.Load(new RepositoryDataService(RepositoryRoot()));
        Assert.Equal("Close", rules.AssetMinimumRelation);
        Assert.Equal(50d, rules.AssetMinimumHouseholdRenown);

        (string Relation, double Renown, bool Expected)[] cases =
        [
            ("Neutral", 100, false),
            ("Warm", 100, false),
            ("Close", 49, false),
            ("Close", 50, true),
            ("Close", 100, true)
        ];

        foreach (var item in cases)
        {
            Assert.Equal(
                item.Expected,
                CommunityConnectionService.MeetsRequestEligibility(
                    item.Relation,
                    item.Renown,
                    rules.AssetMinimumRelation,
                    rules.AssetMinimumHouseholdRenown));
        }
    }

    [Fact]
    public void RequestAvailabilityAndResolutionShareTheSameEligibilityPredicates()
    {
        var service = Read("plugins", "Dynastia.Mechanics.Community", "CommunityConnectionService.cs");
        var actions = Read("plugins", "Dynastia.Mechanics.Community", "CommunityConnectionActions.cs");

        var moneyResolution = Slice(service, "internal bool RequestMoney(", "internal bool RequestHouse(");
        AssertBefore(moneyResolution, "CanRequestMoney(actor, connection", "ApplyRequestBaseCost(connection);");
        AssertBefore(moneyResolution, "CanRequestMoney(actor, connection", "_random.Chance(");

        var houseResolution = Slice(service, "internal bool RequestHouse(", "internal bool RequestFarmland(");
        AssertBefore(houseResolution, "CanRequestMajorAsset(actor, connection", "connection.HasSpareHouse");
        AssertBefore(houseResolution, "CanRequestMajorAsset(actor, connection", "ApplyRequestBaseCost(connection);");
        AssertBefore(houseResolution, "CanRequestMajorAsset(actor, connection", "_random.Chance(");

        var farmlandResolution = Slice(service, "internal bool RequestFarmland(", "internal void AdvanceYear(");
        AssertBefore(farmlandResolution, "CanRequestMajorAsset(actor, connection", "connection.HasSpareFarmland");
        AssertBefore(farmlandResolution, "CanRequestMajorAsset(actor, connection", "ApplyRequestBaseCost(connection);");
        AssertBefore(farmlandResolution, "CanRequestMajorAsset(actor, connection", "_random.Chance(");

        var moneyAvailability = Slice(actions, "private static GameActionDefinition CreateRequestMoney(", "private static GameActionDefinition CreateRequestHouse(");
        AssertBefore(moneyAvailability, "CanRequestMoney(context.Actor, connection", "GetEstimatedMoneyRequestMaximum");

        var houseAvailability = Slice(actions, "private static GameActionDefinition CreateRequestHouse(", "private static GameActionDefinition CreateRequestFarmland(");
        AssertBefore(houseAvailability, "CanRequestMajorAsset(context.Actor, connection", "connection.HasSpareHouse");

        var farmlandAvailability = Slice(actions, "private static GameActionDefinition CreateRequestFarmland(", "private static ActionEvaluationResult EvaluateBase(");
        AssertBefore(farmlandAvailability, "CanRequestMajorAsset(context.Actor, connection", "connection.HasSpareFarmland");
    }

    [Fact]
    public void RequestEligibilityConfigurationRejectsOutOfRangeRenownAndUnsupportedRelation()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "data", "LocalSociety", "connection_rules.json"));

        var badRenown = source.Replace(
            "\"moneyMinimumHouseholdRenown\": 30",
            "\"moneyMinimumHouseholdRenown\": 101",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            CommunityConnectionRules.Load(new InlineDataService(badRenown)));

        var badRelation = source.Replace(
            "\"assetMinimumRelation\": \"Close\"",
            "\"assetMinimumRelation\": \"Neutral\"",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            CommunityConnectionRules.Load(new InlineDataService(badRelation)));
    }

    private static void AssertBefore(string source, string earlier, string later)
    {
        var earlierIndex = source.IndexOf(earlier, StringComparison.Ordinal);
        var laterIndex = source.IndexOf(later, StringComparison.Ordinal);
        Assert.True(earlierIndex >= 0, $"Missing expected text: {earlier}");
        Assert.True(laterIndex >= 0, $"Missing expected text: {later}");
        Assert.True(earlierIndex < laterIndex, $"Expected '{earlier}' before '{later}'.");
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Missing start marker: {start}");
        var endIndex = source.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Missing end marker: {end}");
        return source[startIndex..endIndex];
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

    private sealed class InlineDataService(string connectionRules) : IGameDataService
    {
        public IReadOnlyList<string> GetStringList(string relativePath) =>
            throw new NotSupportedException();

        public IReadOnlyList<WeightedStringEntry> GetWeightedStringList(string relativePath) =>
            throw new NotSupportedException();

        public string ReadText(string relativePath) =>
            relativePath.Equals("LocalSociety/connection_rules.json", StringComparison.OrdinalIgnoreCase)
                ? connectionRules
                : throw new FileNotFoundException(relativePath);
    }
}
