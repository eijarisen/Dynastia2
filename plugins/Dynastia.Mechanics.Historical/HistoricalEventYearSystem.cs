using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

internal sealed class HistoricalEventYearSystem : IYearSystem
{
    // The authoring data intentionally describes noticeable historical shocks.
    // These gameplay dampeners keep ordinary campaigns from being dominated by
    // scheduled history while preserving severe, narrowly targeted events.
    internal const double RecurringExposureScale = 0.55;
    internal const double OneShotExposureScale = 0.85;
    internal const double RetryExposureScale = 0.70;
    internal const double WealthSeverityScale = 0.60;
    internal const double HealthSeverityScale = 0.70;
    internal const double DeathSeverityScale = 0.35;
    internal const double OrdinaryChanceScale = 0.70;
    internal const double StressSeverityScale = 0.28;

    private readonly IGamePluginContext _context;
    private readonly HistoricalEventService _service;

    public HistoricalEventYearSystem(
        IGamePluginContext context,
        HistoricalEventService service)
    {
        _context = context;
        _service = service;
    }

    public string Id => "historical.events";
    public YearPhase Phase => YearPhase.PreYear;
    public IReadOnlyCollection<string> Before => ["households.autonomous_decisions"];
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        var activeAll = _service.Catalog.Events
            .Where(item => HistoricalEventContentConfiguration.IsEnabled(item) && item.IsActive(gameState.Year))
            .ToList();
        if (activeAll.Count == 0)
            return;

        var events = Require<IGameEventBus>("Event bus");

        // Modifier-only historical events (for example the 2022 refugee
        // influx) can still be global Chronicle milestones even though they
        // do not roll direct household harm.
        PublishChronicleStarts(gameState.Year, activeAll, events);

