using System.Globalization;
using Dynastia.Contracts;
using Dynastia.Mechanics.Inheritance;

namespace Dynastia.Core.Tests;

public sealed class EstatePlanBuilderTests
{
    [Theory]
    [InlineData("10001", 3334, 3334, 3333)]
    [InlineData("-10001", -3334, -3334, -3333)]
    [InlineData("10000.5", 3334, 3334, 3333)]
    [InlineData("-10000.5", -3334, -3334, -3333)]
    [InlineData("2", 1, 1, 0)]
    [InlineData("-2", -1, -1, 0)]
    [InlineData("0.5", 1, 0, 0)]
    [InlineData("-0.5", -1, 0, 0)]
    [InlineData("0.49", 0, 0, 0)]
    [InlineData("-0.49", 0, 0, 0)]
    [InlineData("0", 0, 0, 0)]
    public void PurePlannerConservesRoundedSignedBalanceInProvidedHeirOrder(
        string balance, int first, int second, int third)
    {
        var wealth = decimal.Parse(balance, CultureInfo.InvariantCulture);
        var input = Snapshot(wealth);
        var plan = new EstatePlanBuilder().Build(input);
        Assert.Equal(EstateSettlementKind.Inheritance, plan.Kind);
        Assert.Equal(new decimal[] { first, second, third }, plan.Cash.Select(item => item.Amount));
        Assert.Equal(Math.Round(wealth, 0, MidpointRounding.AwayFromZero), plan.Cash.Sum(item => item.Amount));
        Assert.Equal(input.Recipients.Select(heir => heir.Id), plan.Cash.Select(item => item.RecipientId));
        Assert.Equal(new[] { false, false, true }, plan.Cash.Select(item => item.IsPending));
        Assert.Equal(plan.Cash, new EstatePlanBuilder().Build(input).Cash);
    }

