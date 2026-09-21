using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CommunityPolicyService : ICommunityPolicyService
{
    private static readonly string[] WealthBands =
        ["Poor", "Modest", "Comfortable", "Wealthy", "Rich"];

    private readonly IGameState _gameState;
    private readonly ILocalCareerOpportunityService _opportunities;
    private readonly ITownInstitutionService _institutions;
    private readonly ITownProsperityService _prosperity;
    private readonly INationalityService _nationalities;
    private readonly IHistoricalNameService _names;
    private readonly CommunityPolicyCatalog _catalog;
    private readonly CommunityPolicyRules _rules;

    public CommunityPolicyService(
        IGameState gameState,
        ILocalCareerOpportunityService opportunities,
        ITownInstitutionService institutions,
        ITownProsperityService prosperity,
        INationalityService nationalities,
        IHistoricalNameService names,
        CommunityPolicyCatalog catalog,
        CommunityPolicyRules rules)
    {
        _gameState = gameState;
        _opportunities = opportunities;
        _institutions = institutions;
        _prosperity = prosperity;
        _nationalities = nationalities;
        _names = names;
        _catalog = catalog;
        _rules = rules;
    }

    public IReadOnlyList<CommunityPolicyProposalInfo> GetProposals(
        TownInfo town,
        int year)
    {
        ArgumentNullException.ThrowIfNull(town);

        var activeIds = GetActivePolicies(town, year)
            .Select(policy => policy.PolicyId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var opportunities = _opportunities.GetOpportunitySnapshot(town);
        var institutionSnapshot = _institutions.Resolve(town, year);
        var prosperity = _prosperity.Get(town, year).Index;

        var eligible = _catalog.Policies
            .Where(policy => IsEligible(
                policy,
                town,
                year,
                opportunities,
                institutionSnapshot,
                prosperity,
                activeIds))
            .ToList();

        if (eligible.Count < _rules.ProposalsPerTownYear)
            return Array.Empty<CommunityPolicyProposalInfo>();

        var selected = new List<CommunityPolicyDefinition>(_rules.ProposalsPerTownYear);
        if (_rules.PreferAtLeastOneFavorableSubstantive)
        {
            var favorable = eligible
                .Where(policy => policy.IsFavorable && !policy.IsNoEffect)
                .ToList();
            if (favorable.Count > 0)
            {
                var first = PickWeighted(
                    favorable,
                    StableUnit(BuildKey(town.Id, year, 0, "favorable")));
                selected.Add(first);
                eligible.Remove(first);
            }
        }

        for (var slot = selected.Count; slot < _rules.ProposalsPerTownYear; slot++)
        {
            var unfavorableCount = selected.Count(policy => policy.IsUnfavorable);
            var noEffectCount = selected.Count(policy => policy.IsNoEffect);
            var candidates = eligible
                .Where(policy =>
                    (!policy.IsUnfavorable || unfavorableCount < _rules.MaximumUnfavorablePerSet)
                    && (!policy.IsNoEffect || noEffectCount < _rules.MaximumNoEffectPerSet))
                .ToList();
            if (candidates.Count == 0)
                break;

            var chosen = PickWeighted(
                candidates,
                StableUnit(BuildKey(town.Id, year, slot, "proposal")));
            selected.Add(chosen);
            eligible.Remove(chosen);
        }

        return selected
            .Select((policy, slot) => ToProposal(policy, town, year, slot))
            .ToArray();
    }

    public IReadOnlyList<CommunityActivePolicyInfo> GetActivePolicies(
        TownInfo town,
        int year)
    {
        ArgumentNullException.ThrowIfNull(town);
        var state = FindTownState(town.Id);
        if (state is null)
            return Array.Empty<CommunityActivePolicyInfo>();

        return state.ActivePolicies
            .Where(policy => policy.EnactedYear <= year
                && policy.ExpiresAfterYear >= year)
            .Select(policy =>
            {
                var definition = _catalog.Get(policy.PolicyId);
                return new CommunityActivePolicyInfo(
                    definition.Id,
                    definition.DisplayName,
                    definition.Description,
                    policy.EnactedYear,
                    policy.ExpiresAfterYear,
                    definition.EffectKey,
                    definition.Magnitude);
            })
            .OrderBy(policy => policy.ExpiresAfterYear)
            .ThenBy(policy => policy.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public CommunityPolicyModifierSnapshot GetModifiers(
        TownInfo town,
        int year)
    {
        var active = GetActivePolicies(town, year);
        if (active.Count == 0)
            return new CommunityPolicyModifierSnapshot();

        var prosperity = 0;
        var education = 0.0;
        var jobApplication = 0.0;
        var farming = 1m;
        var craft = 1m;
        var medicalCost = 1m;
        var medicalSuccess = 0.0;
        var bank = 1m;
        var housing = 1m;
        var extraHousingOffers = 0;
        var schoolTier = 0;
        var medicalTier = 0;
        var bankTier = 0;
        var historicalWealth = 1m;
        var historicalHealth = 1m;
        var flood = 1m;
        var welfare = 1m;
        var recovery = 0;

        foreach (var policy in active)
        {
            var magnitude = policy.Magnitude;
            switch (policy.EffectKey.ToLowerInvariant())
            {
                case "prosperity_flat":
                    prosperity += (int)Math.Round(magnitude, MidpointRounding.AwayFromZero);
                    break;
                case "education_success_add":
                    education += magnitude;
                    break;
                case "job_application_add":
                    jobApplication += magnitude;
                    break;
                case "farming_income_multiplier":
                    farming *= (decimal)magnitude;
                    break;
                case "craft_income_multiplier":
                    craft *= (decimal)magnitude;
                    break;
                case "medical_cost_multiplier":
                    medicalCost *= (decimal)magnitude;
                    break;
                case "bank_quality_multiplier":
                    bank *= (decimal)magnitude;
                    break;
                case "housing_price_multiplier":
                    housing *= (decimal)magnitude;
                    break;
                case "historical_wealth_loss_multiplier":
                    historicalWealth *= (decimal)magnitude;
                    break;
                case "historical_health_loss_multiplier":
                    historicalHealth *= (decimal)magnitude;
                    break;
                case "flood_loss_multiplier":
                    flood *= (decimal)magnitude;
                    break;
                case "church_welfare_multiplier":
                    welfare *= (decimal)magnitude;
                    break;
                case "prosperity_recovery_bonus":
                    recovery += (int)Math.Round(magnitude, MidpointRounding.AwayFromZero);
                    break;
                case "school_service_tier_add":
                    schoolTier += (int)Math.Round(magnitude, MidpointRounding.AwayFromZero);
                    break;
                case "medical_service_tier_add":
                    medicalTier += (int)Math.Round(magnitude, MidpointRounding.AwayFromZero);
                    break;
                case "bank_service_tier_add":
                    bankTier += (int)Math.Round(magnitude, MidpointRounding.AwayFromZero);
                    break;
                case "medical_bundle_medium":
                    medicalCost *= 0.95m;
                    medicalSuccess += 0.03;
                    break;
                case "housing_bundle_medium":
                    housing *= 0.95m;
                    extraHousingOffers += 1;
                    break;
            }
        }

        return new CommunityPolicyModifierSnapshot(
            Math.Clamp(prosperity, -_rules.ProsperityFlatCap, _rules.ProsperityFlatCap),
            Math.Clamp(education, -_rules.EducationSuccessAddCap, _rules.EducationSuccessAddCap),
            Math.Clamp(jobApplication, -_rules.JobApplicationAddCap, _rules.JobApplicationAddCap),
            Math.Clamp(farming, 0.9m, _rules.FarmingIncomeMultiplierMax),
            Math.Clamp(craft, 0.9m, _rules.CraftIncomeMultiplierMax),
            Math.Clamp(medicalCost, _rules.MedicalCostMultiplierMin, _rules.MedicalCostMultiplierMax),
            Math.Clamp(medicalSuccess, -0.10, 0.10),
            Math.Clamp(bank, _rules.BankQualityMultiplierMin, _rules.BankQualityMultiplierMax),
            Math.Clamp(housing, _rules.HousingPriceMultiplierMin, _rules.HousingPriceMultiplierMax),
            Math.Clamp(extraHousingOffers, 0, 1),
            Math.Clamp(schoolTier, 0, _rules.ServiceTierBonusMax),
            Math.Clamp(medicalTier, 0, _rules.ServiceTierBonusMax),
            Math.Clamp(bankTier, 0, _rules.ServiceTierBonusMax),
            Math.Clamp(historicalWealth, _rules.HistoricalLossMultiplierMin, 1m),
            Math.Clamp(historicalHealth, _rules.HistoricalLossMultiplierMin, 1m),
            Math.Clamp(flood, _rules.HistoricalLossMultiplierMin, 1m),
            Math.Clamp(welfare, 1m, 1.5m),
            Math.Clamp(recovery, 0, 2));
    }

    public int GetParticipationCount(IPerson person) =>
        person.Components.Get<CommunityParticipationComponent>()?.Count ?? 0;

    internal double CalculateLobbyBonus(
        IPerson actor,
        IStatusService status,
        IEducationService education,
        IStatsService stats)
    {
        var social = status.GetStatus(actor);
        var educationLevel = education.GetEducationLevel(actor);
        var appeal = stats.GetStats(actor)
            .FirstOrDefault(stat => stat.Id.Equals("appeal", StringComparison.OrdinalIgnoreCase))
            ?.Value ?? 3;
        return _rules.CalculateLobbyBonus(
            social.LocalRenown,
            social.Reputation,
            educationLevel,
            appeal,
            isTownHead: false);
    }

    internal void RecordLobby(
        IPerson actor,
        Guid householdId,
        CommunityPolicyProposalInfo proposal,
        double supportBonus)
    {
        var state = GetWorldState(create: true);
        if (state is null)
            return;

        state.Lobbies.RemoveAll(lobby =>
            lobby.HouseholdId == householdId
            && lobby.TownId.Equals(proposal.TownId, StringComparison.OrdinalIgnoreCase)
            && lobby.ProposalYear == proposal.ProposalYear);
        state.Lobbies.Add(new CommunityLobbyState
        {
            TownId = proposal.TownId,
            ProposalYear = proposal.ProposalYear,
            PolicyId = proposal.PolicyId,
            ActorId = actor.Id,
            HouseholdId = householdId,
            SupportBonus = supportBonus
        });

        var participation = actor.Components.Get<CommunityParticipationComponent>()
            ?? new CommunityParticipationComponent();
        participation.Count += _rules.ParticipationCountGain;
        actor.Components.Set(participation);

        if (!state.Connections.Any(connection =>
                connection.HouseholdId == householdId
                && connection.Id == ParseStableGuid(proposal.Proposer.Id)))
        {
            state.Connections.Add(new CommunityConnectionState
            {
                Id = ParseStableGuid(proposal.Proposer.Id),
                HouseholdId = householdId,
                Name = proposal.Proposer.Name,
                Sex = proposal.Proposer.Sex,
                BirthYear = proposal.ProposalYear - proposal.Proposer.Age,
                NationalityId = proposal.Proposer.NationalityId,
                TownId = proposal.TownId,
                OccupationLabel = proposal.Proposer.Occupation,
                ArchetypeId = proposal.Proposer.ArchetypeId,
                WealthBand = proposal.Proposer.WealthBand,
                Renown = proposal.Proposer.Renown,
                Reputation = proposal.Proposer.Reputation,
                Familiarity = 20,
                Sympathy = 5,
                OriginPolicyId = proposal.PolicyId,
                IsActive = true
            });
        }
    }

    internal void ResolvePreviousYear(
        IHouseholdService households,
        IEconomyService economy,
        IStatusService status,
        ILocationService locations,
        IGameRandom random,
        IGameEventBus events)
    {
        var proposalYear = _gameState.Year - 1;
        if (proposalYear < _gameState.StartYear)
            return;

        var people = _gameState.People.ToDictionary(person => person.Id);
        var playableHeadsByTown = households.GetActiveHouseholds()
            .Where(household => household.Class == HouseholdClass.Lineage)
            .Select(household => people.TryGetValue(household.HeadId, out var head)
                ? head
                : null)
            .Where(head => head is not null)
            .Select(head => head!)
            .GroupBy(head => economy.GetResidenceTown(head).Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(head => head.Id).First(),
                StringComparer.OrdinalIgnoreCase);

        var world = GetWorldState(create: true);
        if (world is null)
            return;

        // Town policy decisions are autonomous. Resolve every town that still
        // exists in the current year and also existed in the proposal year;
        // only playable-household towns publish News.
        foreach (var currentTown in locations.GetTowns())
        {
            var town = locations.FindTownAtYear(currentTown.Id, proposalYear);
            if (town is null)
                continue;

            var townState = GetOrCreateTownState(town.Id);
            if (townState.LastResolvedProposalYear >= proposalYear)
                continue;

            var proposals = GetProposals(town, proposalYear);
            if (proposals.Count == 0)
            {
                townState.LastResolvedProposalYear = proposalYear;
                continue;
            }

            var lobbies = world.Lobbies
                .Where(lobby => lobby.ProposalYear == proposalYear
                    && lobby.TownId.Equals(town.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var support = proposals.ToDictionary(
                proposal => proposal.PolicyId,
                proposal => Math.Max(0, proposal.BaseSupport
                    + lobbies.Where(lobby => lobby.PolicyId.Equals(proposal.PolicyId, StringComparison.OrdinalIgnoreCase))
                        .Sum(lobby => lobby.SupportBonus)),
                StringComparer.OrdinalIgnoreCase);
            var implementationChance = Math.Min(
                _rules.OverallImplementationChanceCap,
                support.Values.Sum());

            if (random.NextDouble() < implementationChance)
            {
                var chosen = PickWeighted(
                    proposals,
                    proposal => support[proposal.PolicyId],
                    random.NextDouble());
                townState.ActivePolicies.Add(new CommunityActivePolicyState
                {
                    PolicyId = chosen.PolicyId,
                    EnactedYear = _gameState.Year,
                    ExpiresAfterYear = _gameState.Year + Math.Max(1, chosen.DurationYears) - 1
                });

                foreach (var lobby in lobbies.Where(lobby => lobby.PolicyId.Equals(chosen.PolicyId, StringComparison.OrdinalIgnoreCase)))
                {
                    if (people.TryGetValue(lobby.ActorId, out var actor))
                    {
                        status.ApplyPersistentDelta(
                            actor,
                            _rules.EnactedExtraRenown,
                            _rules.EnactedExtraReputation,
                            "community.policy_enacted");
                    }
                }

                if (playableHeadsByTown.TryGetValue(town.Id, out var subjectId))
                {
                    events.Publish(new GameEvent
                    {
                        Type = "community.policy_enacted",
                        Year = _gameState.Year,
                        SubjectId = subjectId,
                        Data = new Dictionary<string, string>
                        {
                            ["townId"] = town.Id,
                            ["town"] = town.Town,
                            ["policyId"] = chosen.PolicyId,
                            ["policyName"] = chosen.DisplayName,
                            ["durationYears"] = chosen.DurationYears.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["text"] = $"{town.Town} adopted the local policy: {chosen.DisplayName}. It will remain in effect for {chosen.DurationYears} years."
                        }
                    });
                }
            }

            townState.LastResolvedProposalYear = proposalYear;
        }

        world.Lobbies.RemoveAll(lobby => lobby.ProposalYear <= proposalYear);
    }

    internal CommunityPolicyProposalInfo? FindProposal(
        TownInfo town,
        int proposalYear,
        string policyId) =>
        GetProposals(town, proposalYear)
            .FirstOrDefault(proposal => proposal.PolicyId.Equals(policyId, StringComparison.OrdinalIgnoreCase));

    internal bool HasLobby(Guid householdId, string townId, int proposalYear) =>
        GetWorldState(create: false)?.Lobbies.Any(lobby =>
            lobby.HouseholdId == householdId
            && lobby.ProposalYear == proposalYear
            && lobby.TownId.Equals(townId, StringComparison.OrdinalIgnoreCase)) == true;

    private bool IsEligible(
        CommunityPolicyDefinition policy,
        TownInfo town,
        int year,
        LocationOpportunitySnapshot opportunities,
        TownInstitutionSnapshot institutions,
        int prosperity,
        IReadOnlySet<string> activeIds)
    {
        if (year < policy.StartYear
            || (policy.EndYear is int endYear && year > endYear)
            || town.SettlementClass < policy.MinimumSettlementClass
            || activeIds.Contains(policy.Id))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(policy.RequiredInstitutionId)
            && institutions.GetTier(policy.RequiredInstitutionId) <= 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(policy.RequiredOpportunityTag)
            && !opportunities.TownOpportunityTags.Contains(policy.RequiredOpportunityTag, StringComparer.OrdinalIgnoreCase)
            && !opportunities.RegionOpportunityTags.Contains(policy.RequiredOpportunityTag, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (policy.RequiredProsperityMax is int maximum && prosperity > maximum)
            return false;

        if (policy.RequiredRegionIds.Count > 0 && !policy.RequiredRegionIds.Contains(town.RegionId))
            return false;

        return true;
    }

    private CommunityPolicyProposalInfo ToProposal(
        CommunityPolicyDefinition policy,
        TownInfo town,
        int year,
        int slot)
    {
        var proposer = GenerateProposer(policy, town, year, slot);
        return new CommunityPolicyProposalInfo(
            policy.Id,
            policy.DisplayName,
            policy.Description,
            policy.Rarity,
            policy.ImpactTier,
            policy.Favorability,
            policy.BaseSupport,
            policy.DurationYears,
            policy.EffectKey,
            policy.Magnitude,
            year,
            town.Id,
            proposer);
    }

    private CommunityPolicyProposerInfo GenerateProposer(
        CommunityPolicyDefinition policy,
        TownInfo town,
        int year,
        int slot)
    {
        var key = BuildKey(town.Id, year, slot, $"proposer:{policy.Id}");
        var random = new StableGameRandom(StableHash64(key));
        var archetypeId = policy.ProposerArchetypes[
            random.NextInt(0, policy.ProposerArchetypes.Count - 1)];
        var archetype = _catalog.GetArchetype(archetypeId);
        var sex = random.Chance(0.5) ? Sex.Male : Sex.Female;
        var age = random.NextInt(25, 70);
        var nationalityId = _nationalities.GenerateNationality(
            town.RegionId,
            year,
            random);
        var cultureId = _nationalities.GetNameCultureId(nationalityId);
        var first = _names.GetRandomFirstName(sex, year - age, cultureId, random);
        var surname = _names.GetRandomSurname(sex, cultureId, random);
        var wealthBand = DrawWealthBand(archetype, random);
        var proposerId = StableGuidText($"{key}|{first}|{surname}|{archetypeId}");

        return new CommunityPolicyProposerInfo(
            proposerId,
            $"{first} {surname}",
            sex,
            age,
            nationalityId,
            archetype.DisplayOccupation,
            archetype.Id,
            wealthBand,
            archetype.TypicalRenown,
            archetype.TypicalReputation);
    }

    private static string DrawWealthBand(
        CommunityConnectionArchetype archetype,
        IGameRandom random)
    {
        var min = Array.FindIndex(WealthBands, band => band.Equals(archetype.MinimumWealthBand, StringComparison.OrdinalIgnoreCase));
        var max = Array.FindIndex(WealthBands, band => band.Equals(archetype.MaximumWealthBand, StringComparison.OrdinalIgnoreCase));
        if (min < 0 || max < min)
            return archetype.MinimumWealthBand;
        return WealthBands[random.NextInt(min, max)];
    }

    private CommunityTownPolicyState? FindTownState(string townId) =>
        GetWorldState(create: false)?.Towns.FirstOrDefault(
            town => town.TownId.Equals(townId, StringComparison.OrdinalIgnoreCase));

    private CommunityTownPolicyState GetOrCreateTownState(string townId)
    {
        var world = GetWorldState(create: true)
            ?? throw new InvalidOperationException("Community state requires at least one game person.");
        var existing = world.Towns.FirstOrDefault(
            town => town.TownId.Equals(townId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var created = new CommunityTownPolicyState { TownId = townId };
        world.Towns.Add(created);
        return created;
    }

    private CommunityWorldStateComponent? GetWorldState(bool create)
    {
        var anchor = _gameState.People.FirstOrDefault();
        if (anchor is null)
            return null;
        var state = anchor.Components.Get<CommunityWorldStateComponent>();
        if (state is null && create)
        {
            state = new CommunityWorldStateComponent();
            anchor.Components.Set(state);
        }
        return state;
    }

    private string BuildKey(string townId, int year, int slot, string salt)
    {
        var anchor = _gameState.People.FirstOrDefault()?.Id.ToString("N") ?? "no-anchor";
        return $"{anchor}|{_gameState.StartYear}|{townId}|{year}|{slot}|{salt}";
    }

    private static CommunityPolicyDefinition PickWeighted(
        IReadOnlyList<CommunityPolicyDefinition> candidates,
        double unit) =>
        PickWeighted(candidates, candidate => candidate.SelectionWeight, unit);

    private static T PickWeighted<T>(
        IReadOnlyList<T> candidates,
        Func<T, double> weight,
        double unit)
    {
        var total = candidates.Sum(item => Math.Max(0, weight(item)));
        if (total <= 0)
            return candidates[0];
        var target = Math.Clamp(unit, 0, 0.9999999999999999) * total;
        var cumulative = 0.0;
        foreach (var candidate in candidates)
        {
            cumulative += Math.Max(0, weight(candidate));
            if (target < cumulative)
                return candidate;
        }
        return candidates[^1];
    }

    private static double StableUnit(string key)
    {
        var value = StableHash64(key) >> 11;
        return value * (1.0 / (1UL << 53));
    }

    private static ulong StableHash64(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private static string StableGuidText(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(bytes.AsSpan(0, 16)).ToString("N");
    }

    private static Guid ParseStableGuid(string value) =>
        Guid.TryParse(value, out var parsed)
            ? parsed
            : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    private sealed class StableGameRandom : IGameRandom
    {
        private ulong _state;

        public StableGameRandom(ulong seed) =>
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            var width = (ulong)((long)maxInclusive - minInclusive + 1L);
            return minInclusive + (int)(NextUInt64() % width);
        }

        public double NextDouble() =>
            (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        public bool Chance(double probability) =>
            NextDouble() < Math.Clamp(probability, 0, 1);

        private ulong NextUInt64()
        {
            var value = _state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            _state = value;
            return value * 2685821657736338717UL;
        }
    }
}
