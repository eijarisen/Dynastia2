using System.Globalization;
using Dynastia.Contracts;
using Dynastia.Mechanics.Inheritance;

namespace Dynastia.Core.Tests;

public sealed class EstateInheritanceCharacterizationTests
{
    [Theory]
    [InlineData(10, 4, 3, 3)]
    [InlineData(-10, -4, -3, -3)]
    [InlineData(2, 1, 1, 0)]
    [InlineData(-2, -1, -1, 0)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(10001, 3334, 3334, 3333)]
    [InlineData(-10001, -3334, -3334, -3333)]
    [InlineData(100003, 33335, 33334, 33334)]
    [InlineData(-100003, -33335, -33334, -33334)]
    public void SignedWholeCurrencyIsConservedWithEldestFirstRemaindersAndNoRepeatSettlement(
        int amount, int eldestShare, int middleShare, int youngestShare)
    {
        using var f = new RefactorFixture();
        var source = f.Person(80, alive: false);
        f.Household(source, amount);
        // Deliberately create and link heirs out of birth order.
        var youngest = f.Person(20);
        var eldest = f.Person(40);
        var middle = f.Person(30);
        foreach (var heir in new[] { youngest, eldest, middle })
        {
            f.Household(heir);
            f.Family.SetParents(heir, source, null);
        }
        Settle(f, source);
        var heirs = new[] { eldest, middle, youngest };
        var expected = new decimal[] { eldestShare, middleShare, youngestShare };
        Assert.Equal(expected, heirs.Select(heir => f.Economy.GetHousehold(heir)!.Wealth));
        Assert.Equal((decimal)amount, heirs.Sum(heir => f.Economy.GetHousehold(heir)!.Wealth));
        Assert.All(heirs, heir => Assert.Equal(0m, f.Economy.GetPendingInheritance(heir)));
        var receipts = f.Events.AllEvents.Where(e => e.Type is "inheritance.received" or "inheritance.debt_received").ToArray();
        Assert.Equal(expected.Count(value => value != 0), receipts.Length);
        Assert.Equal((decimal)amount, receipts.Sum(e => decimal.Parse(e.Data["amount"], CultureInfo.InvariantCulture)));
        Assert.Equal(heirs.Where((_, index) => expected[index] != 0).Select(p => (Guid?)p.Id), receipts.Select(e => e.SubjectId));
        Assert.All(receipts, e => Assert.Equal(amount > 0 ? "inheritance.received" : "inheritance.debt_received", e.Type));
        var summary = Assert.Single(f.Events.AllEvents.Where(e => e.Type == "inheritance.estate_settled"));
        Assert.Equal(source.Id, summary.SubjectId);
        Assert.Equal(heirs.Select(p => p.Id), summary.RelatedPersonIds);
        Assert.Equal(f.State.Year, summary.Year);
        Assert.False(f.Economy.HasHousehold(source));
        var count = f.Events.AllEvents.Count;
        System(f).Execute(f.State);
        Assert.Equal(count, f.Events.AllEvents.Count);
    }