    [Fact]
    public void DesignationsDoNotConsumeDefaultSlotsAndInvalidDesignationsFallBackForEveryAssetKind()
    {
        var input = Snapshot(0m);
        Guid?[] assignments = [Id(30), null, Id(999), Id(10), Id(998)];
        input = input with
        {
            Houses = assignments.Select((id, index) => new HousePropertyInfo(Id(100 + index), Town, false, true, id)).ToArray(),
            Farmland = assignments.Select((id, index) => new FarmlandAssetInfo(Id(200 + index), Town, 1800, "purchase", id)).ToArray(),
            Heirlooms = assignments.Select((id, index) => Item(Id(300 + index)) with { AssignedHeirId = id }).ToArray(),
            HouseValues = new decimal[assignments.Length]
        };
        var plan = new EstatePlanBuilder().Build(input);
        var expected = new[] { Id(30), Id(10), Id(20), Id(10), Id(30) };
        foreach (var allocations in new[] { plan.Houses, plan.Farmland, plan.Heirlooms })
        {
            Assert.Equal(expected, allocations.Select(item => item.RecipientId));
            Assert.Equal(new[] { true, false, false, false, true }, allocations.Select(item => item.IsPending));
        }
        Assert.Equal(input.Houses.Select(asset => asset.Id), plan.Houses.Select(item => item.AssetId));
        Assert.Equal(input.Farmland.Select(asset => asset.Id), plan.Farmland.Select(item => item.AssetId));
        Assert.Equal(input.Heirlooms.Select(asset => asset.Id), plan.Heirlooms.Select(item => item.AssetId));
        // Input designations are retained for validation/fallout; transfer instructions
        // do not mutate these snapshots or the asset-owning stores.
        Assert.Equal(assignments, plan.Snapshot.Houses.Select(asset => asset.AssignedHeirId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LivingAnchorReceivesUnsplitSignedBalanceAndIsNotAnInheritance(bool pending)
    {
        var input = Snapshot(-10001.5m) with
        {
            IsLivingAnchor = true,
            Recipients = [new(Id(1), "Living Anchor", pending, pending ? null : Id(50))],
            Houses = [new(Id(100), Town, true, false, Id(20))],
            HouseValues = [1000m]
        };
        var plan = new EstatePlanBuilder().Build(input);
        Assert.Equal(EstateSettlementKind.LivingAnchorTransfer, plan.Kind);
        Assert.Equal(new EstateCashAllocation(Id(1), pending, -10001.5m), Assert.Single(plan.Cash));
        Assert.Equal(new EstateAssetAllocation(Id(100), Id(1), pending), Assert.Single(plan.Houses));
        Assert.Empty(plan.Disadvantages);
        var news = EstateEventFactory.Create(plan);
        Assert.Empty(news.RecipientEvents);
        Assert.Equal("household.assets_followed_anchor", news.Summary!.Type);
    }

    [Fact]
    public void NoHeirAndMissingFinanceOutcomesDoNotInventAllocations()
    {
        var lost = new EstatePlanBuilder().Build(Snapshot(-9m) with
        {
            Recipients = [], HeirDescription = "no heirs",
            Houses = [new(Id(100), Town, true, false)], HouseValues = [100m]
        });
        Assert.Equal(EstateSettlementKind.LostEstate, lost.Kind);
        Assert.Empty(lost.Cash);
        Assert.Empty(lost.Houses);
        Assert.Single(lost.Snapshot.Houses);
        Assert.Equal(-9m, lost.Snapshot.Wealth);
        Assert.Equal("inheritance.estate_left_dynasty", EstateEventFactory.Create(lost).Summary!.Type);
        var missing = new EstatePlanBuilder().Build(Snapshot(0m) with { Wealth = null, Recipients = [] });
        Assert.Equal(EstateSettlementKind.MissingFinance, missing.Kind);
        Assert.Empty(missing.Cash);
        Assert.Null(EstateEventFactory.Create(missing).Summary);
    }

    [Fact]
    public void PlanDefensivelyFreezesInputListsAndNestedHeirloomHistory()
    {
        var recipients = Snapshot(1m).Recipients.ToList();
        var houses = new List<HousePropertyInfo> { new(Id(100), Town, true, false, Id(10)) };
        var history = new List<HeirloomOwnershipRecordInfo> { new(1850, Id(60), Id(1), "created") };
        var items = new List<HeirloomAssetInfo> { Item(Id(200)) with { OwnershipHistory = history } };
        var values = new List<decimal> { 100m };
        var plan = new EstatePlanBuilder().Build(Snapshot(1m) with
        {
            Recipients = recipients, Houses = houses, Heirlooms = items, HouseValues = values
        });
        recipients.Clear(); houses.Clear(); items.Clear(); history.Clear(); values.Clear();
        Assert.Equal(3, plan.Snapshot.Recipients.Count);
        Assert.Single(plan.Snapshot.Houses);
        Assert.Single(Assert.Single(plan.Snapshot.Heirlooms).OwnershipHistory);
        Assert.Equal(100m, Assert.Single(plan.Snapshot.HouseValues));
        Assert.Throws<NotSupportedException>(() => ((IList<EstateRecipient>)plan.Snapshot.Recipients).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<EstateCashAllocation>)plan.Cash).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<EstateAssetAllocation>)plan.Houses).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HeirloomOwnershipRecordInfo>)plan.Snapshot.Heirlooms[0].OwnershipHistory).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)plan.Disadvantages[0].FavoredHeirIds).Clear());
    }

    [Fact]
    public void ImpossibleIdentitiesAndOutOfRangeBalancesFailInThePurePlanningPhase()
    {
        var builder = new EstatePlanBuilder();
        var input = Snapshot(1m);
        Assert.Throws<InvalidOperationException>(() => builder.Build(input with
        { Recipients = [input.Recipients[0], input.Recipients[0]] }));
        Assert.Throws<InvalidOperationException>(() => builder.Build(input with
        { Houses = [new(Id(100), Town, true, false), new(Id(100), Town, false, true)], HouseValues = [1m, 1m] }));
        Assert.Throws<InvalidOperationException>(() => builder.Build(input with
        { Farmland = [new(Id(100), Town, 1800, "purchase"), new(Id(100), Town, 1801, "purchase")] }));
        Assert.Throws<InvalidOperationException>(() => builder.Build(input with
        { Heirlooms = [Item(Id(100)), Item(Id(100))] }));
        Assert.Throws<InvalidOperationException>(() => builder.Build(input with
        { Heirlooms = [Item(Guid.Empty)] }));
        Assert.Throws<InvalidOperationException>(() => builder.Build(input with { IsLivingAnchor = true }));
        Assert.Throws<OverflowException>(() => builder.Build(input with { Wealth = decimal.MaxValue }));
        Assert.Throws<OverflowException>(() => builder.Build(input with { Wealth = (decimal)long.MinValue }));
        // Those amounts never entered the dividing algorithm for a lost estate.
        Assert.Equal(EstateSettlementKind.LostEstate, builder.Build(input with
        { Wealth = decimal.MaxValue, Recipients = [] }).Kind);
    }

    [Theory]
    [InlineData(0, "skipped")]
    [InlineData(49, "heavy")]
    [InlineData(50, null)]
    public void DisadvantageUsesStrictHalfThresholdAndAllFavoredIdsInHeirOrder(int lesser, string? severity)
    {
        var input = Snapshot(999999m) with
        {
            Houses = [new(Id(100), Town, false, true, Id(20)), new(Id(101), Town, false, true, Id(10))],
            HouseValues = [100m, 100m],
            Farmland = lesser == 0 ? [] : [new(Id(200), Town, 1800, "purchase", Id(30))],
            FarmlandValue = lesser
        };
        var plan = new EstatePlanBuilder().Build(input);
        if (severity is null) Assert.Empty(plan.Disadvantages);
        else
        {
            var disadvantage = Assert.Single(plan.Disadvantages);
            Assert.Equal(Id(30), disadvantage.RecipientId);
            Assert.Equal(severity, disadvantage.Severity);
            Assert.Equal((decimal)lesser, disadvantage.ReceivedValue);
            Assert.Equal(new[] { Id(10), Id(20) }, disadvantage.FavoredHeirIds);
        }
    }

    private static EstateSnapshot Snapshot(decimal wealth) => new(
        1900, Id(1), Id(2), Id(1), Id(1), "Source Family", false, wealth, "the living children",
        [new(Id(10), "Eldest Family", false, Id(11)), new(Id(20), "Middle Family", false, Id(21)),
         new(Id(30), "Minor Family", true, null)], [], [], [], [], 0m);

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    private static TownInfo Town => new("Testowo", "Test County", 20, 52, 10000) { Id = "test-town" };
    private static HeirloomAssetInfo Item(Guid id) => new(id, "test", "", "Book", 100m, 1850,
        Id(1), "test", "test", "Family work", null, false, []);
}
