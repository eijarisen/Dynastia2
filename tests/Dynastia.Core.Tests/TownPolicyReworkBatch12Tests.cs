using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Community;

namespace Dynastia.Core.Tests;

public sealed class TownPolicyReworkBatch12Tests
{
    [Fact]
    public void ReplacementCatalogKeepsRemovedActivePoliciesAsLegacyOnly()
    {
        var catalog = CommunityPolicyCatalog.Load(DataService());

        Assert.Equal(78, catalog.Policies.Count);
        Assert.Equal(5, catalog.LegacyPolicies.Count);
        Assert.DoesNotContain(catalog.Policies, policy =>
            policy.Id.Equals("anniversary", StringComparison.OrdinalIgnoreCase));

        var legacy = catalog.Get("anniversary");
        Assert.Equal("Official Anniversary Programme", legacy.DisplayName);
        Assert.Equal("No direct effect.", legacy.EffectSummary);

        var current = catalog.Get("municipal_jubilee");
        Assert.Equal("Ceremonial", current.ImpactTier);
        Assert.Equal("No direct effect.", current.EffectSummary);
    }

    [Fact]
    public void ProposalGenerationIsStableBandFirstAndRespectsSetCapsAcrossLargeSample()
    {
        var (service, catalog) = CreateService();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sawThreeCeremonial = false;
        const int samples = 2_000;

        for (var index = 0; index < samples; index++)
        {
            var town = Town($"policy-sample-{index}");
            var first = service.GetProposals(town, 2020);
            var repeated = service.GetProposals(town, 2020);

            Assert.Equal(3, first.Count);
            Assert.Equal(
                first.Select(proposal => (proposal.PolicyId, proposal.Proposer.Id)).ToArray(),
                repeated.Select(proposal => (proposal.PolicyId, proposal.Proposer.Id)).ToArray());
            Assert.Equal(3, first.Select(proposal => proposal.PolicyId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.True(first.Count(proposal => proposal.ImpactTier.Equals("Strong", StringComparison.OrdinalIgnoreCase)) <= 1);
            Assert.True(first.Count(proposal => proposal.Favorability.Equals("Unfavorable", StringComparison.OrdinalIgnoreCase)) <= 1);

            sawThreeCeremonial |= first.All(proposal =>
                proposal.ImpactTier.Equals("Ceremonial", StringComparison.OrdinalIgnoreCase));

            foreach (var proposal in first)
            {
                counts[proposal.ImpactTier] = counts.GetValueOrDefault(proposal.ImpactTier) + 1;
                Assert.False(string.IsNullOrWhiteSpace(proposal.EffectSummary));
                Assert.Contains(catalog.Policies, policy => policy.Id.Equals(proposal.PolicyId, StringComparison.OrdinalIgnoreCase));
            }
        }

        var total = samples * 3.0;
        Assert.InRange(counts.GetValueOrDefault("Ceremonial") / total, 0.40, 0.50);
        Assert.InRange(counts.GetValueOrDefault("Weak") / total, 0.30, 0.40);
        Assert.InRange(counts.GetValueOrDefault("Medium") / total, 0.12, 0.20);
        Assert.InRange(counts.GetValueOrDefault("Strong") / total, 0.02, 0.06);
        Assert.True(sawThreeCeremonial);
    }

    [Fact]
    public void NewPolicyModifiersStackAndClampInSingleSnapshot()
    {
        var (service, _) = CreateService(out var state, out var anchor);
        var town = Town("modifier-town");
        anchor.Components.Set(new CommunityWorldStateComponent
        {
            Towns =
            [
                new CommunityTownPolicyState
                {
                    TownId = town.Id,
                    ActivePolicies =
                    [
                        Active("food_market_rules"),
                        Active("wage_agreement"),
                        Active("sanitation_programme"),
                        Active("public_safety"),
                        Active("technology_hub")
                    ]
                }
            ]
        });
        state.Year = 2020;

        var modifiers = service.GetModifiers(town, 2020);

        Assert.Equal(0.98m, modifiers.LivingCostMultiplier);
        Assert.Equal(1.06m, modifiers.CareerIncomeMultiplier); // 1.04 * 1.04, capped
        Assert.Equal(0.5, modifiers.AnnualHealthAdd, 10);
        Assert.Equal(0.90, modifiers.CrimeChanceMultiplier, 10);
        Assert.Equal(2, modifiers.ProsperityFlat);
        Assert.Equal(0.05, modifiers.JobApplicationAdd, 10);
        Assert.Equal(0.95m, modifiers.HistoricalHealthLossMultiplier);
        Assert.Equal(0.95m, modifiers.HistoricalWealthLossMultiplier);

        state.Year = 2026;
        var expired = service.GetModifiers(town, 2026);
        Assert.Equal(1m, expired.LivingCostMultiplier);
        Assert.Equal(1m, expired.CareerIncomeMultiplier);
        Assert.Equal(0d, expired.AnnualHealthAdd);
        Assert.Equal(1d, expired.CrimeChanceMultiplier);
    }

    [Fact]
    public void PolicyEligibilityHonorsSettlementMaximumAndProsperityRange()
    {
        var (service, _) = CreateService();
        var policy = new CommunityPolicyDefinition(
            "eligibility_probe",
            "Eligibility Probe",
            1900,
            null,
            SettlementClass.SmallTown,
            SettlementClass.Town,
            null,
            null,
            60,
            80,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            "Common",
            1,
            "Weak",
            0.1,
            2,
            "none",
            0,
            "Neutral",
            ["clerk"],
            "Eligibility test policy.",
            "No direct effect.");

        var eligibleTown = TownWithPopulation("eligible-town", 10_000);
        var city = TownWithPopulation("too-large-city", 50_000);
        var opportunities = new LocationOpportunitySnapshot(
            eligibleTown,
            "Policy Region",
            Array.Empty<string>(),
            Array.Empty<string>(),
            "No special opportunities");
        var institutions = new TownInstitutionSnapshot(
            eligibleTown,
            2020,
            Array.Empty<TownInstitutionInfo>());
        var noActivePolicies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.True(service.IsEligible(
            policy, eligibleTown, 2020, opportunities, institutions, 70, noActivePolicies));
        Assert.False(service.IsEligible(
            policy, city, 2020, opportunities with { Town = city },
            institutions with { Town = city }, 70, noActivePolicies));
        Assert.False(service.IsEligible(
            policy, eligibleTown, 2020, opportunities, institutions, 59, noActivePolicies));
        Assert.False(service.IsEligible(
            policy, eligibleTown, 2020, opportunities, institutions, 81, noActivePolicies));
    }

    private static CommunityActivePolicyState Active(string id) =>
        new()
        {
            PolicyId = id,
            EnactedYear = 2020,
            ExpiresAfterYear = 2025
        };

    private static (CommunityPolicyService Service, CommunityPolicyCatalog Catalog) CreateService() =>
        CreateService(out _, out _);

    private static (CommunityPolicyService Service, CommunityPolicyCatalog Catalog) CreateService(
        out GameState state,
        out IPerson anchor)
    {
        var data = DataService();
        var catalog = CommunityPolicyCatalog.Load(data);
        var rules = CommunityPolicyRules.Load(data);
        state = new GameState
        {
            StartYear = 1700,
            Year = 2020
        };
        anchor = state.CreatePerson(
            "Policy",
            "Seed",
            30,
            Guid.Parse("6f2f8531-9c9c-4cc1-8761-419c8e10a203"));

        var opportunityTags = catalog.Policies
            .Select(policy => policy.RequiredOpportunityTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var service = new CommunityPolicyService(
            state,
            new StubOpportunityService(opportunityTags),
            new StubInstitutionService(),
            new StubProsperityService(50),
            new StubNationalityService(),
            new StubNameService(),
            catalog,
            rules);
        return (service, catalog);
    }

    private static JsonGameDataService DataService() =>
        new(RepositoryFiles.Path("data"));

    private static TownInfo Town(string id) =>
        TownWithPopulation(id, 150_000);

    private static TownInfo TownWithPopulation(string id, int population) =>
        new("Policy City", "Policy County", 19.0, 52.0, population)
        {
            Id = id,
            RegionId = "generic_region",
            PolityId = "test_polity",
            PolityName = "Test Polity",
            IsDestinationAvailable = true
        };

    private sealed class StubOpportunityService(IReadOnlyList<string> tags) : ILocalCareerOpportunityService
    {
        public CareerLocationEvaluation Evaluate(IPerson person, CareerLocationRequirement requirement) =>
            throw new NotSupportedException();

        public CareerLocationEvaluation Evaluate(TownInfo town, CareerLocationRequirement requirement) =>
            throw new NotSupportedException();

        public LocationOpportunitySnapshot GetOpportunitySnapshot(IPerson person) =>
            throw new NotSupportedException();

        public LocationOpportunitySnapshot GetOpportunitySnapshot(TownInfo town) =>
            new(town, "Policy Region", tags, tags, "All policy opportunity tags");
    }

    private sealed class StubInstitutionService : ITownInstitutionService
    {
        private static readonly string[] InstitutionIds =
            ["school", "bank", "medical", "court", "administration", "post_office", "railway_station", "port", "church"];

        public TownInstitutionSnapshot Resolve(TownInfo town, int year) =>
            new(
                town,
                year,
                InstitutionIds.Select(id => new TownInstitutionInfo(id, id, 5, "Excellent")).ToArray());
    }

    private sealed class StubProsperityService(int index) : ITownProsperityService
    {
        public TownProsperitySnapshot Get(TownInfo town) =>
            new(index, "Stable", 0, Array.Empty<string>());

        public TownProsperitySnapshot Get(TownInfo town, int year) => Get(town);

        public decimal GetIncomeMultiplier(TownInfo town, LocalEconomicStrength strength) => 1m;

        public void ApplyHistoricalShock(IEnumerable<string> placeIds, string sourceId, int delta, int recoveryYears)
        {
        }
    }

    private sealed class StubNationalityService : INationalityService
    {
        public string GetNationality(IPerson person) => "polish";
        public void SetNationality(IPerson person, string id) { }
        public string GetDisplayName(string id) => id;
        public string GetNameCultureId(string id) => "polish";
        public IReadOnlyDictionary<string, double> ResolveDistribution(string regionId, int year) =>
            new Dictionary<string, double> { ["polish"] = 1.0 };
        public string GenerateNationality(string regionId, int year, IGameRandom random) => "polish";
        public void RegisterDistributionModifierProvider(INationalityDistributionModifierProvider provider) { }
        public string FormatSurname(IPerson person, string surname, Sex sex) => surname;
    }

    private sealed class StubNameService : IHistoricalNameService
    {
        public string GetRandomFirstName(Sex sex, int birthYear, IGameRandom random) =>
            sex == Sex.Male ? "Jan" : "Anna";

        public string GetRandomFirstName(Sex sex, int birthYear, string nameCultureId, IGameRandom random) =>
            GetRandomFirstName(sex, birthYear, random);

        public string GetRandomDifferentFirstName(Sex sex, int birthYear, string excludedName, IGameRandom random) =>
            sex == Sex.Male ? "Piotr" : "Maria";

        public string GetRandomDifferentFirstName(Sex sex, int birthYear, string excludedName, string nameCultureId, IGameRandom random) =>
            GetRandomDifferentFirstName(sex, birthYear, excludedName, random);

        public string GetRandomFirstNameExcluding(Sex sex, int birthYear, IReadOnlyCollection<string> excludedNames, IGameRandom random) =>
            sex == Sex.Male ? "Adam" : "Ewa";

        public string GetRandomFirstNameExcluding(Sex sex, int birthYear, IReadOnlyCollection<string> excludedNames, string nameCultureId, IGameRandom random) =>
            GetRandomFirstNameExcluding(sex, birthYear, excludedNames, random);

        public string GetRandomSurname(Sex sex, string nameCultureId, IGameRandom random) => "Nowak";
        public string FormatSurname(string surname, Sex sex, string nameCultureId) => surname;
        public bool HasNameCulture(string nameCultureId) => true;
    }
}