    [Theory]
    [InlineData(125, 12, false)]
    [InlineData(-125, 12, false)]
    [InlineData(125, 25, false)]
    [InlineData(-125, 25, false)]
    [InlineData(125, 25, true)]
    [InlineData(-125, 25, true)]
    public void HeirWithoutAnEstablishedOutsideHouseholdReceivesSignedPendingBalance(
        int amount, int age, bool memberOfEstate)
    {
        using var f = new RefactorFixture();
        var source = f.Person(80, alive: false);
        var heir = f.Person(age);
        f.Household(source, amount);
        f.Family.SetParents(heir, source, null);
        if (memberOfEstate) f.Economy.AddHouseholdMember(source, heir);
        f.Economy.SetPendingInheritance(heir, -7m);
        Settle(f, source);
        Assert.Equal(amount - 7m, f.Economy.GetPendingInheritance(heir));
        Assert.False(f.Economy.HasHousehold(heir));
        var receipt = Assert.Single(f.Events.AllEvents.Where(e => e.Type ==
            (amount > 0 ? "inheritance.pending" : "inheritance.debt_pending")));
        Assert.Equal(heir.Id, receipt.SubjectId);
        Assert.Equal((decimal)amount, decimal.Parse(receipt.Data["amount"], CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(17)]
    [InlineData(-17)]
    public void MarriedDaughtersShareChangesHerActualHouseholdRatherThanHerFathers(int amount)
    {
        using var f = new RefactorFixture();
        var source = f.Person(80, alive: false);
        var daughter = f.Person(30, sex: Sex.Female);
        var husband = f.Person(32);
        f.Household(source, amount);
        f.Household(husband, 5m);
        f.Economy.AddHouseholdMember(husband, daughter);
        f.Family.SetParents(daughter, source, null);
        f.Family.SetSpouses(daughter, husband, f.State.Year - 5);
        Settle(f, source);
        Assert.Equal(5m + amount, f.Economy.GetHousehold(husband)!.Wealth);
        Assert.Equal(0m, f.Economy.GetPendingInheritance(daughter));
    }

    [Fact]
    public void DesignatedDefaultAndStaleAssetAssignmentsConserveIdentityAndPendingOwnership()
    {
        using var f = new RefactorFixture();
        var source = f.Person(80, alive: false);
        var eldest = f.Person(40);
        var minor = f.Person(12);
        var deceasedChild = f.Person(35);
        f.Household(source, 30m);
        f.Household(eldest);
        foreach (var child in new[] { minor, deceasedChild, eldest }) f.Family.SetParents(child, source, null);
        var assets = new[] { AddAssets(f, source), AddAssets(f, source), AddAssets(f, source) };
        Designate(f, source, assets[0], minor);
        Designate(f, source, assets[2], deceasedChild);
        deceasedChild.Tags.Remove("state.alive");
        deceasedChild.Tags.Add("state.dead");
        Settle(f, source);

        var establishedHouses = f.Economy.GetHouses(eldest);
        var pendingHouses = f.Economy.TakePendingHouses(minor);
        var establishedFarms = f.Economy.GetFarmland(eldest);
        var pendingFarms = f.Economy.GetPendingFarmland(minor);
        var establishedItems = f.Heirlooms.GetHeirlooms(eldest);
        var pendingItems = f.Heirlooms.GetPending(minor);
        Assert.Equal(assets[1].House.Id, Assert.Single(establishedHouses).Id);
        Assert.Equal(assets[1].Farm.Id, Assert.Single(establishedFarms).Id);
        Assert.Equal(assets[1].Item.Id, Assert.Single(establishedItems).Id);
        Assert.Equal(new[] { assets[0].House.Id, assets[2].House.Id }, pendingHouses.Select(a => a.Id));
        Assert.Equal(new[] { assets[0].Farm.Id, assets[2].Farm.Id }, pendingFarms.Select(a => a.Id));
        Assert.Equal(new[] { assets[0].Item.Id, assets[2].Item.Id }, pendingItems.Select(a => a.Id));
        Assert.All(establishedHouses.Concat(pendingHouses), a =>
        {
            Assert.Null(a.AssignedHeirId);
            Assert.Equal(60000m, a.PurchasePrice);
            Assert.Equal(2, a.CapacityExtensions);
        });
        Assert.All(establishedFarms.Concat(pendingFarms), a =>
        {
            Assert.Null(a.AssignedHeirId);
            Assert.Equal(f.State.Year, a.AcquiredYear);
            Assert.Equal("inheritance", a.AcquisitionSource);
            Assert.Equal("orchard", a.FarmTypeId);
            Assert.Equal("cattle", a.LivestockTypeId);
        });
        Assert.All(establishedItems.Concat(pendingItems), a =>
        {
            Assert.Null(a.AssignedHeirId);
            Assert.Equal(700m, a.AppraisedValue);
            Assert.Equal(source.Id, a.OriginPersonId);
            Assert.Equal(source.Id, a.RoyaltyAuthorId);
            Assert.Equal(0.05m, a.RoyaltyAnnualRate);
            Assert.Equal(f.State.Year, a.OwnershipHistory[^1].Year);
        });
        Assert.All(pendingItems, a => Assert.Equal("inherited_pending", a.OwnershipHistory[^1].Reason));
        Assert.Equal("inherited", establishedItems[0].OwnershipHistory[^1].Reason);
        Assert.Equal(15m, f.Economy.GetHousehold(eldest)!.Wealth);
        Assert.Equal(15m, f.Economy.GetPendingInheritance(minor));
        Assert.Equal(2, f.Events.AllEvents.Count(e => e.Type == "farmland.inherited"));
        Assert.Equal(2, f.Events.AllEvents.Count(e => e.Type == "heirloom.pending"));
        Assert.Contains(f.Events.AllEvents, e => e.Type == "inheritance.pending_houses" && e.SubjectId == minor.Id);
        Assert.DoesNotContain(f.Events.AllEvents, e => e.SubjectId == deceasedChild.Id);
    }

    [Theory]
    [InlineData(83, true)]
    [InlineData(-83, true)]
    [InlineData(83, false)]
    [InlineData(-83, false)]
    public void ResidualEstateFollowsLivingAnchorWithoutAnInheritanceEvent(int amount, bool established)
    {
        using var f = new RefactorFixture();
        var oldHead = f.Person(60, alive: false);
        var anchor = f.Person(35);
        f.Household(oldHead, amount, anchor);
        if (established) f.Household(anchor);
        else f.Economy.AddHouseholdMember(oldHead, anchor);
        var assets = AddAssets(f, oldHead);
        Settle(f, oldHead);
        Assert.Equal((decimal)amount, established
            ? f.Economy.GetHousehold(anchor)!.Wealth : f.Economy.GetPendingInheritance(anchor));
        Assert.Equal(assets.House.Id, Assert.Single(established
            ? f.Economy.GetHouses(anchor) : f.Economy.TakePendingHouses(anchor)).Id);
        Assert.Equal(assets.Farm.Id, Assert.Single(established
            ? f.Economy.GetFarmland(anchor) : f.Economy.GetPendingFarmland(anchor)).Id);
        var item = Assert.Single(established ? f.Heirlooms.GetHeirlooms(anchor) : f.Heirlooms.GetPending(anchor));
        Assert.Equal(assets.Item.Id, item.Id);
        Assert.Equal(established ? "household_transfer" : "household_transfer_pending", item.OwnershipHistory[^1].Reason);
        Assert.False(f.Economy.HasHousehold(oldHead));
        var news = Assert.Single(f.Events.AllEvents);
        Assert.Equal("household.assets_followed_anchor", news.Type);
        Assert.Equal(anchor.Id, news.SubjectId);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-99)]
    public void EstateWithoutHeirsIsRemovedAndItsSignedBalanceAndAssetsReported(int amount)
    {
        using var f = new RefactorFixture();
        var source = f.Person(80, alive: false);
        f.Household(source, amount);
        AddAssets(f, source);
        Settle(f, source);
        Assert.False(f.Economy.HasHousehold(source));
        Assert.Empty(f.Heirlooms.GetHeirlooms(source));
        var news = Assert.Single(f.Events.AllEvents);
        Assert.Equal("inheritance.estate_left_dynasty", news.Type);
        Assert.Equal(amount.ToString(CultureInfo.InvariantCulture), news.Data["amount"]);
        foreach (var key in new[] { "houses", "farmland", "heirlooms" }) Assert.Equal("1", news.Data[key]);
        System(f).Execute(f.State);
        Assert.Single(f.Events.AllEvents);
    }

    [Theory]
    [InlineData(125)]
    [InlineData(-125)]
    public void PendingInheritanceIsClaimedOnlyAfterAdulthoodAndEstablishingAHousehold(int amount)
    {
        using var f = new RefactorFixture();
        var source = f.Person(80, alive: false);
        var minor = f.Person(17);
        f.Household(source, amount);
        f.Family.SetParents(minor, source, null);
        var assets = AddAssets(f, source);
        Settle(f, source);
        var claims = new AdulthoodInheritanceSystem(f.Family, f.Economy, f.Heirlooms, f.Events);
        claims.Execute(f.State);
        Assert.Equal((decimal)amount, f.Economy.GetPendingInheritance(minor));
        minor.Age = 18;
        claims.Execute(f.State);
        Assert.Equal((decimal)amount, f.Economy.GetPendingInheritance(minor));
        f.Household(minor, 5m);
        claims.Execute(f.State);
        Assert.Equal(5m + amount, f.Economy.GetHousehold(minor)!.Wealth);
        Assert.Equal(0m, f.Economy.GetPendingInheritance(minor));
        Assert.Equal(assets.House.Id, Assert.Single(f.Economy.GetHouses(minor)).Id);
        Assert.Equal(assets.Farm.Id, Assert.Single(f.Economy.GetFarmland(minor)).Id);
        var item = Assert.Single(f.Heirlooms.GetHeirlooms(minor));
        Assert.Equal(assets.Item.Id, item.Id);
        Assert.Equal("inherited", item.OwnershipHistory[^1].Reason);
        Assert.Empty(f.Economy.TakePendingHouses(minor));
        Assert.Empty(f.Economy.GetPendingFarmland(minor));
        Assert.Empty(f.Heirlooms.GetPending(minor));
        var receipt = Assert.Single(f.Events.AllEvents.Where(e => e.Type == "inheritance.received_at_household"));
        Assert.Equal(minor.Id, receipt.SubjectId);
        Assert.Contains(amount < 0 ? "inherited debt" : "inheritance", receipt.Data["text"]);
        var count = f.Events.AllEvents.Count;
        claims.Execute(f.State);
        Assert.Equal(count, f.Events.AllEvents.Count);
        Assert.Equal(5m + amount, f.Economy.GetHousehold(minor)!.Wealth);
    }

    [Theory]
    [InlineData(false, 0, null)]
    [InlineData(true, 0, "skipped")]
    [InlineData(true, 49, "heavy")]
    [InlineData(true, 50, null)]
    public void DesignationFalloutUsesAssetValuesAndStrictHalfShareThreshold(
        bool designated, int lesserValue, string? severity)
    {
        using var f = new RefactorFixture();
        var source = f.Person(80, alive: false);
        var favored = f.Person(40);
        var other = f.Person(30);
        f.Household(source);
        f.Household(favored);
        f.Household(other);
        f.Family.SetParents(favored, source, null);
        f.Family.SetParents(other, source, null);
        var first = new HeirloomAssetInfo(f.NextId(), "fixture", "📚", "Book", 100m, 1890,
            source.Id, "fixture", "fixture", "Family work", null, false, []);
        f.Heirlooms.AddExisting(source, first, 1890, source.Id, "fixture");
        if (designated) Assert.True(f.Heirlooms.SetInheritanceHeir(source, first.Id, favored.Id));
        if (lesserValue > 0)
        {
            var second = first with { Id = f.NextId(), AppraisedValue = lesserValue };
            f.Heirlooms.AddExisting(source, second, 1890, source.Id, "fixture");
            Assert.True(f.Heirlooms.SetInheritanceHeir(source, second.Id, other.Id));
        }
        Settle(f, source);
        var fallout = f.Events.AllEvents.Where(e => e.Type == "inheritance.disadvantaged").ToArray();
        if (severity is null)
        {
            Assert.Empty(fallout);
            return;
        }
        var news = Assert.Single(fallout);
        Assert.Equal(other.Id, news.SubjectId);
        Assert.Equal(new[] { source.Id, favored.Id }, news.RelatedPersonIds);
        Assert.Equal(severity, news.Data["severity"]);
        Assert.Equal(lesserValue.ToString(CultureInfo.InvariantCulture), news.Data["receivedAssetValue"]);
        Assert.Equal("100", news.Data["favoredAssetValue"]);
        Assert.Equal(favored.Id.ToString(), news.Data["favoredHeirIds"]);
        Assert.Equal("true", news.Data["suppressChronicle"]);
    }

    private static EstateInheritanceSystem System(RefactorFixture f) => new(f.Family, f.Economy, f.Heirlooms, f.Events);

    private static void Settle(RefactorFixture f, IPerson source)
    {
        f.Economy.MarkEstateReady(source);
        System(f).Execute(f.State);
        f.Random.AssertComplete();
    }

    private sealed record Assets(HousePropertyInfo House, FarmlandAssetInfo Farm, HeirloomAssetInfo Item);

    private static Assets AddAssets(RefactorFixture f, IPerson owner)
    {
        var house = new HousePropertyInfo(f.NextId(), f.Town, false, true,
            PurchasePrice: 60000m, CapacityExtensions: 2);
        var farm = new FarmlandAssetInfo(f.NextId(), f.Town, 1890, "purchase",
            FarmTypeId: "orchard", LivestockTypeId: "cattle");
        var item = new HeirloomAssetInfo(f.NextId(), "fixture", "📚", "Family book", 700m, 1890,
            owner.Id, "fixture", "fixture", "Family work", null, false, [], owner.Id, 0.05m);
        f.Economy.AddExistingHouse(owner, house);
        f.Economy.AddExistingFarmland(owner, farm);
        f.Heirlooms.AddExisting(owner, item, 1890, owner.Id, "fixture");
        return new Assets(house, farm, item);
    }

    private static void Designate(RefactorFixture f, IPerson owner, Assets assets, IPerson heir)
    {
        Assert.True(f.Economy.SetHouseInheritanceHeir(owner, assets.House.Id, heir.Id));
        Assert.True(f.Economy.SetFarmlandInheritanceHeir(owner, assets.Farm.Id, heir.Id));
        Assert.True(f.Heirlooms.SetInheritanceHeir(owner, assets.Item.Id, heir.Id));
    }
}
