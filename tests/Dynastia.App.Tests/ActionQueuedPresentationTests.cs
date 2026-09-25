using Dynastia.Contracts;
using static Dynastia.App.Tests.ActionOptionTestServices;

namespace Dynastia.App.Tests;

public sealed class ActionQueuedPresentationTests
{
    [Theory]
    [InlineData("education.get_education", "summaryEducationOption", "University course", "University course")]
    [InlineData("craft.start.carpentry", "summaryCraft", "Carpentry", "Carpentry")]
    [InlineData("church.donate", "churchAmount", "2500", "2,500 zł")]
    [InlineData("farming.sell_farmland", "summaryFarmland", "Orchard", "Orchard")]
    [InlineData("heirloom.sell", "summaryHeirloom", "Family ring", "Family ring")]
    public void StoredSelectionDetailsRemainUnchanged(string id, string key, string value, string expected)
    {
        using var f = new ActionCoordinatorFixture();
        var queued = new QueuedActionInfo(id, "Legacy label", YearPhase.LifeEvents, f.Source.Head.Id, f.Source.Head.Id,
            Parameters: new Dictionary<string, string> { [key] = value });
        Assert.Equal(expected, f.Panel.BuildQueuedActionDetail(queued));
    }

    [Fact]
    public void LegacyHouseSummariesResolveLiveAssetsButPreferStoredTownPriceAndCapacity()
    {
        using var f = new ActionCoordinatorFixture();
        var house = new HousePropertyInfo(Guid.NewGuid(), f.Source.Economy.Residence, false, true,
            PurchasePrice: 40000m, CapacityExtensions: 2);
        f.Source.Economy.Houses.Add(house);
        var parameters = new Dictionary<string, string> { ["propertyId"] = house.Id.ToString() };
        var queued = new QueuedActionInfo("household.sell_house", "Sell", YearPhase.LifeEvents,
            f.Source.Head.Id, f.Source.Head.Id, Parameters: parameters);
        Assert.Equal("Z Residence, 48,000 zł", f.Panel.BuildQueuedActionDetail(queued));
        Assert.Equal("Z Residence, 10,000 zł", f.Panel.BuildQueuedActionDetail(queued with { ActionId = "household.extend_house" }));
        parameters["summaryTown"] = "Stored town";
        parameters["summaryPrice"] = "12345";
        Assert.Equal("Stored town, 12,345 zł", f.Panel.BuildQueuedActionDetail(queued));
        parameters["summaryCapacity"] = "12";
        Assert.Equal("Stored town, 12 residents, 12,345 zł",
            f.Panel.BuildQueuedActionDetail(queued with { ActionId = "household.buy_house" }));
        parameters.Clear();
        parameters["townId"] = house.Town.Id;
        Assert.Equal("Z Residence, 40,000 zł", f.Panel.BuildQueuedActionDetail(queued with { ActionId = "household.buy_house" }));
        parameters["propertyId"] = "no-longer-valid";
        Assert.Equal(string.Empty, f.Panel.BuildQueuedActionDetail(queued));
    }

    [Theory]
    [InlineData(1, "5,000 zł, 1 year")]
    [InlineData(2, "5,000 zł, 2 years")]
    public void LoanQueuedTextKeepsInvariantAmountsAndYearPluralization(int years, string expectedDetail)
    {
        using var culture = new CultureScope("fr-FR");
        using var f = new ActionCoordinatorFixture();
        f.Source.Register("loan.take");
        f.Panel.QueueLoanAction("loan.take", new(5000m, years, "Bank", "town", "polish"));
        var queued = Assert.Single(f.Source.Actions.GetQueuedActions(f.Source.Head));
        Assert.Equal(expectedDetail, f.Panel.BuildQueuedActionDetail(queued));
        f.Panel.Refresh(false);
        Assert.EndsWith(expectedDetail, f.Panel.QueuedActionText);
        Assert.DoesNotContain(f.Source.Head.Name, f.Panel.QueuedActionText);
        Assert.Empty(f.Panel.AvailableActions);
    }

    [Theory]
    [InlineData(false, false, "Support Brother")]
    [InlineData(false, true, "Support Sister")]
    [InlineData(true, false, "Support Brother")]
    [InlineData(true, true, "Support Sister")]
    public void PaternalAndMaternalSiblingLabelsRemainIdenticalInChoicesAndQueue(bool maternal, bool female, string expected)
    {
        var family = new Family();
        using var f = new ActionCoordinatorFixture(family: family);
        var parent = f.Source.Person(60, "Parent");
        var sibling = f.Source.Person(12, "Sibling");
        if (female) sibling.Tags.Add("sex.female");
        family.SetParents(f.Source.Head, maternal ? null : parent, maternal ? parent : null);
        family.SetParents(sibling, maternal ? null : parent, maternal ? parent : null);
        f.Selected = sibling;
        f.Source.Register("childhood.raise_child");
        f.Panel.Refresh(false);
        Assert.Equal($"🫂 {expected}", f.Panel.AvailableActions.Single(action => action.Id == "childhood.raise_child").Label);
        f.Click("childhood.raise_child");
        f.Panel.Refresh(false);
        Assert.Equal($"Queued: 🫂 {expected} – Sibling Test", f.Panel.QueuedActionText);
    }

    [Fact]
    public void SelfAndUnrelatedTargetsKeepDefinitionLabelAndUnknownActionsKeepTheirLegacyFallback()
    {
        var family = new Family();
        using var f = new ActionCoordinatorFixture(family: family);
        f.Source.Register("childhood.raise_child", "plugin.unknown");
        f.Panel.Refresh(false);
        Assert.Equal("🫂 childhood.raise_child", f.Panel.AvailableActions.Single(a => a.Id == "childhood.raise_child").Label);
        f.Selected = f.Source.Person(12, "Unrelated");
        f.Panel.Refresh(false);
        Assert.Equal("🫂 childhood.raise_child", f.Panel.AvailableActions.Single(a => a.Id == "childhood.raise_child").Label);
        f.Click("plugin.unknown");
        f.Panel.Refresh(false);
        Assert.StartsWith("Queued: ⚙️ plugin.unknown", f.Panel.QueuedActionText);
        Assert.EndsWith(" – Unrelated Test", f.Panel.QueuedActionText);
    }
}
