using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Plugins;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed partial class AutonomousStrategyCharacterizationTests
{
    [Theory]
    [InlineData("wellbeing.heal_relative", 1000, 136, "Survival")]
    [InlineData("wellbeing.recover", 1000, 78, "Survival")]
    [InlineData("household.sell_house", 800, 96, "Solvency")]
    [InlineData("career.seek_employment", 300, 62, "CareerDevelopment")]
    [InlineData("reproduction.try_for_baby", 650, 100.5, "Continuity")]
    [InlineData("education.help_learning", 300, 60, "ChildProtection")]
    [InlineData("family_relations.give_money", 100, 24, "FamilyRelations")]
    [InlineData("personality.religious_study", 100, 20, "Optional")]
    [InlineData("turn.pass", 100, 42, "Optional")]
    public void ScoringCharacterizesEveryPlannedDomainWithoutConsumingRandomness(
        string actionId, int band, double score, string category)
    {
        using var f = new Fixture();
        var snapshot = f.Snapshot;
        var target = f.Head;
        if (actionId is "wellbeing.heal_relative" or "wellbeing.recover")
            snapshot = snapshot with { Members = [Member(f.Head, 20, immediate: true)],
                HasImmediateMedicalDanger = true, HasSeriousMedicalDanger = true };
        if (actionId == "household.sell_house")
            snapshot = snapshot with { FinancialState = AutonomousFinancialState.Critical, HasInvestmentHouse = true };
        if (actionId == "reproduction.try_for_baby")
            snapshot = snapshot with { CanActivelyTryForChild = true, HasRealisticReproductivePath = true,
                LivingChildCount = 0, ReproductiveUrgency = 0.25 };
        if (actionId == "education.help_learning")
        {
            target = f.World.Person(10);
            snapshot = snapshot with { Members = [Member(f.Head), Member(target)] };
        }
        var candidate = Candidate(actionId, target);
        var result = f.Strategy.ScoreAction(candidate, snapshot);
        Assert.NotNull(result);
        Assert.Equal(band, result.PriorityBand);
        Assert.Equal(score, result.Score);
        Assert.Equal(category, result.Category.ToString());
        Assert.Same(candidate.Action, result.Action);
        Assert.Same(candidate.Target, result.Target);
        Assert.Same(candidate.Parameters, result.Parameters);
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void ImmediateNeedsSuppressOptionalSpendingAndDangerousFamilyChoices()
    {
        using var f = new Fixture();
        var crisis = f.Snapshot with { FinancialState = AutonomousFinancialState.Critical, HasSeriousMedicalDanger = true };
        foreach (var id in new[] { "family_relations.give_money", "personality.religious_study", "stats.improve_strength", "wellbeing.drink", "unknown.action" })
            Assert.Null(f.Strategy.ScoreAction(Candidate(id, f.Head), crisis));
        Assert.Null(f.Strategy.ScoreAction(Candidate("reproduction.try_for_baby", f.Head),
            f.Snapshot with { CanActivelyTryForChild = true, MarriageSatisfaction = 44 }));
        var pass = f.Strategy.ScoreAction(Candidate("turn.pass", f.Head), crisis);
        Assert.NotNull(pass);
        Assert.Equal(100, pass.PriorityBand);
        Assert.Equal(8d, pass.Score);
    }

    [Fact]
    public void HighestPriorityBandDominatesScoreWithoutAnyRandomDraw()
    {
        using var f = new Fixture();
        var emergency = Candidate("emergency", f.Head, band: 1000, score: 1);
        Assert.Same(emergency, f.Strategy.ChooseAction([
            Candidate("lucrative", f.Head, band: 800, score: 150), emergency]));
        Assert.Null(f.Strategy.ChooseAction([]));
    }

    [Fact]
    public void NonCompetitiveAlternativeDoesNotConsumeRandomness()
    {
        using var f = new Fixture();
        var best = Candidate("best", f.Head, score: 100);
        Assert.Same(best, f.Strategy.ChooseAction([Candidate("weak", f.Head, score: 84.999), best]));
        Assert.Same(best, f.Strategy.ChooseAction([best]));
    }

    [Theory]
    [InlineData(0.0, "best")]
    [InlineData(0.54, "best")]
    [InlineData(0.56, "runner-up")]
    [InlineData(0.999, "runner-up")]
    public void CompetitiveSelectionUsesOneDrawAndSquaredRatherThanLinearScores(double roll, string expected)
    {
        using var f = new Fixture(SequenceGameRandom.Double(roll));
        var best = Candidate("best", f.Head, score: 100);
        var runnerUp = Candidate("runner-up", f.Head, score: 90);
        var input = new[] { runnerUp, Candidate("excluded", f.Head, score: 84), best };
        var result = f.Strategy.ChooseAction(input);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Action.Id);
        Assert.Equal(1, f.World.Random.ConsumedCount);
        Assert.Same(runnerUp, input[0]); // Choosing does not reorder caller-owned candidates.
    }

    [Fact]
    public void FifteenPercentCompetitionBoundaryIsInclusive()
    {
        using var f = new Fixture(SequenceGameRandom.Double(0.99));
        var boundary = Candidate("boundary", f.Head, score: 85);
        Assert.Same(boundary, f.Strategy.ChooseAction([Candidate("best", f.Head, score: 100), boundary]));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.34, 1)]
    [InlineData(0.67, 2)]
    public void TiesAreOrderedByCaseInsensitiveActionIdThenTargetId(double roll, int index)
    {
        using var f = new Fixture(SequenceGameRandom.Double(roll));
        var other = f.World.Person();
        var expected = new[] { Candidate("A", f.Head), Candidate("a", other), Candidate("Z", f.Head) };
        Assert.Same(expected[index], f.Strategy.ChooseAction([expected[2], expected[1], expected[0]]));
    }

    [Fact]
    public void LoanCandidateUsesAffordableLocalOfferAndPreservesItsExactQueueParameters()
    {
        using var f = new Fixture();
        var loans = new LoanOffersStub();
        f.Context.AddService<ILoanService>(loans);
        f.Actions.Register(Definition("loan.take"));
        loans.Offers = [Offer("too small", 500m, 5, 0.01m, 100m, 0.5m),
            Offer("unaffordable", 10000m, 2, 0.01m, 2000m, 0.5m),
            Offer("expensive", 4000m, 8, 0.15m, 700m, 1.2m),
            Offer("chosen", 3000m, 7, 0.10m, 600m, 0.8m)];
        var candidate = Assert.Single(f.Strategy.GetAvailableActions(f.Snapshot));
        Assert.Equal("loan.take", candidate.Action.Id);
        Assert.Equal("3000", candidate.Parameters["principal"]);
        Assert.Equal("7", candidate.Parameters["durationYears"]);
        Assert.Equal("0.8", candidate.Parameters["interestMultiplier"]);
        Assert.Equal("chosen", candidate.Parameters["counterpartyName"]);
        Assert.Equal("bank-town", candidate.Parameters["counterpartyTownId"]);
        Assert.Equal("polish", candidate.Parameters["counterpartyNationalityId"]);
        Assert.Equal((f.Head.Id, false, 10000m), Assert.Single(loans.Requests));
        Assert.True(f.Strategy.QueueAction(candidate, f.Snapshot));
        var queued = Assert.Single(f.Actions.GetQueuedActions(f.Head));
        Assert.Equal(candidate.Action.Id, queued.ActionId);
        Assert.Equal(candidate.Target.Id, queued.TargetId);
        Assert.Equal(ActionExecutionOrigin.Autonomous, queued.Origin);
        Assert.Equal(f.Snapshot.Household.HouseholdId, queued.ActorHouseholdId);
        Assert.Equal(candidate.Parameters.OrderBy(p => p.Key), queued.Parameters!.OrderBy(p => p.Key));
    }

    [Fact]
    public void MissingOrUnusableLocalLoanOffersDoNotProduceCandidates()
    {
        using var f = new Fixture();
        f.Actions.Register(Definition("loan.take"));
        Assert.Empty(f.Strategy.GetAvailableActions(f.Snapshot));
        var loans = new LoanOffersStub();
        f.Context.AddService<ILoanService>(loans);
        Assert.Empty(f.Strategy.GetAvailableActions(f.Snapshot));
        loans.Offers = [Offer("unaffordable", 10000m, 2, 0.1m, 2000m, 1m)];
        Assert.Empty(f.Strategy.GetAvailableActions(f.Snapshot));
    }

    private static LoanOfferInfo Offer(string name, decimal principal, int years, decimal rate, decimal payment, decimal multiplier) =>
        new(name, name, Sex.Male, 40, "", new LoanTermsInfo(principal, years, rate, principal * (1 + rate), payment, multiplier))
        { OriginTownId = "bank-town" };

    private static GameActionDefinition Definition(string id) => new()
    { Id = id, Label = id, Description = id, IsAvailable = _ => true, Execute = _ => new GameActionResult(true) };

    private static AutonomousActionCandidate Candidate(string id, IPerson target, int band = 300, double score = 100) =>
        new(Definition(id), target, new Dictionary<string, string>(), AutonomyCategory.Optional, band, score);

    private static AutonomousMemberSnapshot Member(IPerson person, double health = 100, bool immediate = false) =>
        new(person, new HealthSnapshot(health, 100, []), null, new Dictionary<string, int>(),
            person.Age < 18, person.Age < 18, health <= 55, immediate);

    private sealed class Fixture : IDisposable
    {
        public Fixture(params SequenceGameRandom.ExpectedCall[] calls)
        {
            World = new RefactorFixture(calls);
            Head = World.Person();
            var household = World.Household(Head, 20000m);
            Actions = new ActionRegistry(World.State, World.Events, World.Random, new ActionGuardRegistry());
            // Hand-built snapshots avoid unrelated annual service work. Unused services are deliberately absent:
            // a new dependency or random draw in the characterized paths must fail this fixture.
            Strategy = CreateStrategy(this);
            Snapshot = new AutonomousHouseholdSnapshot(
                new HouseholdInfo(household.HouseholdId, Head.Id, Head.Id, 1, "Family", "Test", HouseholdClass.Lineage, [Head.Id]),
                Head, World.Economy.GetHousehold(Head), null, [Member(Head)], null, [], AutonomousFinancialState.Secure,
                5000m, 2000m, 0m, false, false, false, true, false, false, 2, 0, null, 0, []);
        }
        public RefactorFixture World { get; }
        public IPerson Head { get; }
        public GamePluginContext Context { get; } = new();
        public ActionRegistry Actions { get; }
        public AdvancedAutonomousHouseholdStrategy Strategy { get; }
        public AutonomousHouseholdSnapshot Snapshot { get; }
        public void Dispose() => World.Dispose();
    }

    private sealed class LoanOffersStub : ILoanService
    {
        public IReadOnlyList<LoanOfferInfo> Offers { get; set; } = [];
        public List<(Guid PersonId, bool Giving, decimal Maximum)> Requests { get; } = [];
        public IReadOnlyList<LoanOfferInfo> GetOffers(IPerson person, bool isGivingLoan, decimal maximumPrincipal)
        { Requests.Add((person.Id, isGivingLoan, maximumPrincipal)); return Offers; }
        public LoanTermsInfo CalculateTerms(decimal principal, int durationYears, decimal interestMultiplier = 1m) => throw new NotSupportedException();
        public bool HasActiveSelfOriginatedBankLoan(IPerson borrower) => false;
        public IReadOnlyList<LoanContractInfo> GetDebts(IPerson person) => [];
        public IReadOnlyList<LoanContractInfo> GetLoansGiven(IPerson person) => [];
    }
}
