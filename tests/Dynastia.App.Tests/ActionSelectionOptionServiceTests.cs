using Dynastia.App.ViewModels;
using Dynastia.App.ViewModels.Actions;
using Dynastia.Contracts;
using static Dynastia.App.Tests.ActionOptionTestServices;

namespace Dynastia.App.Tests;

public sealed class ActionSelectionOptionServiceTests
{
    [Theory]
    [InlineData("household.buy_house", "townId", "40000")]
    [InlineData("household.sell_house", "propertyId", "48000")]
    [InlineData("household.extend_house", "propertyId", "10000")]
    public void HouseSelectionRetainsExactSummaryParametersAndActiveHouseholdTarget(string id, string key, string price)
    {
        using var culture = new CultureScope("fr-FR");
        using var f = new ActionCoordinatorFixture();
        f.Selected = f.Source.Person(24, "Other resident");
        var house = new HousePropertyInfo(Guid.NewGuid(), f.Source.Economy.Residence, false, true,
            PurchasePrice: 40000m, CapacityExtensions: 2);
        f.Source.Economy.Houses.Add(house);
        f.Source.Register(id);
        var selectedId = key == "townId" ? house.Town.Id : house.Id.ToString();
        f.Panel.QueueActionWithSelection(id, selectedId);
        var submitted = Assert.Single(f.Source.Registry.Submissions);
        Assert.Equal(f.Source.Head.Id, submitted.TargetId);
        Assert.Equal(3, submitted.Parameters!.Count);
        Assert.Equal(selectedId, submitted.Parameters[key]);
        Assert.Equal("Z Residence", submitted.Parameters["summaryTown"]);
        Assert.Equal(price, submitted.Parameters["summaryPrice"]);
        Assert.True(Assert.Single(f.Results).FromSelection);
    }

    [Theory]
    [InlineData("farming.sell_farmland", "12345")]
    [InlineData("farming.add_livestock", "5000")]
    public void FarmlandSelectionStoresParcelFlavorAndExactPrice(string id, string price)
    {
        using var farming = new FixtureWithFarming();
        var f = farming.Fixture;
        var parcel = new FarmlandAssetInfo(Guid.NewGuid(), f.Source.Economy.Residence, 1890, "purchase",
            FarmTypeDisplayName: "Orchard", FarmTypeEmoji: "🍎");
        farming.Service.Parcels.Add(parcel);
        f.Source.Register(id);
        f.Panel.QueueActionWithSelection(id, parcel.Id.ToString());
        var submitted = Assert.Single(f.Source.Registry.Submissions);
        Assert.Equal(f.Source.Head.Id, submitted.TargetId);
        Assert.Equal(3, submitted.Parameters!.Count);
        Assert.Equal(parcel.Id.ToString(), submitted.Parameters["farmlandId"]);
        Assert.Equal("🍎 Orchard — Z Residence", submitted.Parameters["summaryFarmland"]);
        Assert.Equal(price, submitted.Parameters["summaryPrice"]);
    }

    [Fact]
    public void HeirloomSelectionPreservesNameAndInvariantSaleAmount()
    {
        using var culture = new CultureScope("fr-FR");
        var heirlooms = new Heirlooms();
        using var f = new ActionCoordinatorFixture(heirlooms: heirlooms);
        var asset = new HeirloomAssetInfo(Guid.NewGuid(), "template", "💍", "Grandfather's ring", 2000m,
            1890, null, "test", "test", "Origin", null, false, []);
        heirlooms.Assets.Add(asset);
        f.Source.Register("heirloom.sell");
        f.Panel.QueueActionWithSelection("heirloom.sell", asset.Id.ToString());
        var submitted = Assert.Single(f.Source.Registry.Submissions);
        Assert.Equal(3, submitted.Parameters!.Count);
        Assert.Equal(asset.Id.ToString(), submitted.Parameters["heirloomId"]);
        Assert.Equal("Grandfather's ring", submitted.Parameters["summaryHeirloom"]);
        Assert.Equal("1234.5", submitted.Parameters["summaryPrice"]);
    }

