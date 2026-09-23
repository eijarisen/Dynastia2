using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Plugins;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed partial class AutonomousStrategyCharacterizationTests
{
    // This table records the pre-REF-04 switch, including broad dynamic prefixes.
    [Theory]
    [InlineData("wellbeing.heal_relative", typeof(AutonomousHealthScorer))]
    [InlineData("wellbeing.therapy", typeof(AutonomousHealthScorer))]
    [InlineData("wellbeing.recover", typeof(AutonomousHealthScorer))]
    [InlineData("career.ask_to_recover", typeof(AutonomousHealthScorer))]
    [InlineData("household.hire_nanny", typeof(AutonomousHealthScorer))]
    [InlineData("household.ask_daughter_nanny", typeof(AutonomousHealthScorer))]
    [InlineData("household.fire_nanny", typeof(AutonomousHealthScorer))]
    [InlineData("household.sell_house", typeof(AutonomousFinancePropertyScorer))]
    [InlineData("farming.sell_farmland", typeof(AutonomousFinancePropertyScorer))]
    [InlineData("loan.take", typeof(AutonomousFinancePropertyScorer))]
    [InlineData("household.buy_house", typeof(AutonomousFinancePropertyScorer))]
    [InlineData("farming.buy_farmland", typeof(AutonomousFinancePropertyScorer))]
    [InlineData("loan.give", typeof(AutonomousFinancePropertyScorer))]
    [InlineData("household.ask_move_out", typeof(AutonomousFinancePropertyScorer))]
    [InlineData("career.seek_employment", typeof(AutonomousCareerEducationScorer))]
    [InlineData("career.help_seek_employment", typeof(AutonomousCareerEducationScorer))]
    [InlineData("career.find_another_job", typeof(AutonomousCareerEducationScorer))]
    [InlineData("career.help_find_better_job", typeof(AutonomousCareerEducationScorer))]
    [InlineData("education.help_learning", typeof(AutonomousCareerEducationScorer))]
    [InlineData("education.private_tutor", typeof(AutonomousCareerEducationScorer))]
    [InlineData("education.get_education", typeof(AutonomousCareerEducationScorer))]
    [InlineData("career.work_harder", typeof(AutonomousCareerEducationScorer))]
    [InlineData("career.quit_job", typeof(AutonomousCareerEducationScorer))]
    [InlineData("career.ask_to_quit", typeof(AutonomousCareerEducationScorer))]
    [InlineData("craft.start.carpentry", typeof(AutonomousCareerEducationScorer))]
    [InlineData("craft.start.future_content", typeof(AutonomousCareerEducationScorer))]
    [InlineData("craft.start.", typeof(AutonomousCareerEducationScorer))]
    [InlineData("craft.teach.carpentry", typeof(AutonomousCareerEducationScorer))]
    [InlineData("craft.teach.future_content", typeof(AutonomousCareerEducationScorer))]
    [InlineData("craft.teach.", typeof(AutonomousCareerEducationScorer))]
    [InlineData("relationship.repair_marriage", typeof(AutonomousFamilyContinuityScorer))]
    [InlineData("reproduction.try_for_baby", typeof(AutonomousFamilyContinuityScorer))]
    [InlineData("relationship.find_spouse", typeof(AutonomousFamilyContinuityScorer))]
    [InlineData("childhood.raise_child", typeof(AutonomousFamilyContinuityScorer))]
    [InlineData("relationship.marry_off_daughter", typeof(AutonomousFamilyContinuityScorer))]
    [InlineData("family_relations.ask_money", typeof(AutonomousFamilyRelationsScorer))]
    [InlineData("family_relations.ask_house", typeof(AutonomousFamilyRelationsScorer))]
    [InlineData("family_relations.ask_farmland", typeof(AutonomousFamilyRelationsScorer))]
    [InlineData("family_relations.ask_job_help", typeof(AutonomousFamilyRelationsScorer))]
    [InlineData("family_relations.improve", typeof(AutonomousFamilyRelationsScorer))]
    [InlineData("family_relations.give_money", typeof(AutonomousFamilyRelationsScorer))]
    [InlineData("family_relations.give_house", typeof(AutonomousFamilyRelationsScorer))]
    [InlineData("family_relations.give_farmland", typeof(AutonomousFamilyRelationsScorer))]
    [InlineData("family_relations.give_job_help", typeof(AutonomousFamilyRelationsScorer))]
    [InlineData("personality.religious_study", typeof(AutonomousPersonalDevelopmentScorer))]
    [InlineData("wellbeing.drink", typeof(AutonomousPersonalDevelopmentScorer))]
    [InlineData("turn.pass", typeof(AutonomousPersonalDevelopmentScorer))]
    [InlineData("stats.improve_strength", typeof(AutonomousPersonalDevelopmentScorer))]
    [InlineData("stats.future_content", typeof(AutonomousPersonalDevelopmentScorer))]
    [InlineData("stats.", typeof(AutonomousPersonalDevelopmentScorer))]
    public void EachExistingActionHasExactlyOneDomainOwner(string id, Type expectedOwner)
    {
        using var f = new Fixture();
        var scorers = CreateScorers(f);
        foreach (var spelling in new[] { id, id.ToUpperInvariant() })
        {
            var owner = Assert.Single(scorers.Where(scorer => scorer.Handles(spelling)));
            Assert.Equal(expectedOwner, owner.GetType());
        }
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown.action")]
    [InlineData("career.future_action")]
    [InlineData("craft.start")]
    [InlineData("craft.teach")]
    [InlineData("craft.stop.carpentry")]
    [InlineData("stats")]
    [InlineData("notstats.improve_strength")]
    [InlineData("family_relations.ask_move_out")]
    public void UnsupportedActionsRemainUnscored(string id)
    {
        using var f = new Fixture();
        Assert.Empty(CreateScorers(f).Where(scorer => scorer.Handles(id)));
        Assert.Null(f.Strategy.ScoreAction(Candidate(id, f.Head), f.Snapshot));
    }

    [Fact]
    public void OverlappingOwnersFailBeforeEitherScorerRuns()
    {
        using var f = new Fixture();
        var first = new ScorerProbe("turn.pass", candidate => candidate);
        var second = new ScorerProbe("TURN.PASS", candidate => candidate);
        var strategy = CreateStrategy(f, scorers: [first, second]);
        var error = Assert.Throws<InvalidOperationException>(() =>
            strategy.ScoreAction(Candidate("turn.pass", f.Head), f.Snapshot));
        Assert.Contains("turn.pass", error.Message);
        Assert.Equal(0, first.ScoreCalls);
        Assert.Equal(0, second.ScoreCalls);
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(20, 0)]
    [InlineData(20, -1)]
    public void NonPositiveBaseScoresAndBandsAreRejectedBeforePersonality(double score, int band)
    {
        using var f = new Fixture();
        var owner = new ScorerProbe("turn.pass", candidate => candidate with { Score = score, PriorityBand = band });
        var strategy = CreateStrategy(f, scorers: [owner]);
        // Personality would dereference Head; rejected scores must never reach it.
        Assert.Null(strategy.ScoreAction(Candidate("turn.pass", f.Head), f.Snapshot with { Head = null! }));
        Assert.Equal(1, owner.ScoreCalls);
    }

    [Theory]
    [InlineData("wellbeing.recover", "personality.melancholic", 78, 1.12)]
    [InlineData("career.seek_employment", "personality.sanguine", 62, 1.10)]
    [InlineData("loan.take", "personality.choleric", 54, 1.12)]
    [InlineData("career.seek_employment", "personality.phlegmatic", 62, 0.92)]
    [InlineData("family_relations.give_money", "morals.good", 24, 1.12)]
    [InlineData("family_relations.give_money", "morals.evil", 24, 0.92)]
    [InlineData("turn.pass", "personality.phlegmatic", 42, 1.10)]
    public void PersonalityIsAppliedExactlyOnceAfterDomainScoring(
        string id, string tag, double baseScore, double multiplier)
    {
        using var f = new Fixture();
        f.Head.Tags.Add(tag);
        var owner = new ScorerProbe(id, candidate => candidate with { Score = baseScore, PriorityBand = 300 });
        var strategy = CreateStrategy(f, scorers: [owner]);
        var input = Candidate(id, f.Head);
        var result = Assert.IsType<AutonomousActionCandidate>(strategy.ScoreAction(input, f.Snapshot));
        Assert.Equal(baseScore * multiplier, result.Score); // Exact double equality, no tolerance.
        Assert.Equal(1, owner.ScoreCalls);
        Assert.Equal(100d, input.Score);
        Assert.Same(input.Parameters, result.Parameters);
    }

    [Theory]
    [InlineData(0.1, 1)]
    [InlineData(200, 150)]
    public void PositiveBaseScoresRetainTheFinalClamp(double score, double expected)
    {
        using var f = new Fixture();
        var owner = new ScorerProbe("turn.pass", candidate => candidate with { Score = score });
        var result = CreateStrategy(f, scorers: [owner]).ScoreAction(Candidate("turn.pass", f.Head), f.Snapshot);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Score);
    }

    [Theory]
    [InlineData(0.0, "career.seek_employment")]
    [InlineData(0.499, "career.seek_employment")]
    [InlineData(0.5, "education.get_education")]
    [InlineData(0.999, "education.get_education")]
    public void DomainScoresFlowThroughUnchangedWeightedChoiceAndAutonomousQueue(double roll, string expectedId)
    {
        using var f = new Fixture(SequenceGameRandom.Double(roll));
        var ids = new[] { "education.get_education", "turn.pass", "family_relations.give_money",
            "stats.improve_strength", "loan.give", "craft.teach.carpentry", "career.seek_employment" };
        var parameters = new Dictionary<string, string> { ["marker"] = "unchanged" };
        var candidates = ids.Select(id => Candidate(id, f.Head) with { Parameters = parameters }).ToArray();
        foreach (var candidate in candidates) f.Actions.Register(candidate.Action);
        var scored = candidates.Select(candidate => f.Strategy.ScoreAction(candidate, f.Snapshot))
            .OfType<AutonomousActionCandidate>().ToArray();
        Assert.Equal(new[] { 62d, 42d, 24d, 38d, 36d, 38d, 62d }, scored.Select(candidate => candidate.Score));
        Assert.Equal(new[] { 300, 100, 100, 300, 300, 300, 300 }, scored.Select(candidate => candidate.PriorityBand));
        Assert.Equal(0, f.World.Random.ConsumedCount);
        var selected = Assert.IsType<AutonomousActionCandidate>(f.Strategy.ChooseAction(scored));
        Assert.Equal(expectedId, selected.Action.Id);
        Assert.True(f.Strategy.QueueAction(selected, f.Snapshot));
        var queued = Assert.Single(f.Actions.GetQueuedActions(f.Head));
        Assert.Equal(expectedId, queued.ActionId);
        Assert.Equal(f.Head.Id, queued.TargetId);
        Assert.Equal(ActionExecutionOrigin.Autonomous, queued.Origin);
        Assert.Equal(f.Snapshot.Household.HouseholdId, queued.ActorHouseholdId);
        Assert.Equal("unchanged", queued.Parameters!["marker"]);
        Assert.Equal(1, f.World.Random.ConsumedCount);
    }

    [Theory]
    [InlineData(2600, 3000)]
    [InlineData(2000, 2000)]
    [InlineData(1800, 2000)]
    public void SnapshotPreservesWorldOrderLiveMembershipReadOrderAndConservativeForecast(
        decimal historicalExpenses, decimal expectedExpenses)
    {
        using var f = new Fixture();
        var child = f.World.Person(9);
        var spouse = f.World.Person(41, sex: Sex.Female);
        var dead = f.World.Person(8, alive: false);
        var outsider = f.World.Person(10);
        f.World.Family.SetSpouses(f.Head, spouse, 1890);
        f.World.Family.SetParents(child, f.Head, spouse);
        f.World.Family.SetParents(dead, f.Head, spouse);
        var calls = new List<string>();
        var health = Probe<IHealthService>((method, args) =>
        {
            Assert.Equal(nameof(IHealthService.GetHealth), method.Name);
            var person = Assert.IsAssignableFrom<IPerson>(args[0]);
            calls.Add("health:" + person.Id);
            return person == child
                ? new HealthSnapshot(45, 100, [new("illness", "Illness", "terminal", -5, null)])
                : new HealthSnapshot(80, 100, []);
        });
        var career = Probe<ICareerService>((method, args) =>
        {
            Assert.Equal(nameof(ICareerService.GetCareer), method.Name);
            var person = Assert.IsAssignableFrom<IPerson>(args[0]);
            calls.Add("career:" + person.Id);
            return EmployedCareer();
        });
        var stats = Probe<IStatsService>((method, args) =>
        {
            Assert.Equal(nameof(IStatsService.GetStats), method.Name);
            var person = Assert.IsAssignableFrom<IPerson>(args[0]);
            calls.Add("stats:" + person.Id);
            return new StatValue[] { new("FERTILITY", "Fertility", 3, "") };
        });
        var status = new HouseholdStatusSnapshot(f.Head.Id, 1, 3, 3, null, null, false, false, false, false, false, []);
        var households = Probe<IHouseholdService>((method, args) =>
        {
            Assert.Equal(nameof(IHouseholdService.GetStatus), method.Name);
            Assert.Same(f.Head, args[0]);
            calls.Add("status");
            return status;
        });
        var finance = f.Snapshot.Finance! with { LastExpenses = historicalExpenses };
        var economy = Probe<IEconomyService>((method, args) =>
        {
            Assert.Same(f.Head, args[0]);
            calls.Add(method.Name);
            return method.Name switch
            {
                nameof(IEconomyService.GetHousehold) => finance,
                nameof(IEconomyService.GetAnnualForecast) => new HouseholdAnnualForecast(6000m, 2000m, [], []),
                _ => throw new InvalidOperationException(method.Name)
            };
        });
        f.Context.AddService<ILoanService>(Probe<ILoanService>((method, args) =>
        {
            Assert.Equal(nameof(ILoanService.GetDebts), method.Name);
            Assert.Same(f.Head, args[0]);
            calls.Add("debts");
            return new LoanContractInfo[] { new(Guid.Empty, "Bank", 2000m, 400m, 5, LoanCreditorType.Bank, LoanStatus.Active, true) };
        }));
        var info = f.Snapshot.Household with { MemberIds = [spouse.Id, child.Id, dead.Id, child.Id] };
        var snapshot = CreateStrategy(f, households, health, career, stats, economy).BuildSnapshot(info);
        Assert.Equal(new[] { f.Head.Id, child.Id, spouse.Id }, snapshot.Members.Select(member => member.Person.Id));
        Assert.DoesNotContain(snapshot.Members, member => member.Person.Id == outsider.Id || member.Person.Id == dead.Id);
        Assert.Equal(new[] { child.Id }, snapshot.LivingChildren.Select(person => person.Id));
        Assert.Same(finance, snapshot.Finance);
        Assert.Same(status, snapshot.Status);
        Assert.Same(spouse, snapshot.Spouse);
        Assert.Equal(6000m, snapshot.ProjectedIncome);
        Assert.Equal(expectedExpenses, snapshot.ExpectedExpenses); // Debt is added only when the historical floor wins.
        Assert.Equal(400m, snapshot.DebtPayments);
        Assert.Equal(AutonomousFinancialState.Secure, snapshot.FinancialState);
        Assert.True(snapshot.HasImmediateMedicalDanger);
        Assert.True(snapshot.HasSeriousMedicalDanger);
        Assert.True(snapshot.HasRealisticReproductivePath);
        Assert.True(snapshot.CanActivelyTryForChild);
        Assert.Equal(1, snapshot.LivingChildCount);
        Assert.Equal(1, snapshot.DependentChildCount);
        Assert.Equal(1.1, snapshot.ReproductiveUrgency);
        Assert.Equal(3, snapshot.Members[1].Stats["fertility"]);
        Assert.Null(snapshot.Members[1].Career);
        Assert.Equal(new[] {
            "health:" + f.Head.Id, "career:" + f.Head.Id, "stats:" + f.Head.Id,
            "health:" + child.Id, "stats:" + child.Id,
            "health:" + spouse.Id, "career:" + spouse.Id, "stats:" + spouse.Id,
            "GetHousehold", "status", "GetAnnualForecast", "debts",
            "stats:" + spouse.Id, "stats:" + f.Head.Id }, calls);
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void MissingSnapshotHeadRetainsTheOriginalFailure()
    {
        using var f = new Fixture();
        var info = f.Snapshot.Household with { HeadId = Guid.Empty };
        var error = Assert.Throws<InvalidOperationException>(() => f.Strategy.BuildSnapshot(info));
        Assert.Equal($"Household {info.HouseholdId} has no head.", error.Message);
    }

    [Fact]
    public void DiscoveryPreservesMemberOrderFirstDuplicatesAndMechanicalAutonomousContext()
    {
        using var f = new Fixture();
        var child = f.World.Person(10);
        var pass = Definition("turn.pass");
        var duplicatePass = Definition("TURN.PASS");
        var unsupported = Definition("unsupported.action");
        var queries = new List<Guid>();
        var actions = Probe<IActionRegistry>((method, args) =>
        {
            Assert.Equal(nameof(IActionRegistry.GetAvailableActions), method.Name);
            Assert.Equal(4, args.Length);
            Assert.Same(f.Head, args[0]);
            var target = Assert.IsAssignableFrom<IPerson>(args[1]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(args[2]));
            var execution = Assert.IsType<ActionExecutionContext>(args[3]);
            Assert.Equal(ActionExecutionOrigin.Autonomous, execution.Origin);
            Assert.Equal(f.Snapshot.Household.HouseholdId, execution.ActorHouseholdId);
            queries.Add(target.Id);
            return new[] { pass, unsupported, duplicatePass };
        });
        var strategy = CreateStrategy(f, actions: actions);
        var snapshot = f.Snapshot with { Members = [Member(child), Member(f.Head), Member(child)] };
        var candidates = strategy.GetAvailableActions(snapshot);
        Assert.Equal(new[] { child.Id, f.Head.Id, child.Id }, queries);
        Assert.Equal(new[] { "turn.pass", "unsupported.action", "turn.pass", "unsupported.action" }, candidates.Select(c => c.Action.Id));
        Assert.Equal(new[] { child.Id, child.Id, f.Head.Id, f.Head.Id }, candidates.Select(c => c.Target.Id));
        Assert.Same(pass, candidates[0].Action);
        Assert.Same(pass, candidates[2].Action);
        Assert.All(candidates, candidate => Assert.Empty(candidate.Parameters));
        Assert.Null(strategy.ScoreAction(candidates[1], snapshot));
        Assert.Equal(3, queries.Count); // Scorers must not repeat discovery/availability checks.
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Theory]
    [InlineData("career.seek_employment", false)]
    [InlineData("career.seek_employment", true)]
    [InlineData("career.find_another_job", false)]
    [InlineData("career.find_another_job", true)]
    [InlineData("career.help_seek_employment", false)]
    [InlineData("career.help_seek_employment", true)]
    [InlineData("career.help_find_better_job", false)]
    [InlineData("career.help_find_better_job", true)]
    public void CareerParametersPreserveApplicantUrgencyStableTiesAndInvariantValues(string id, bool urgent)
    {
        using var f = new Fixture();
        var relative = f.World.Person(25);
        var expectedApplicant = id.StartsWith("career.help_", StringComparison.Ordinal) ? relative : f.Head;
        var requests = 0;
        var opportunities = new[] { Job("slow", 2000m, 0.2), Job("reliable", 1000m, 0.95),
            Job("equal-later", urgent ? 1000m : 2000m, urgent ? 0.95 : 0.2) };
        var career = Probe<ICareerService>((method, args) =>
        {
            Assert.Equal(nameof(ICareerService.GetJobOpportunities), method.Name);
            Assert.Same(expectedApplicant, args[0]);
            Assert.Equal(5, args[1]);
            requests++;
            return opportunities;
        });
        f.Actions.Register(Definition(id));
        var snapshot = f.Snapshot with { Members = [Member(relative)],
            FinancialState = urgent ? AutonomousFinancialState.Critical : AutonomousFinancialState.Secure };
        var candidate = Assert.Single(CreateStrategy(f, career: career).GetAvailableActions(snapshot));
        Assert.Equal(1, requests);
        Assert.Same(relative, candidate.Target);
        var expected = new Dictionary<string, string>
        {
            ["jobCareerId"] = urgent ? "reliable" : "slow", ["jobLevel"] = "2",
            ["jobRequiredAbility"] = "3", ["jobRequiredEducation"] = "2", ["jobRequiredExperience"] = "1",
            ["jobSuccessChance"] = urgent ? "0.95" : "0.2", ["jobAnnualSalary"] = urgent ? "1000" : "2000"
        };
        Assert.Equal(expected.OrderBy(p => p.Key), candidate.Parameters.OrderBy(p => p.Key));
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void RelatedHouseholdCandidatesKeepTheirContextAmountsAndSelectiveWillingnessReads()
    {
        using var f = new Fixture();
        var relative = f.World.Person(40);
        var dead = f.World.Person(60, alive: false);
        f.World.Household(relative, 2500m);
        var requests = 0;
        f.Context.AddService<IFamilyRelationService>(Probe<IFamilyRelationService>((method, args) =>
        {
            Assert.Equal(nameof(IFamilyRelationService.EvaluateRequestWillingness), method.Name);
            Assert.Same(f.Head, args[0]);
            Assert.Same(relative, args[1]);
            requests++;
            return 0.4;
        }));
        var households = Probe<IHouseholdService>((method, args) =>
        {
            Assert.Equal(nameof(IHouseholdService.ResolveHouseholdHead), method.Name);
            Assert.Same(relative, args[0]);
            return relative;
        });
        var queries = 0;
        var actions = Probe<IActionRegistry>((method, args) =>
        {
            Assert.Equal(nameof(IActionRegistry.GetAvailableActions), method.Name);
            Assert.Same(f.Head, args[0]);
            var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(args[2]);
            queries++;
            if (args[1] == f.Head)
            {
                Assert.Empty(parameters);
                return new[] { Definition("turn.pass") };
            }
            Assert.Same(relative, args[1]);
            Assert.Equal("true", parameters["familyRelations"]);
            return new[] { Definition("family_relations.ask_house"), Definition("family_relations.ask_farmland"),
                Definition("family_relations.ask_money"), Definition("family_relations.ask_job_help"), Definition("turn.pass") };
        });
        RelatedFamilyHouseholdInfo Related(IPerson person) => new(null, person.Id, HouseholdClass.Bloodline,
            [new FamilyRelationLinkInfo(person.Id, FamilyRelationshipType.Sibling, "Brother", 60, "Warm", 60, "Known", 60, "Warm")]);
        var snapshot = f.Snapshot with { FinancialState = AutonomousFinancialState.Critical,
            RelatedHouseholds = [Related(relative), Related(dead)] };
        var candidates = CreateStrategy(f, households: households, actions: actions).GetAvailableActions(snapshot);
        Assert.Equal(new[] { "turn.pass", "family_relations.ask_house", "family_relations.ask_farmland",
            "family_relations.ask_money", "family_relations.ask_job_help" }, candidates.Select(candidate => candidate.Action.Id));
        Assert.Equal(2, queries); // Dead relatives and non-family actions from the external query are skipped.
        Assert.Equal(3, requests); // Ask Farmland did not request willingness in the original strategy.
        Assert.Null(candidates[2].RequestWillingness);
        Assert.Equal(0.4, candidates[1].RequestWillingness);
        Assert.Equal(0.4, candidates[3].RequestWillingness);
        Assert.Equal(0.4, candidates[4].RequestWillingness);
        Assert.Equal("2000", candidates[3].Parameters["amount"]);
        Assert.All(candidates.Skip(1), candidate => Assert.Equal("true", candidate.Parameters["familyRelations"]));
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void SharedReproductiveEligibilityReadsCurrentRelationshipsAndTags()
    {
        using var f = new Fixture();
        var eligibility = new AutonomousReproductiveEligibility(f.World.Family);
        Assert.True(eligibility.CanSearchForReproductiveSpouse(f.Head));
        f.Head.Tags.Add("sexuality.homosexual");
        Assert.False(eligibility.CanSearchForReproductiveSpouse(f.Head));
        f.Head.Tags.Remove("sexuality.homosexual");
        Assert.True(eligibility.CanSearchForReproductiveSpouse(f.Head));
        f.World.Family.SetSpouses(f.Head, f.World.Person(30, sex: Sex.Female), 1900);
        Assert.False(eligibility.CanSearchForReproductiveSpouse(f.Head));
    }

    private static JobOpportunityInfo Job(string id, decimal income, double chance) =>
        new(id, id, "Clerk", 2, income, "intellect", 3, 2, 1, 3, 2, 1, chance, 1900);

    private static CareerSnapshot EmployedCareer() =>
        new(1, "Laborer", 3, "Content", 1000m, 1000m, false, IsEmployed: true);

    private static AdvancedAutonomousHouseholdStrategy CreateStrategy(
        Fixture f, IHouseholdService? households = null, IHealthService? health = null,
        ICareerService? career = null, IStatsService? stats = null, IEconomyService? economy = null,
        IActionRegistry? actions = null, IReadOnlyList<IAutonomousActionScorer>? scorers = null)
    {
        var reproductiveEligibility = new AutonomousReproductiveEligibility(f.World.Family);
        return new AdvancedAutonomousHouseholdStrategy(
            actions ?? f.Actions, f.World.Random,
            new AutonomousSnapshotBuilder(f.Context, f.World.State, households!, economy ?? f.World.Economy,
                health!, career!, f.World.Family, stats!, reproductiveEligibility),
            new AutonomousActionCandidateBuilder(f.Context, f.World.State, households!, actions ?? f.Actions,
                economy ?? f.World.Economy, career!),
            scorers ?? CreateScorers(f, career, stats, economy, reproductiveEligibility));
    }

    private static IReadOnlyList<IAutonomousActionScorer> CreateScorers(
        Fixture f, ICareerService? career = null, IStatsService? stats = null,
        IEconomyService? economy = null, AutonomousReproductiveEligibility? reproductiveEligibility = null) =>
        [
            new AutonomousHealthScorer(),
            new AutonomousFinancePropertyScorer(f.Context, f.World.State, economy ?? f.World.Economy),
            new AutonomousCareerEducationScorer(f.Context, career!, stats!),
            new AutonomousFamilyContinuityScorer(f.Context, f.World.Family, stats!,
                reproductiveEligibility ?? new AutonomousReproductiveEligibility(f.World.Family)),
            new AutonomousFamilyRelationsScorer(f.Context),
            new AutonomousPersonalDevelopmentScorer(f.Context)
        ];

    private sealed class ScorerProbe(string id, Func<AutonomousActionCandidate, AutonomousActionCandidate?> score)
        : IAutonomousActionScorer
    {
        public int ScoreCalls { get; private set; }
        public bool Handles(string actionId) => id.Equals(actionId, StringComparison.OrdinalIgnoreCase);
        public AutonomousActionCandidate? Score(AutonomousActionCandidate option, AutonomousHouseholdSnapshot snapshot)
        {
            ScoreCalls++;
            return score(option);
        }
    }

    private static T Probe<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var service = DispatchProxy.Create<T, ReadServiceProbe>();
        ((ReadServiceProbe)(object)service).Handler = handler;
        return service;
    }

    public class ReadServiceProbe : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException("Missing service method."), args ?? []);
    }
}
