using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Dynastia.Contracts;
using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.Inheritance;
using Dynastia.Mechanics.Loans;

namespace Dynastia.Core.Tests;

public sealed class EstateSettlementApplierTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotAndPlanDoNotWriteAndApplyTakesAssetsOnceBeforePublishingLegacyOrderedEvents(bool pending)
    {
        using var f = new RefactorFixture();
        var seed = Seed(f, -10001m, pending);
        var calls = new List<string>();
        var economy = Observe<IEconomyService>(f.Economy, "economy", calls);
        var heirlooms = Observe<IHeirloomService>(f.Heirlooms, "heirlooms", calls);
        var before = Fingerprint(f);
        var snapshot = new EstateSnapshotReader(f.Family, economy, heirlooms, () => null).Capture(f.State, seed.Head);
        var plan = new EstatePlanBuilder().Build(snapshot);
        Assert.Equal(before, Fingerprint(f));
        Assert.Empty(Mutations(calls));
        f.Events.EventPublished += (_, item) =>
        {
            calls.Add("event:" + item.Type);
            // Every recipient sees the completed transfer, not a half-settled estate.
            Assert.Equal(-10001m, pending ? f.Economy.GetPendingInheritance(seed.Heir)
                : f.Economy.GetHousehold(seed.Heir)!.Wealth);
            Assert.Single(pending ? f.Heirlooms.GetPending(seed.Heir) : f.Heirlooms.GetHeirlooms(seed.Heir));
        };
        var applier = new EstateSettlementApplier(f.Family, economy, heirlooms, f.Events);
        applier.Apply(f.State, plan);
        Assert.Equal(new[]
        {
            "economy.TakeAllHouses", "economy.TakeAllFarmland", "heirlooms.TakeAll",
            pending ? "economy.ChangePendingInheritance" : "economy.ChangeWealthAllowDebt",
            pending ? "economy.AddPendingHouse" : "economy.AddExistingHouse",
            pending ? "economy.AddPendingFarmland" : "economy.AddExistingFarmland",
            pending ? "heirlooms.AddPending" : "heirlooms.AddExisting",
            pending ? "event:inheritance.pending_houses" : "event:inheritance.houses",
            "event:farmland.inherited",
            pending ? "event:heirloom.pending" : "event:heirloom.inherited",
            pending ? "event:inheritance.debt_pending" : "event:inheritance.debt_received",
            "economy.SetWealth", "event:inheritance.estate_settled", "economy.DissolveHousehold"
        }, Mutations(calls));
        Assert.False(f.Economy.HasHousehold(seed.Head));
        calls.Clear();
        Assert.Throws<InvalidOperationException>(() => applier.Apply(f.State, plan));
        Assert.Empty(Mutations(calls));
        new EstateInheritanceSystem(f.Family, economy, heirlooms, f.Events).Execute(f.State);
        Assert.Empty(Mutations(calls));
    }

    [Fact]
    public void SnapshotUsesExactlyTheExistingAssetRemovalOrderWithoutTakingAnything()
    {
        using var f = new RefactorFixture();
        var seed = Seed(f);
        var secondHouse = seed.House with { Id = f.NextId() };
        var thirdHouse = seed.House with { Id = f.NextId() };
        f.Economy.AddExistingHouse(seed.Head, thirdHouse);
        f.Economy.AddExistingHouse(seed.Head, secondHouse);
        var lateFarm = seed.Farm with { Id = f.NextId(), AcquiredYear = 1890 };
        var earlyFarm = seed.Farm with { Id = f.NextId(), AcquiredYear = 1870 };
        f.Economy.AddExistingFarmland(seed.Head, lateFarm);
        f.Economy.AddExistingFarmland(seed.Head, earlyFarm);
        var secondItem = seed.Item with { Id = f.NextId() };
        var thirdItem = seed.Item with { Id = f.NextId() };
        f.Heirlooms.AddExisting(seed.Head, thirdItem, 1880, seed.Head.Id, "fixture");
        f.Heirlooms.AddExisting(seed.Head, secondItem, 1880, seed.Head.Id, "fixture");
        var before = Fingerprint(f);
        var snapshot = new EstateSnapshotReader(f.Family, f.Economy, f.Heirlooms, () => null).Capture(f.State, seed.Head);
        Assert.Equal(before, Fingerprint(f));
        Assert.Equal(new[] { seed.House.Id, thirdHouse.Id, secondHouse.Id }, snapshot.Houses.Select(asset => asset.Id));
        Assert.Equal(new[] { earlyFarm.Id, seed.Farm.Id, lateFarm.Id }, snapshot.Farmland.Select(asset => asset.Id));
        Assert.Equal(new[] { seed.Item.Id, thirdItem.Id, secondItem.Id }, snapshot.Heirlooms.Select(asset => asset.Id));
        Assert.Equal(snapshot.Houses.Select(asset => asset.Id), f.Economy.TakeAllHouses(seed.Head).Select(asset => asset.Id));
        Assert.Equal(snapshot.Farmland.Select(asset => asset.Id), f.Economy.TakeAllFarmland(seed.Head).Select(asset => asset.Id));
        Assert.Equal(snapshot.Heirlooms.Select(asset => asset.Id), f.Heirlooms.TakeAll(seed.Head).Select(asset => asset.Id));
    }

    [Theory]
    [InlineData("balance")]
    [InlineData("house-designation")]
    [InlineData("farmland")]
    [InlineData("heirloom")]
    [InlineData("heirloom-history")]
    [InlineData("dead-heir")]
    [InlineData("new-heir")]
    [InlineData("household")]
    [InlineData("year")]
    [InlineData("not-ready")]
    [InlineData("anchor")]
    public void StalePlanIsRejectedBeforeTheFirstMutation(string change)
    {
        using var f = new RefactorFixture();
        var seed = Seed(f);
        var plan = Plan(f, seed.Head);
        switch (change)
        {
            case "balance": f.Economy.ChangeWealthAllowDebt(seed.Head, 1m); break;
            case "house-designation":
                Assert.True(f.Economy.SetHouseInheritanceHeir(seed.Head, seed.House.Id, seed.Heir.Id)); break;
            case "farmland": Assert.NotNull(f.Economy.TakeFarmland(seed.Head, seed.Farm.Id)); break;
            case "heirloom": Assert.NotNull(f.Heirlooms.Take(seed.Head, seed.Item.Id)); break;
            case "heirloom-history":
                var item = f.Heirlooms.Take(seed.Head, seed.Item.Id)!;
                f.Heirlooms.AddExisting(seed.Head, item, 1900, seed.Head.Id, "returned"); break;
            case "dead-heir": seed.Heir.Tags.Remove("state.alive"); seed.Heir.Tags.Add("state.dead"); break;
            case "new-heir": f.Family.SetParents(f.Person(25), seed.Head, null); break;
            case "household": f.Economy.DissolveHousehold(seed.Heir); break;
            case "year": f.State.Year++; break;
            case "not-ready": f.Economy.MarkEstateReady(seed.Head, false); break;
            case "anchor": seed.Head.Components.Get<HouseholdEconomyComponent>()!.DynastyAnchorId = f.Person(70).Id; break;
        }
        var before = Fingerprint(f);
        var calls = new List<string>();
        var economy = Observe<IEconomyService>(f.Economy, "economy", calls);
        var heirlooms = Observe<IHeirloomService>(f.Heirlooms, "heirlooms", calls);
        Assert.Throws<InvalidOperationException>(() => new EstateSettlementApplier(
            f.Family, economy, heirlooms, f.Events).Apply(f.State, plan));
        Assert.Empty(Mutations(calls));
        Assert.Equal(before, Fingerprint(f));
        Assert.Empty(f.Events.AllEvents);
    }

    [Theory]
    [InlineData("missing-asset")]
    [InlineData("duplicate-asset")]
    [InlineData("invalid-recipient")]
    [InlineData("cash-conservation")]
    public void MalformedInstructionsAreRejectedBeforeAnyMutation(string flaw)
    {
        using var f = new RefactorFixture();
        var seed = Seed(f);
        var plan = Plan(f, seed.Head);
        IReadOnlyList<EstateAssetAllocation> houses = plan.Houses;
        IReadOnlyList<EstateCashAllocation> cash = plan.Cash;
        if (flaw == "missing-asset") houses = [];
        if (flaw == "duplicate-asset") houses = [plan.Houses[0], plan.Houses[0]];
        if (flaw == "invalid-recipient") houses = [plan.Houses[0] with { RecipientId = f.NextId() }];
        if (flaw == "cash-conservation") cash = [plan.Cash[0] with { Amount = plan.Cash[0].Amount + 1m }];
        var malformed = new EstateSettlementPlan(plan.Snapshot, plan.Kind, cash, houses,
            plan.Farmland, plan.Heirlooms, plan.Disadvantages);
        var before = Fingerprint(f);
        var calls = new List<string>();
        Assert.Throws<InvalidOperationException>(() => new EstateSettlementApplier(f.Family,
            Observe<IEconomyService>(f.Economy, "economy", calls),
            Observe<IHeirloomService>(f.Heirlooms, "heirlooms", calls), f.Events).Apply(f.State, malformed));
        Assert.Empty(Mutations(calls));
        Assert.Equal(before, Fingerprint(f));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlannerFailureLeavesTheEntireEstateUntouched(bool duplicateHouse)
    {
        using var f = new RefactorFixture();
        var seed = Seed(f, duplicateHouse ? 100m : decimal.MaxValue);
        if (duplicateHouse)
        {
            var household = seed.Head.Components.Get<HouseholdEconomyComponent>()!;
            household.Houses.Add(household.Houses[0]);
        }
        var before = Fingerprint(f);
        var calls = new List<string>();
        var system = new EstateInheritanceSystem(f.Family,
            Observe<IEconomyService>(f.Economy, "economy", calls),
            Observe<IHeirloomService>(f.Heirlooms, "heirlooms", calls), f.Events);
        if (duplicateHouse) Assert.Throws<InvalidOperationException>(() => system.Execute(f.State));
        else Assert.Throws<OverflowException>(() => system.Execute(f.State));
        Assert.Empty(Mutations(calls));
        Assert.Equal(before, Fingerprint(f));
    }

    [Fact]
    public void MissingFinanceRetainsTheExistingDissolveOnlyPath()
    {
        using var f = new RefactorFixture();
        var seed = Seed(f);
        var calls = new List<string>();
        var economy = Observe<IEconomyService>(f.Economy, "economy", calls);
        ((ServiceProbe)(object)economy).ReturnNullFor = nameof(IEconomyService.GetHousehold);
        var heirlooms = Observe<IHeirloomService>(f.Heirlooms, "heirlooms", calls);
        new EstateInheritanceSystem(f.Family, economy, heirlooms, f.Events).Execute(f.State);
        Assert.Equal(new[] { "economy.DissolveHousehold" }, Mutations(calls));
        Assert.Empty(f.Events.AllEvents);
        Assert.False(f.Economy.HasHousehold(seed.Head));
    }

    [Theory]
    [InlineData(10001)]
    [InlineData(-10001)]
    public void ContractualLoanPortfolioIsNotReinterpretedOrDoubleCounted(int wealth)
    {
        using var f = new RefactorFixture();
        var seed = Seed(f, wealth);
        var contract = new LoanContractState
        {
            ContractId = f.NextId(), Principal = 20000m, RemainingAmount = 17500m,
            AnnualPayment = 3500m, DurationYears = 10, YearsPaid = 5,
            StartYear = 1895, ServicingHouseholdId = f.Economy.GetHouseholdId(seed.Heir)
        };
        var portfolio = new LoanPortfolioComponent { Contracts = [contract] };
        seed.Heir.Components.Set(portfolio);
        var before = JsonSerializer.Serialize(portfolio);
        new EstateInheritanceSystem(f.Family, f.Economy, f.Heirlooms, f.Events).Execute(f.State);
        Assert.Same(portfolio, seed.Heir.Components.Get<LoanPortfolioComponent>());
        Assert.Equal(before, JsonSerializer.Serialize(portfolio));
        Assert.Equal((decimal)wealth, f.Economy.GetHousehold(seed.Heir)!.Wealth);
    }

    [Theory]
    [InlineData(37, false)]
    [InlineData(-37, false)]
    [InlineData(37, true)]
    [InlineData(-37, true)]
    public void RecipientEventsRetainExactLegacyIdsDataTextAndOrdering(int amount, bool pending)
    {
        using var f = new RefactorFixture();
        var seed = Seed(f, amount, pending);
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            new EstateInheritanceSystem(f.Family, f.Economy, f.Heirlooms, f.Events).Execute(f.State);
            var events = f.Events.AllEvents;
            Assert.Equal(5, events.Count);
            AssertEvent(events[0], pending ? "inheritance.pending_houses" : "inheritance.houses", seed.Heir.Id,
                [seed.Head.Id], new() { ["count"] = "1", ["towns"] = "Testowo", ["text"] = "Heir Family inherited 1 house." });
            AssertEvent(events[1], "farmland.inherited", seed.Heir.Id, [seed.Head.Id], new()
            { ["count"] = "1", ["towns"] = "Testowo", ["text"] = "Heir Family inherited 1 parcel of farmland." });
            AssertEvent(events[2], pending ? "heirloom.pending" : "heirloom.inherited", seed.Heir.Id, [seed.Head.Id], new()
            {
                ["heirloomId"] = seed.Item.Id.ToString(), ["item"] = "Family book", ["familyNews"] = "true",
                ["text"] = pending ? "Family book was set aside for Heir Family until the inheritance can be received."
                    : "Heir Family inherited Family book from the family estate."
            });
            AssertEvent(events[3], pending
                ? amount > 0 ? "inheritance.pending" : "inheritance.debt_pending"
                : amount > 0 ? "inheritance.received" : "inheritance.debt_received",
                seed.Heir.Id, [seed.Head.Id], new()
                {
                    ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                    ["text"] = pending
                        ? amount > 0 ? "Heir Family has an inheritance of 37 zł waiting until they establish a household."
                            : "Heir Family has 37 zł of inherited debt waiting until they establish a household."
                        : amount > 0 ? "Heir Family received an inheritance of 37 zł."
                            : "Heir Family inherited 37 zł of household debt."
                });
            AssertEvent(events[4], "inheritance.estate_settled", seed.Head.Id, [seed.Heir.Id], new()
            {
                ["estate"] = amount.ToString(CultureInfo.InvariantCulture), ["houses"] = "1", ["farmland"] = "1",
                ["heirlooms"] = "1", ["heirs"] = "1",
                ["text"] = "The remaining household estate of Source Family was divided among the living children."
            });
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResidualTransferPreservesLivingDesignationsAndFarmlandAcquisitionMetadata(bool pending)
    {
        using var f = new RefactorFixture();
        var seed = Seed(f, -83m, pending);
        var designated = f.Person(25);
        var household = seed.Head.Components.Get<HouseholdEconomyComponent>()!;
        household.DynastyAnchorId = seed.Heir.Id; // This is a living-anchor household departure.
        household.Houses[0].AssignedHeirId = designated.Id;
        household.Farmland[0].AssignedHeirId = designated.Id;
        new EstateInheritanceSystem(f.Family, f.Economy, f.Heirlooms, f.Events).Execute(f.State);
        var house = Assert.Single(pending ? f.Economy.TakePendingHouses(seed.Heir) : f.Economy.GetHouses(seed.Heir));
        var farm = Assert.Single(pending ? f.Economy.GetPendingFarmland(seed.Heir) : f.Economy.GetFarmland(seed.Heir));
        Assert.Equal(designated.Id, house.AssignedHeirId);
        Assert.Equal(designated.Id, farm.AssignedHeirId);
        Assert.Equal(1880, farm.AcquiredYear);
        Assert.Equal("purchase", farm.AcquisitionSource);
        var item = Assert.Single(pending ? f.Heirlooms.GetPending(seed.Heir) : f.Heirlooms.GetHeirlooms(seed.Heir));
        Assert.Equal(seed.Head.Id, item.OriginPersonId);
        Assert.Equal(pending ? "household_transfer_pending" : "household_transfer", item.OwnershipHistory[^1].Reason);
        Assert.Equal("household.assets_followed_anchor", Assert.Single(f.Events.AllEvents).Type);
    }

    private static void AssertEvent(GameEvent actual, string type, Guid subject,
        IReadOnlyList<Guid> related, Dictionary<string, string> data)
    {
        Assert.Equal(type, actual.Type);
        Assert.Equal(1900, actual.Year);
        Assert.Equal(subject, actual.SubjectId);
        Assert.Equal(related, actual.RelatedPersonIds);
        Assert.Equal(data.OrderBy(pair => pair.Key), actual.Data.OrderBy(pair => pair.Key));
    }

    private static EstateSettlementPlan Plan(RefactorFixture f, IPerson head) =>
        new EstatePlanBuilder().Build(new EstateSnapshotReader(f.Family, f.Economy, f.Heirlooms, () => null).Capture(f.State, head));

    private sealed record SeededEstate(IPerson Head, IPerson Heir, HousePropertyInfo House,
        FarmlandAssetInfo Farm, HeirloomAssetInfo Item);

    private static SeededEstate Seed(RefactorFixture f, decimal wealth = 10001m, bool pending = false)
    {
        var head = f.Person(80, alive: false, name: "Source");
        var heir = f.Person(pending ? 12 : 30, name: "Heir");
        f.Household(head, wealth);
        if (!pending) f.Household(heir);
        f.Family.SetParents(heir, head, null);
        var house = new HousePropertyInfo(f.NextId(), f.Town, true, false, PurchasePrice: 60000m);
        var farm = new FarmlandAssetInfo(f.NextId(), f.Town, 1880, "purchase", FarmTypeId: "orchard");
        var item = new HeirloomAssetInfo(f.NextId(), "fixture", "", "Family book", 700m, 1880,
            head.Id, "fixture", "fixture", "Family work", null, false, []);
        f.Economy.AddExistingHouse(head, house);
        f.Economy.AddExistingFarmland(head, farm);
        f.Heirlooms.AddExisting(head, item, 1880, head.Id, "fixture");
        f.Economy.MarkEstateReady(head);
        return new(head, heir, house, farm, item);
    }

    private static string Fingerprint(RefactorFixture f) => JsonSerializer.Serialize(new
    {
        People = f.State.People.Select(person => new
        {
            person.Id,
            Household = person.Components.Get<HouseholdEconomyComponent>(),
            Pending = person.Components.Get<PersonalEstateComponent>(),
            OwnedHeirlooms = f.Heirlooms.GetHeirlooms(person),
            PendingHeirlooms = f.Heirlooms.GetPending(person)
        }).ToArray(),
        Events = f.Events.AllEvents
    });

    private static IReadOnlyList<string> Mutations(IEnumerable<string> calls) => calls.Where(call =>
        call.StartsWith("event:", StringComparison.Ordinal)
        || call.StartsWith("economy.Take", StringComparison.Ordinal)
        || call.StartsWith("heirlooms.Take", StringComparison.Ordinal)
        || call.StartsWith("economy.Add", StringComparison.Ordinal)
        || call.StartsWith("heirlooms.Add", StringComparison.Ordinal)
        || call.StartsWith("economy.Change", StringComparison.Ordinal)
        || call is "economy.SetWealth" or "economy.DissolveHousehold").ToArray();

    private static T Observe<T>(T target, string name, List<string> calls) where T : class
    {
        var proxy = DispatchProxy.Create<T, ServiceProbe>();
        var probe = (ServiceProbe)(object)proxy;
        probe.Target = target;
        probe.Name = name;
        probe.Calls = calls;
        return proxy;
    }

    public class ServiceProbe : DispatchProxy
    {
        public object Target { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public List<string> Calls { get; set; } = [];
        public string? ReturnNullFor { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var method = targetMethod ?? throw new InvalidOperationException("A service method is required.");
            Calls.Add(Name + "." + method.Name);
            if (method.Name == ReturnNullFor) return null;
            try { return method.Invoke(Target, args); }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }
    }
}