    [Theory]
    [InlineData(-100, 0)]
    [InlineData(999, 0)]
    [InlineData(1000, 1000)]
    [InlineData(1999, 1000)]
    [InlineData(9999, 9000)]
    [InlineData(10000, 10000)]
    [InlineData(25000, 10000)]
    public void LendingMaximumKeepsThousandFloorAndBoundsWhileBorrowingKeepsItsCeiling(int wealth, int expected)
    {
        using var f = new ActionCoordinatorFixture();
        f.Source.Economy.Wealth = wealth;
        Assert.Equal((decimal)expected, f.Options.GetMaximumLoanPrincipal("LOAN.GIVE"));
        Assert.Equal(10000m, f.Options.GetMaximumLoanPrincipal("loan.take"));
    }

    [Fact]
    public void LoanOptionsForwardTheHouseholdAndBankOfferWithoutChangingTerms()
    {
        var loans = new Loans();
        using var f = new ActionCoordinatorFixture(loans: loans);
        f.Selected = f.Source.Person(21, "Resident");
        Assert.Empty(f.Options.GetLoanOffers(true, 999m));
        Assert.Empty(loans.OfferRequests);
        var offer = Assert.Single(f.Options.GetLoanOffers(true, 7000m));
        Assert.Same(loans.Offer, offer);
        Assert.Equal((f.Source.Head.Id, true, 7000m), Assert.Single(loans.OfferRequests));
        Assert.Same(loans.Offer.Terms, f.Options.GetLoanTerms(5000m, 2));
        Assert.Equal((5000m, 2, 1m), Assert.Single(loans.TermRequests));
        Assert.Null(f.Options.GetLoanTerms(5000m, 0));
        Assert.Empty(f.Source.Registry.Submissions);
    }

    [Theory]
    [InlineData("loan.take")]
    [InlineData("loan.give")]
    public void LoanSubmissionPreservesEveryCounterpartyParameterAndAlwaysUsesTheActiveHousehold(string id)
    {
        using var culture = new CultureScope("fr-FR");
        using var f = new ActionCoordinatorFixture();
        f.Selected = null; // loan inventory does not require an inspected family member
        f.Source.Register(id);
        f.Panel.QueueLoanAction(id, new(5000m, 2, "Banker", "bank-town", "polish", 0.85m));
        var submitted = Assert.Single(f.Source.Registry.Submissions);
        Assert.Equal(f.Source.Head.Id, submitted.ActorId);
        Assert.Equal(f.Source.Head.Id, submitted.TargetId);
        Assert.Equal(6, submitted.Parameters!.Count);
        Assert.Equal("5000", submitted.Parameters["principal"]);
        Assert.Equal("2", submitted.Parameters["durationYears"]);
        Assert.Equal("0.85", submitted.Parameters["interestMultiplier"]);
        Assert.Equal("Banker", submitted.Parameters["counterpartyName"]);
        Assert.Equal("bank-town", submitted.Parameters["counterpartyTownId"]);
        Assert.Equal("polish", submitted.Parameters["counterpartyNationalityId"]);
        Assert.True(Assert.Single(f.Results).FromSelection);
    }

    [Fact]
    public void MissingOptionalServicesKeepEmptyChoicesAndTermsRatherThanThrowing()
    {
        using var f = new ActionCoordinatorFixture();
        var options = new ActionSelectionOptionService(f.Source.Succession, f.Source.Registry,
            null, null, null, null, null, null, null);
        Assert.Empty(options.GetPropertySelectionOptions("household.buy_house"));
        Assert.Empty(options.GetLoanOffers(false, 5000m));
        Assert.Null(options.GetLoanTerms(5000m, 2));
        Assert.Equal(0m, options.GetMaximumLoanPrincipal("loan.give"));
        Assert.Empty(f.Source.Registry.Submissions);
    }

    private sealed class FixtureWithFarming : IDisposable
    {
        public FixtureWithFarming() => Fixture = new ActionCoordinatorFixture(farming: Service);
        public Farming Service { get; } = new();
        public ActionCoordinatorFixture Fixture { get; }
        public void Dispose() => Fixture.Dispose();
    }
}