        var active = activeAll
            .Where(item => !item.TriggerMode.Equals("modifier", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (active.Count == 0)
            return;

        var households = Require<IHouseholdService>("Household service");
        var economy = Require<IEconomyService>("Economy service");
        var family = Require<IFamilyService>("Family service");
        var nationality = Require<INationalityService>("Nationality service");
        var career = Require<ICareerService>("Career service");
        var health = Require<IHealthService>("Health service");
        var justice = Require<IJusticeService>("Justice service");
        var random = Require<IGameRandom>("Random service");
        var deaths = Require<IDeathTransitionService>("Death transition service");
        var succession = Require<ISuccessionService>("Succession service");
        var towns = Require<IHistoricalTownCatalog>("Historical town catalog");
        var farming = _context.GetService<IFarmingService>();

        var migration = new HistoricalMigrationService(
            gameState,
            economy,
            towns,
            career,
            family,
            succession,
            farming,
            random,
            events);

        foreach (var household in households.GetActiveHouseholds().ToList())
        {
            var head = gameState.People.FirstOrDefault(person => person.Id == household.HeadId);
            if (head is null
                || !head.Tags.Has("state.alive")
                || SimulationState.IsExternallyResident(head))
            {
                continue;
            }

            var place = economy.GetResidenceTown(head);
            var members = household.MemberIds
                .Select(id => gameState.People.FirstOrDefault(person => person.Id == id))
                .Where(person => person is not null && person.Tags.Has("state.alive"))
                .Cast<IPerson>()
                .ToList();
            if (members.All(person => person.Id != head.Id))
                members.Insert(0, head);

            var applicable = new List<ApplicableEvent>();
            foreach (var historicalEvent in active)
            {
                if (historicalEvent.PlayerImpactPolicy.Equals("autonomous_branches_only", StringComparison.OrdinalIgnoreCase)
                    && household.Class != HouseholdClass.Bloodline)
                {
                    continue;
                }

                var scopeMultiplier = _service.Scopes.GetMultiplier(
                    historicalEvent.ScopeId,
                    place.Id,
                    gameState.Year);
                if (scopeMultiplier <= 0)
                    continue;

                var filter = _service.Catalog.Filters[historicalEvent.TargetFilterId];
                var target = ResolveTargets(head, members, filter, nationality, family, random);
                if (!target.Eligible)
                    continue;

                if (IsHandled(target.AllMembers, historicalEvent))
                    continue;

                applicable.Add(new ApplicableEvent(historicalEvent, scopeMultiplier, filter, target));
            }

            foreach (var selected in ApplyExclusivity(applicable))
            {
                var historicalEvent = selected.Event;
                var profile = _service.Catalog.Profiles[historicalEvent.EffectProfileId];
                var householdMultiplier = ResolveHouseholdExposureMultiplier(
                    profile,
                    head,
                    selected.Target.AllMembers,
                    economy,
                    career);
                var exposure = Math.Clamp(
                    profile.HouseholdExposureChance
                    * selected.ScopeMultiplier
                    * householdMultiplier
                    * ExposureScale(historicalEvent),
                    0.0,
                    1.0);

                var impacted = random.Chance(exposure);
                if (historicalEvent.TriggerMode.Equals("one_shot", StringComparison.OrdinalIgnoreCase))
                    MarkHandled(selected.Target.AllMembers, historicalEvent);

                if (!impacted)
                    continue;

                var outcome = ApplyEffects(
                    gameState,
                    head,
                    selected,
                    profile,
                    economy,
                    career,
                    health,
                    justice,
                    family,
                    random,
                    deaths,
                    farming);

                if (historicalEvent.MigrationRouteId is { Length: > 0 }
                    && _service.Catalog.Routes.TryGetValue(historicalEvent.MigrationRouteId, out var route))
                {
                    var routeChance = route.Type.StartsWith("external", StringComparison.OrdinalIgnoreCase)
                        ? profile.ExternalDepartureChance
                        : profile.InternalRelocationChance;
                    routeChance = routeChance >= 0.75
                        ? routeChance
                        : routeChance * 0.80;

                    if (routeChance > 0 && random.Chance(Math.Clamp(routeChance, 0, 1)))
                    {
                        var forced = IsForcedDeparture(historicalEvent, route);
                        var migrationRepresentative = ResolveMigrationRepresentative(
                            head,
                            selected.Target.AllMembers,
                            economy);
                        if (migrationRepresentative is not null)
                        {
                            var migrationResult = migration.Apply(
                                migrationRepresentative,
                                historicalEvent,
                                route,
                                forced);

                            if (migrationResult.Applied)
                            {
                                outcome.Migrated = true;
                                outcome.MigrationFrom = migrationResult.FromLocation;
                                outcome.MigrationTo = migrationResult.ToLocation;
                                outcome.MigrationCashLost += migrationResult.CashLost;
                                outcome.MigrationHousesConfiscated += migrationResult.HousesConfiscated;
                                outcome.MigrationFarmlandConfiscated += migrationResult.FarmlandConfiscated;
                            }
                        }
                    }
                }

                PublishHouseholdImpact(
                    gameState.Year,
                    historicalEvent,
                    head,
                    selected.Target.AllMembers,
                    outcome,
                    events,
                    family);

                if (historicalEvent.TriggerMode.Equals("annual_until_impacted", StringComparison.OrdinalIgnoreCase))
                    MarkHandled(selected.Target.AllMembers, historicalEvent);
            }
        }
    }

    private ImpactOutcome ApplyEffects(
        IGameState state,
        IPerson head,
        ApplicableEvent selected,
        HistoricalEffectProfile profile,
        IEconomyService economy,
        ICareerService career,
        IHealthService health,
        IJusticeService justice,
        IFamilyService family,
        IGameRandom random,
        IDeathTransitionService deaths,
        IFarmingService? farming)
    {
        var outcome = new ImpactOutcome();
        var householdEffectsEnabled = !selected.Filter.HouseholdEffects.Equals("none", StringComparison.OrdinalIgnoreCase);
        var residenceTown = economy.GetResidenceTown(head);
        var communityModifiers = _context.GetService<ICommunityPolicyService>()?
            .GetModifiers(residenceTown, state.Year)
            ?? new CommunityPolicyModifierSnapshot();
        var isFlood = selected.Event.Id.Contains("flood", StringComparison.OrdinalIgnoreCase)
            || selected.Event.EffectProfileId.Contains("flood", StringComparison.OrdinalIgnoreCase);
        var floodMultiplier = isFlood
            ? communityModifiers.FloodLossMultiplier
            : 1m;
        var wealthLossMultiplier =
            communityModifiers.HistoricalWealthLossMultiplier
            * floodMultiplier;
        var healthLossMultiplier =
            communityModifiers.HistoricalHealthLossMultiplier
            * floodMultiplier;

        if (householdEffectsEnabled && profile.WealthLossPercent is { Length: >= 2 })
        {
            var snapshot = economy.GetHousehold(head);
            if (snapshot is not null && snapshot.Wealth > 0)
            {
                var fraction = Math.Min(
                    0.55,
                    RollRange(profile.WealthLossPercent, random)
                    * WealthSeverityScale
                    * (double)wealthLossMultiplier);
                var amount = Math.Round(snapshot.Wealth * (decimal)fraction, 0, MidpointRounding.AwayFromZero);
                if (amount > 0)
                {
                    economy.ChangeWealth(head, -amount);
                    outcome.WealthLost += amount;
                }
            }
        }

        if (householdEffectsEnabled && profile.JobLossChance > 0)
        {
            foreach (var member in selected.Target.AllMembers.Where(person => person.Age >= 18))
            {
                if (!career.IsEmployed(member))
                    continue;
                if (random.Chance(Math.Clamp(profile.JobLossChance * OrdinaryChanceScale, 0, 1)))
                {
                    career.SetJobLevel(member, 0);
                    outcome.JobsLost++;
                    outcome.JobLossNames.Add(family.GetDisplayName(member));
                }
            }
        }

        if (householdEffectsEnabled && profile.ImprisonmentChance > 0)
        {
            var candidates = selected.Target.PersonTargets
                .Where(person => person.Age >= 18 && !justice.IsImprisoned(person))
                .ToList();
            if (candidates.Count > 0
                && random.Chance(Math.Clamp(profile.ImprisonmentChance * OrdinaryChanceScale, 0, 1)))
            {
                var person = candidates[random.NextInt(0, candidates.Count - 1)];
                // Historical detention is applied in PreYear, while the ordinary
                // prison countdown runs later in the same year.  A minimum of
                // two years prevents a newly imposed one-year sentence from
                // being created and released in a single annual turn.
                var sentence = random.NextInt(2, profile.ImprisonmentChance >= 0.5 ? 7 : 4);
                justice.Imprison(
                    person,
                    sentence,
                    $"historical.{selected.Event.Id}",
                    selected.Event.DisplayName,
                    "Historical repression or detention.");
                outcome.Imprisoned++;
                outcome.ImprisonedNames.Add(family.GetDisplayName(person));
            }
        }

        if (householdEffectsEnabled && profile.PropertyConfiscationChance > 0
            && random.Chance(Math.Clamp(
                profile.PropertyConfiscationChance
                * 0.60
                * (double)wealthLossMultiplier,
                0,
                1)))
        {
            // A generic confiscation roll removes at most one asset.  Forced
            // relocation routes may still remove all origin property where the
            // historical content explicitly calls for it.
            var houses = economy.GetHouses(head);
            var farmland = economy.GetFarmland(head);
            var removeHouse = houses.Count > 0
                && (farmland.Count == 0 || random.Chance(0.5));
            if (removeHouse)
            {
                var removed = economy.TakeHouse(head, houses[0].Id);
                if (removed is not null)
                    outcome.PropertiesLost++;
            }
            else if (farmland.Count > 0)
            {
                var removed = economy.TakeFarmland(head, farmland[0].Id);
                if (removed is not null)
                    outcome.PropertiesLost++;
            }
        }

        if (householdEffectsEnabled
            && profile.HouseRepairCostChance > 0
            && profile.HouseRepairCostPercentOfValue is { Length: >= 2 }
            && economy.GetHouses(head).Count > 0
            && random.Chance(Math.Clamp(profile.HouseRepairCostChance * OrdinaryChanceScale, 0, 1)))
        {
            var fraction = RollRange(profile.HouseRepairCostPercentOfValue, random)
                * 0.60
                * (double)wealthLossMultiplier;
            var repair = Math.Round(economy.GetHousePrice(residenceTown) * (decimal)fraction, 0, MidpointRounding.AwayFromZero);
            if (repair > 0)
            {
                economy.ChangeWealth(head, -repair);
                outcome.PropertyDamageCost += repair;
            }
        }

        if (householdEffectsEnabled
            && profile.FarmlandLossValuePercent is { Length: >= 2 }
            && economy.GetFarmland(head).Count > 0)
        {
            var baseValue = farming?.SalePrice ?? 8000m;
            var fraction = RollRange(profile.FarmlandLossValuePercent, random)
                * 0.55
                * (double)wealthLossMultiplier;
            var loss = Math.Round(baseValue * economy.GetFarmland(head).Count * (decimal)fraction, 0, MidpointRounding.AwayFromZero);
            if (loss > 0)
            {
                economy.ChangeWealth(head, -loss);
                outcome.PropertyDamageCost += loss;
            }
        }

        foreach (var person in selected.Target.PersonTargets.ToList())
        {
            if (!person.Tags.Has("state.alive"))
                continue;

            var riskMultiplier = ResolvePersonRiskMultiplier(profile, person, family);
            if (profile.Person.DeathChance > 0
                && random.Chance(Math.Clamp(profile.Person.DeathChance * riskMultiplier * DeathSeverityScale, 0, 1)))
            {
                outcome.DeathNames.Add(family.GetDisplayName(person));
                deaths.Kill(state, person, $"historical:{selected.Event.Id}");
                outcome.Deaths++;
                continue;
            }

            if (profile.Person.InjuryChance > 0
                && profile.Person.HealthDamage is { Length: >= 2 }
                && random.Chance(Math.Clamp(profile.Person.InjuryChance * riskMultiplier * OrdinaryChanceScale, 0, 1)))
            {
                var damage = RollRange(profile.Person.HealthDamage, random)
                    * HealthSeverityScale
                    * (double)healthLossMultiplier;
                health.ChangeHealth(person, -damage);
                outcome.Injuries++;
                outcome.InjuryNames.Add(family.GetDisplayName(person));
            }
        }

        if (profile.Illness is not null)
            ApplyIllness(
                state,
                selected,
                profile.Illness,
                health,
                random,
                deaths,
                family,
                outcome,
                (double)healthLossMultiplier);

        outcome.StressGain = Math.Min(7.0, profile.StressGain * StressSeverityScale);
        return outcome;
    }

    private static void ApplyIllness(
        IGameState state,
        ApplicableEvent selected,
        HistoricalIllnessEffect illness,
        IHealthService health,
        IGameRandom random,
        IDeathTransitionService deaths,
        IFamilyService family,
        ImpactOutcome outcome,
        double healthLossMultiplier)
    {
        var candidates = selected.Target.PersonTargets
            .Where(person => person.Tags.Has("state.alive"))
            .OrderBy(_ => random.NextDouble())
            .ToList();
        if (candidates.Count == 0)
            return;

        var maximum = Math.Min(candidates.Count, Math.Max(illness.MembersMin, illness.MembersMax));
        var minimum = Math.Min(maximum, Math.Max(1, illness.MembersMin));
        var count = random.NextInt(minimum, maximum);

        foreach (var person in candidates.Take(count))
        {
            if (!random.Chance(Math.Clamp(illness.ChancePerSelectedMember * 0.80, 0, 1)))
                continue;

            if (health.HasCondition(person, illness.ConditionId))
                continue;

            var conditionAdded = false;
            try
            {
                conditionAdded = health.AddCondition(
                    person,
                    illness.ConditionId,
                    state.Year);
            }
            catch (KeyNotFoundException)
            {
                // historical_plague and historical_covid19 are intentionally
                // allowed to precede dedicated Health content.  The profile's
                // documented fallback below remains authoritative in that case.
            }

            if (conditionAdded)
            {
                outcome.Illnesses++;
                outcome.IllnessNames.Add(family.GetDisplayName(person));
                continue;
            }

            if (illness.FallbackDeathChance > 0
                && random.Chance(Math.Clamp(illness.FallbackDeathChance * DeathSeverityScale, 0, 1)))
            {
                outcome.DeathNames.Add(family.GetDisplayName(person));
                deaths.Kill(state, person, $"historical:{selected.Event.Id}");
                outcome.Deaths++;
                continue;
            }

            if (illness.FallbackHealthDamage is { Length: >= 2 })
            {
                var damage = RollRange(illness.FallbackHealthDamage, random)
                    * HealthSeverityScale
                    * healthLossMultiplier;
                health.ChangeHealth(person, -damage);
                outcome.Illnesses++;
                outcome.IllnessNames.Add(family.GetDisplayName(person));
            }
        }
    }

    private static TargetResolution ResolveTargets(
        IPerson head,
        IReadOnlyList<IPerson> members,
        HistoricalTargetFilterDefinition filter,
        INationalityService nationalities,
        IFamilyService family,
        IGameRandom random)
    {
        var nationalityIds = filter.NationalityIds?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool MatchesNationality(IPerson person)
        {
            if (nationalityIds.Count == 0)
                return true;
            try
            {
                return nationalityIds.Contains(nationalities.GetNationality(person));
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        var eligible = filter.HouseholdEligibility.ToLowerInvariant() switch
        {
            "always" => true,
            "any_matching_member" => members.Any(MatchesNationality),
            "head_or_spouse_matches" => MatchesNationality(head)
                || (family.GetSpouse(head) is { } spouse && members.Any(member => member.Id == spouse.Id) && MatchesNationality(spouse)),
            "has_member_age_range" => members.Any(person => person.Age >= (filter.MinAge ?? 0) && person.Age <= (filter.MaxAge ?? int.MaxValue)),
            _ => false
        };
        if (!eligible)
            return new TargetResolution(false, members, []);

        IReadOnlyList<IPerson> targets = filter.PersonEffects.ToLowerInvariant() switch
        {
            "matching_members" => members.Where(MatchesNationality).ToList(),
            "selected_eligible_adult" => SelectOneAdult(members, filter, random),
            _ => members
        };
        return new TargetResolution(targets.Count > 0 || filter.HouseholdEffects != "none", members, targets);
    }

    private static IReadOnlyList<IPerson> SelectOneAdult(
        IReadOnlyList<IPerson> members,
        HistoricalTargetFilterDefinition filter,
        IGameRandom random)
    {
        var eligible = members
            .Where(person => person.Age >= (filter.MinAge ?? 18) && person.Age <= (filter.MaxAge ?? int.MaxValue))
            .ToList();
        if (eligible.Count == 0)
            return [];
        return [eligible[random.NextInt(0, eligible.Count - 1)]];
    }

    private static IReadOnlyList<ApplicableEvent> ApplyExclusivity(IReadOnlyList<ApplicableEvent> events)
    {
        var result = events.Where(item => string.IsNullOrWhiteSpace(item.Event.ExclusiveGroup)).ToList();
        result.AddRange(events
            .Where(item => !string.IsNullOrWhiteSpace(item.Event.ExclusiveGroup))
            .GroupBy(item => item.Event.ExclusiveGroup!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Event.Priority).First()));
        return result.OrderByDescending(item => item.Event.Priority).ToList();
    }

    private static double ResolveHouseholdExposureMultiplier(
        HistoricalEffectProfile profile,
        IPerson head,
        IReadOnlyList<IPerson> members,
        IEconomyService economy,
        ICareerService career)
    {
        if (profile.HouseholdExposureMultipliers is null)
            return 1.0;

        var multiplier = 1.0;
        foreach (var rule in profile.HouseholdExposureMultipliers)
        {
            var matches = rule.Key switch
            {
                "owns_farmland_or_house" => economy.GetFarmland(head).Count > 0 || economy.GetHouses(head).Count > 0,
                "owns_farmland" => economy.GetFarmland(head).Count > 0,
                "high_wealth" => (economy.GetHousehold(head)?.Wealth ?? 0) >= 50000m,
                "unemployed" => members.Any(person => person.Age >= 18 && !career.IsEmployed(person)),
                "industrial_or_transport_worker" => members.Any(person =>
                    career.GetCareerFamily(person) is string family
                    && (family.Contains("Industry", StringComparison.OrdinalIgnoreCase)
                        || family.Contains("Manufactur", StringComparison.OrdinalIgnoreCase)
                        || family.Contains("Extract", StringComparison.OrdinalIgnoreCase)
                        || family.Contains("Transport", StringComparison.OrdinalIgnoreCase))),
                "age_18_29" => members.Any(person => person.Age is >= 18 and <= 29),
                _ when rule.Key.StartsWith("career_family:", StringComparison.OrdinalIgnoreCase) =>
                    members.Any(person => string.Equals(
                        career.GetCareerFamily(person),
                        rule.Key["career_family:".Length..],
                        StringComparison.OrdinalIgnoreCase)),
                _ => false
            };

            if (matches)
                multiplier *= rule.Value;
        }

        return Math.Clamp(multiplier, 0, 5);
    }

    private static double ResolvePersonRiskMultiplier(
        HistoricalEffectProfile profile,
        IPerson person,
        IFamilyService family)
    {
        if (profile.PersonRiskMultipliers is null)
            return 1.0;

        var multiplier = 1.0;
        foreach (var rule in profile.PersonRiskMultipliers)
        {
            var matches = rule.Key switch
            {
                "male_18_54" => family.GetSex(person) == Sex.Male && person.Age is >= 18 and <= 54,
                "adult" => person.Age >= 18,
                "age_70_plus" => person.Age >= 70,
                "age_55_69" => person.Age is >= 55 and <= 69,
                "age_35_54" => person.Age is >= 35 and <= 54,
                _ => false
            };
            if (matches)
                multiplier *= rule.Value;
        }
        return Math.Clamp(multiplier, 0, 8);
    }

    private void PublishChronicleStarts(
        int year,
        IReadOnlyList<HistoricalScheduledEvent> active,
        IGameEventBus events)
    {
        foreach (var historicalEvent in active.Where(item => item.ChronicleAtStart && item.StartYear == year))
        {
            var already = events.GetEventsForYear(year).Any(gameEvent =>
                gameEvent.Type.Equals("historical.milestone", StringComparison.OrdinalIgnoreCase)
                && gameEvent.Data.TryGetValue("eventId", out var eventId)
                && eventId.Equals(historicalEvent.Id, StringComparison.OrdinalIgnoreCase));
            if (already)
                continue;

            events.Publish(new GameEvent
            {
                Type = "historical.milestone",
                Year = year,
                Data = new Dictionary<string, string>
                {
                    ["eventId"] = historicalEvent.Id,
                    ["globalChronicle"] = "true",
                    ["globalNews"] = "true",
                    ["text"] = BuildGlobalNewsText(historicalEvent)
                }
            });
        }
    }

    private static string BuildGlobalNewsText(
        HistoricalScheduledEvent historicalEvent)
    {
        if (string.IsNullOrWhiteSpace(historicalEvent.GlobalNewsDescription))
            return historicalEvent.DisplayName;

        return $"{historicalEvent.DisplayName}: {historicalEvent.GlobalNewsDescription}";
    }

    private static void PublishHouseholdImpact(
        int year,
        HistoricalScheduledEvent historicalEvent,
        IPerson head,
        IReadOnlyList<IPerson> members,
        ImpactOutcome outcome,
        IGameEventBus events,
        IFamilyService family)
    {
        if (!historicalEvent.HouseholdNews)
            return;

        var sentences = new List<string>();

        if (outcome.DeathNames.Count > 0)
            sentences.Add($"{FormatNameList(outcome.DeathNames)} died.");

        if (outcome.InjuryNames.Count > 0)
            sentences.Add($"{FormatNameList(outcome.InjuryNames)} suffered injuries.");

        if (outcome.IllnessNames.Count > 0)
            sentences.Add($"{FormatNameList(outcome.IllnessNames)} became seriously ill.");

        if (outcome.JobLossNames.Count > 0)
            sentences.Add($"{FormatNameList(outcome.JobLossNames)} lost employment.");

        if (outcome.ImprisonedNames.Count > 0)
        {
            sentences.Add(outcome.ImprisonedNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                ? $"{FormatNameList(outcome.ImprisonedNames)} was detained or imprisoned."
                : $"{FormatNameList(outcome.ImprisonedNames)} were detained or imprisoned.");
        }

        if (outcome.PropertiesLost > 0)
        {
            sentences.Add(outcome.PropertiesLost == 1
                ? "One house or farmland parcel was confiscated."
                : $"{outcome.PropertiesLost} houses or farmland parcels were confiscated.");
        }

        if (outcome.MigrationHousesConfiscated > 0)
        {
            sentences.Add(outcome.MigrationHousesConfiscated == 1
                ? "One house was lost during the relocation."
                : $"{outcome.MigrationHousesConfiscated} houses were lost during the relocation.");
        }

        if (outcome.MigrationFarmlandConfiscated > 0)
        {
            sentences.Add(outcome.MigrationFarmlandConfiscated == 1
                ? "One farmland parcel was lost during the relocation."
                : $"{outcome.MigrationFarmlandConfiscated} farmland parcels were lost during the relocation.");
        }

        var financialTotal =
            outcome.WealthLost
            + outcome.PropertyDamageCost
            + outcome.MigrationCashLost;
        if (financialTotal > 0)
        {
            sentences.Add(
                $"Financial losses totaled {financialTotal:N0} zł.");
        }

        if (outcome.Migrated)
        {
            var from = string.IsNullOrWhiteSpace(outcome.MigrationFrom)
                ? "their previous home"
                : outcome.MigrationFrom;
            var to = string.IsNullOrWhiteSpace(outcome.MigrationTo)
                ? "a new destination"
                : outcome.MigrationTo;
            sentences.Add($"The household was relocated from {from} to {to}.");
        }

        if (sentences.Count == 0)
            sentences.Add("Daily life was disrupted and the household came under additional strain.");

        var surname = family.FormatSurname(head, head.Surname, family.GetSex(head));
        var text =
            $"{historicalEvent.DisplayName} directly affected the {surname} household. "
            + string.Join(" ", sentences);

        events.Publish(new GameEvent
        {
            Type = "historical.household_impact",
            Year = year,
            SubjectId = head.Id,
            RelatedPersonIds = members.Where(person => person.Id != head.Id).Select(person => person.Id).ToList(),
            Data = new Dictionary<string, string>
            {
                ["eventId"] = historicalEvent.Id,
                ["familyNews"] = "true",
                ["globalNews"] = "true",
                ["stressGain"] = outcome.StressGain.ToString("0.###", CultureInfo.InvariantCulture),
                ["financialLoss"] = financialTotal.ToString(CultureInfo.InvariantCulture),
                ["migrationFrom"] = outcome.MigrationFrom ?? string.Empty,
                ["migrationTo"] = outcome.MigrationTo ?? string.Empty,
                ["text"] = text
            }
        });
    }

    private static string FormatNameList(IReadOnlyList<string> names)
    {
        var distinct = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return distinct.Count switch
        {
            0 => "A family member",
            1 => distinct[0],
            2 => $"{distinct[0]} and {distinct[1]}",
            _ => $"{string.Join(", ", distinct.Take(distinct.Count - 1))}, and {distinct[^1]}"
        };
    }

    private static bool IsHandled(
        IReadOnlyList<IPerson> members,
        HistoricalScheduledEvent historicalEvent)
    {
        if (!historicalEvent.TriggerMode.Equals("one_shot", StringComparison.OrdinalIgnoreCase)
            && !historicalEvent.TriggerMode.Equals("annual_until_impacted", StringComparison.OrdinalIgnoreCase))
            return false;

        return members.Any(person =>
            person.Components.Get<HistoricalEventStateComponent>()?
                .HandledEvents.Contains(historicalEvent.Id) == true);
    }

    private static void MarkHandled(
        IReadOnlyList<IPerson> members,
        HistoricalScheduledEvent historicalEvent)
    {
        // The economy service can transfer household headship after death.
        // Mirror the compact handled ledger onto every current member so an
        // already-completed one-shot/retry event cannot be re-applied merely
        // because another household member becomes head on a later turn.
        foreach (var person in members)
        {
            var state = person.Components.Get<HistoricalEventStateComponent>();
            if (state is null)
            {
                state = new HistoricalEventStateComponent();
                person.Components.Set(state);
            }
            state.HandledEvents.Add(historicalEvent.Id);
        }
    }

    private static double ExposureScale(HistoricalScheduledEvent historicalEvent)
    {
        if (historicalEvent.Id.Equals("warsaw_uprising_1944", StringComparison.OrdinalIgnoreCase))
            return 1.0;

        return historicalEvent.TriggerMode.ToLowerInvariant() switch
        {
            "one_shot" => OneShotExposureScale,
            "annual_until_impacted" => RetryExposureScale,
            "annual_until_ineligible" => RetryExposureScale,
            _ => RecurringExposureScale
        };
    }

    private static IPerson? ResolveMigrationRepresentative(
        IPerson originalHead,
        IReadOnlyList<IPerson> members,
        IEconomyService economy)
    {
        if (originalHead.Tags.Has("state.alive"))
            return originalHead;

        var householdId = economy.GetHouseholdId(originalHead);
        if (householdId is null)
            return null;

        return members.FirstOrDefault(person =>
            person.Tags.Has("state.alive")
            && economy.GetHouseholdId(person) == householdId);
    }

    private static bool IsForcedDeparture(
        HistoricalScheduledEvent historicalEvent,
        HistoricalMigrationRoute route)
    {
        if (route.Type.Equals("internal_household", StringComparison.OrdinalIgnoreCase))
            return true;
        if (historicalEvent.Id.Equals("eu_labor_migration", StringComparison.OrdinalIgnoreCase)
            || historicalEvent.Id.StartsWith("post_november", StringComparison.OrdinalIgnoreCase)
            || historicalEvent.Id.StartsWith("post_january", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static double RollRange(double[] range, IGameRandom random)
    {
        if (range.Length == 0) return 0;
        if (range.Length == 1) return range[0];
        return range[0] + (range[1] - range[0]) * random.NextDouble();
    }

    private T Require<T>(string name) where T : class =>
        _context.GetService<T>()
        ?? throw new InvalidOperationException($"{name} is unavailable to Historical Events.");

    private sealed record ApplicableEvent(
        HistoricalScheduledEvent Event,
        double ScopeMultiplier,
        HistoricalTargetFilterDefinition Filter,
        TargetResolution Target);

    private sealed record TargetResolution(
        bool Eligible,
        IReadOnlyList<IPerson> AllMembers,
        IReadOnlyList<IPerson> PersonTargets);

    private sealed class ImpactOutcome
    {
        public decimal WealthLost { get; set; }
        public decimal PropertyDamageCost { get; set; }
        public decimal MigrationCashLost { get; set; }
        public int JobsLost { get; set; }
        public int Imprisoned { get; set; }
        public int PropertiesLost { get; set; }
        public int MigrationHousesConfiscated { get; set; }
        public int MigrationFarmlandConfiscated { get; set; }
        public int Injuries { get; set; }
        public int Illnesses { get; set; }
        public int Deaths { get; set; }
        public double StressGain { get; set; }
        public bool Migrated { get; set; }
        public string? MigrationFrom { get; set; }
        public string? MigrationTo { get; set; }
        public List<string> JobLossNames { get; } = [];
        public List<string> ImprisonedNames { get; } = [];
        public List<string> InjuryNames { get; } = [];
        public List<string> IllnessNames { get; } = [];
        public List<string> DeathNames { get; } = [];
    }
}
