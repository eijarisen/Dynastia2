using Dynastia.Contracts;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed partial class AutonomousStrategyCharacterizationTests
{
    [Theory]
    [InlineData(1000)]
    [InlineData(800)]
    [InlineData(700)]
    [InlineData(650)]
    [InlineData(500)]
    public void ScoreCannotOutbidSurvivalContinuityOrStability(int band)
    {
        using var f = new Fixture();
        var need = Candidate("need", f.Head, band, 1);
        var reward = Candidate("reward", f.Head, 300, 150) with { ExpectedGameScore = 1000000 };
        Assert.Same(need, f.Strategy.ChooseAction([reward, need]));
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void SafeDevelopmentUsesIncrementalRewardBeforePersonalityUtility()
    {
        using var f = new Fixture();
        var reward = Candidate("new-achievement", f.Head, 300, 1) with { ExpectedGameScore = 25 };
        var repeated = Candidate("already-earned", f.Head, 300, 150) with { ExpectedGameScore = 0 };
        Assert.Same(reward, f.Strategy.ChooseAction([repeated, reward]));
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void EquivalentMedicalUrgencyProtectsMaleLineBeforeOtherBloodline()
    {
        using var f = new Fixture();
        var daughter = f.World.Person(8, sex: Sex.Female);
        var son = f.World.Person(9);
        var snapshot = f.Snapshot with
        {
            Members = [Member(f.Head),
                Member(daughter, 20, immediate: true) with { IsBloodline = true },
                Member(son, 20, immediate: true) with { IsBloodline = true, IsMaleLineage = true }],
            HasSeriousMedicalDanger = true,
            HasImmediateMedicalDanger = true
        };
        var actions = new[] { daughter, son }.Select(person =>
            f.Strategy.ScoreAction(Candidate("wellbeing.heal_relative", person), snapshot)!).ToArray();
        Assert.Same(son, f.Strategy.ChooseAction(actions)!.Target);
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void AcuteBloodlineEmergencyPrecedesNonAcuteMaleLineTreatment()
    {
        using var f = new Fixture();
        var daughter = f.World.Person(8, sex: Sex.Female);
        var son = f.World.Person(9);
        var snapshot = f.Snapshot with
        {
            Members = [Member(f.Head),
                Member(daughter, 20, immediate: true) with { IsBloodline = true },
                Member(son, 50) with { IsBloodline = true, IsMaleLineage = true }],
            HasSeriousMedicalDanger = true,
            HasImmediateMedicalDanger = true
        };
        var actions = new[] { daughter, son }.Select(person =>
            f.Strategy.ScoreAction(Candidate("wellbeing.heal_relative", person), snapshot)!).ToArray();
        Assert.Same(daughter, f.Strategy.ChooseAction(actions)!.Target);
    }

    [Fact]
    public void ReproductiveSearchRejectsInfertileAndExpiredMatchesDespiteWealth()
    {
        var infertile = Partner("wealthy", 25, 0) with { PartnerValue = 10000, AnnualIncome = 1000000 };
        var expired = Partner("too-late", 45, 5);
        var viable = Partner("viable", 30, 3);
        Assert.Same(viable, AutonomousPartnerChoiceRules.Choose(
            [infertile, expired, viable], Sex.Female, needsContinuity: true, needsIncome: true));
        Assert.Null(AutonomousPartnerChoiceRules.Choose(
            [infertile, expired], Sex.Female, needsContinuity: true, needsIncome: false));
    }

    [Fact]
    public void SpouseChoiceUsesAcceptanceAndFertilityYearsWithoutRerolling()
    {
        var unlikely = Partner("unlikely", 20, 5) with { AcceptanceChance = 0.05 };
        var reliable = Partner("reliable", 25, 4) with { AcceptanceChance = 0.9 };
        Assert.Same(reliable, AutonomousPartnerChoiceRules.Choose(
            [unlikely, reliable], Sex.Female, needsContinuity: true, needsIncome: false));
    }

    [Fact]
    public void AutonomousLendingCarriesTheGeneratedBankTerms()
    {
        using var f = new Fixture();
        f.Actions.Register(Definition("loan.give"));
        var loans = new LoanOffersStub
        {
            Offers = [Offer("Actual borrower", 1000m, 5, 0.2m, 240m, 0.45m)]
        };
        f.Context.AddService<ILoanService>(loans);
        var action = Assert.Single(f.Strategy.GetAvailableActions(f.Snapshot));
        Assert.Equal("0.45", action.Parameters["interestMultiplier"]);
        Assert.Equal("Actual borrower", action.Parameters["counterpartyName"]);
        Assert.Equal((f.Head.Id, true, 1000m), Assert.Single(loans.Requests));
        loans.Offers = [];
        Assert.Empty(f.Strategy.GetAvailableActions(f.Snapshot));
    }

    [Fact]
    public void FarmlandSaleExplicitlySelectsRemoteLandBeforeProductiveLocalLand()
    {
        using var f = new Fixture();
        f.Actions.Register(Definition("farming.sell_farmland"));
        var local = new FarmlandAssetInfo(f.World.NextId(), f.World.Town, 1890, "test");
        var remote = new FarmlandAssetInfo(f.World.NextId(), f.World.Town with { Id = "remote" }, 1895, "test");
        var economy = Probe<IEconomyService>((method, _) => method.Name switch
        {
            nameof(IEconomyService.GetResidenceTown) => f.World.Town,
            nameof(IEconomyService.GetFarmland) => new[] { local, remote },
            _ => throw new InvalidOperationException(method.Name)
        });
        var action = Assert.Single(CreateStrategy(f, economy: economy).GetAvailableActions(f.Snapshot));
        Assert.Equal(remote.Id.ToString(), action.Parameters["farmlandId"]);
    }

    [Fact]
    public void RejectedFirstPlanFallsBackWithoutOverwritingAnExistingQueue()
    {
        using var f = new Fixture();
        var info = f.Snapshot.Household with { Class = HouseholdClass.Bloodline };
        var households = Probe<IHouseholdService>((method, _) =>
            method.Name == nameof(IHouseholdService.GetActiveHouseholds)
                ? new[] { info } : throw new InvalidOperationException(method.Name));
        var strategy = new FallbackStrategy(f.Snapshot);
        var service = new AutonomousHouseholdDecisionService(f.World.State, households, f.Actions, strategy);
        Assert.Equal(1, service.QueueActionsForAutonomousHouseholds());
        Assert.Equal(new[] { "stale", "valid" }, strategy.Attempts);

        var queued = new GameActionDefinition
        {
            Id = "user.choice", Label = "Choice", Description = "Choice",
            Mode = ActionExecutionMode.Queued, IsAvailable = _ => true,
            Execute = _ => new GameActionResult(true)
        };
        f.Actions.Register(queued);
        Assert.True(f.Actions.ExecuteAutonomous(queued.Id, f.Head, f.Head, actorHouseholdId: info.HouseholdId).Success);
        strategy.Attempts.Clear();
        Assert.Equal(0, service.QueueActionsForAutonomousHouseholds());
        Assert.Empty(strategy.Attempts);
        Assert.Equal("user.choice", Assert.Single(f.Actions.GetQueuedActions(f.Head)).ActionId);
    }

    private static PartnerCandidateInfo Partner(string key, int age, int fertility) =>
        new(key, key, "Family", Sex.Female, new GameDate(1900 - age, 1, 1), age,
            null!, 1, new Dictionary<string, int> { ["fertility"] = fertility }, [],
            null, "", "", 0, 3, 1000, 0, 0, null!, "", 50, 0.8, "test-town", 1900);

    private sealed class FallbackStrategy(AutonomousHouseholdSnapshot snapshot) : IAutonomousHouseholdStrategy
    {
        public List<string> Attempts { get; } = [];
        public AutonomousHouseholdSnapshot BuildSnapshot(HouseholdInfo _) => snapshot;
        public IReadOnlyList<AutonomousActionCandidate> GetAvailableActions(AutonomousHouseholdSnapshot _) =>
            [Candidate("stale", snapshot.Head), Candidate("valid", snapshot.Head)];
        public AutonomousActionCandidate? ScoreAction(AutonomousActionCandidate action, AutonomousHouseholdSnapshot _) => action;
        public AutonomousActionCandidate? ChooseAction(IReadOnlyList<AutonomousActionCandidate> actions) => actions.FirstOrDefault();
        public bool QueueAction(AutonomousActionCandidate action, AutonomousHouseholdSnapshot _)
        {
            Attempts.Add(action.Action.Id);
            return action.Action.Id == "valid";
        }
    }
}
