using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

internal sealed class StandardCraftService : ICraftService, IIncomeProvider
{
    private const string ContextPath = "Crafts/craft_context_weights.csv";
    private const double EducationProgressGain = 3.0;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;
    private readonly IStatsService _stats;
    private readonly IPersonalityService _personality;
    private readonly ILocalCareerOpportunityService _localOpportunities;
    private readonly ITownProsperityService _prosperity;
    private readonly ILocalEconomicStrengthService _economicStrength;
    private readonly IWorkCapacityService _workCapacity;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly CraftCatalog _catalog;
    private readonly IContextWeightCatalog _context;

    public StandardCraftService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        ICareerService career,
        IStatsService stats,
        IPersonalityService personality,
        ILocalCareerOpportunityService localOpportunities,
        ITownProsperityService prosperity,
        ILocalEconomicStrengthService economicStrength,
        IWorkCapacityService workCapacity,
        IGameRandom random,
        IGameEventBus events,
        IContextWeightService contextWeights,
        CraftCatalog catalog)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _career = career;
        _stats = stats;
        _personality = personality;
        _localOpportunities = localOpportunities;
        _prosperity = prosperity;
        _economicStrength = economicStrength;
        _workCapacity = workCapacity;
        _random = random;
        _events = events;
        _catalog = catalog;
        _context = contextWeights.LoadCatalog(ContextPath, catalog.All.Select(craft => craft.Id));
    }

    public string Id => "crafts";

    public IReadOnlyList<CraftInfo> Catalog =>
        _catalog.All.Select(craft => _catalog.Present(craft, _gameState.Year)).ToList();

    public CraftSnapshot GetSnapshot(IPerson person)
    {
        var component = GetRequired(person);
        var active = _catalog.Find(component.ActiveCraftOccupationId);
        var presented = active is null ? null : _catalog.Present(active, _gameState.Year);
        var workYears = GetKnownCraftIds(component)
            .ToDictionary(
                id => id,
                id => GetRelevantExperienceYears(person, _catalog.Find(id)!, GetProgressState(component, id)),
                StringComparer.OrdinalIgnoreCase);

        return new CraftSnapshot(
            GetKnownCrafts(person),
            active?.Id,
            presented?.SelfEmploymentTitle,
            presented?.Emoji,
            workYears,
            GetExpectedAnnualIncome(person),
            component.LastAnnualIncome,
            component.LastIncomeYear);
    }

    public IReadOnlyList<CraftInfo> GetKnownCrafts(IPerson person)
    {
        var component = GetRequired(person);
        return GetKnownCraftIds(component)
            .Select(_catalog.Find)
            .Where(craft => craft is not null)
            .Select(craft => _catalog.Present(craft!, _gameState.Year))
            .ToList();
    }

    public bool KnowsCraft(IPerson person, string craftId)
    {
        var canonical = _catalog.CanonicalizeId(craftId);
        return canonical is not null
            && GetKnownCraftIds(GetRequired(person)).Contains(canonical, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsSelfEmployed(IPerson person) => GetActiveCraft(person) is not null;

    public CraftInfo? GetActiveCraft(IPerson person)
    {
        var active = _catalog.Find(GetRequired(person).ActiveCraftOccupationId);
        return active is null ? null : _catalog.Present(active, _gameState.Year);
    }

    public CraftProgressSnapshot? GetProgress(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null)
            return null;

        var component = GetRequired(person);
        if (!GetKnownCraftIds(component).Contains(craft.Id, StringComparer.OrdinalIgnoreCase))
            return null;

        var state = GetProgressState(component, craft.Id);
        var relevantYears = GetRelevantExperienceYears(person, craft, state);
        var totalProgress = state.ExperienceProgress + state.EducationProgress;
        var level = CraftRules.GetMasteryLevel(totalProgress, relevantYears);
        var rule = CraftRules.GetMasteryRule(level);
        var next = level < 5 ? CraftRules.GetMasteryRule(level + 1) : null;

        return new CraftProgressSnapshot(
            craft.Id,
            _catalog.ResolveDisplayName(craft.Id, _gameState.Year),
            level,
            rule.DisplayName,
            totalProgress,
            state.ExperienceProgress,
            state.EducationProgress,
            relevantYears,
            state.SelfEmploymentYears,
            state.CreditedPreLearningCareerYears,
            ApplyTownIncomeMultiplier(
                person,
                craft,
                _workCapacity
                    .GetWorkCapacity(person)
                    .Apply(GetExpectedAnnualIncome(craft, level))),
            next?.RequiredMasteryProgress,
            next?.MinimumRelevantExperienceYears);
    }

    public IReadOnlyList<CraftEducationOption> GetEducationOptions(IPerson person)
    {
        if (!person.Tags.Has("state.alive") || SimulationState.IsInactive(person))
            return [];

        var component = GetRequired(person);
        var result = new List<CraftEducationOption>();

        foreach (var craft in _catalog.All)
        {
            if (KnowsCraft(person, craft.Id))
            {
                var progress = GetProgress(person, craft.Id)!;
                if (progress.MasteryLevel >= 5
                    || !MeetsTrainingAvailability(person, craft))
                    continue;

                var stat = GetPrimaryStat(person, craft);
                result.Add(new CraftEducationOption(
                    craft.Id,
                    _catalog.ResolveDisplayName(craft.Id, _gameState.Year),
                    true,
                    progress.MasteryLevel,
                    progress.MasteryName,
                    progress.MasteryProgress,
                    progress.RelevantExperienceYears,
                    craft.PrimaryStat,
                    stat,
                    CraftRules.GetCraftImprovementChance(stat, progress.MasteryLevel)));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(component.ChosenCraftId)
                || !CanLearnChosenCraft(person, craft))
            {
                continue;
            }

            var primaryStat = GetPrimaryStat(person, craft);
            result.Add(new CraftEducationOption(
                craft.Id,
                _catalog.ResolveDisplayName(craft.Id, _gameState.Year),
                false,
                0,
                "Not learned",
                0.0,
                0,
                craft.PrimaryStat,
                primaryStat,
                CraftRules.GetNewCraftStudyChance(primaryStat)));
        }

        return result
            .OrderByDescending(option => option.IsKnownCraft)
            .ThenBy(option => option.CraftName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public CraftEducationResult StudyCraft(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null)
            return InvalidStudy(craftId, craftId, "Unknown Craft.");

        var component = GetMutable(person);
        var known = GetKnownCraftIds(component).Contains(craft.Id, StringComparer.OrdinalIgnoreCase);
        var displayName = _catalog.ResolveDisplayName(craft.Id, _gameState.Year);
        var primaryStat = GetPrimaryStat(person, craft);

        if (!known)
        {
            if (!string.IsNullOrWhiteSpace(component.ChosenCraftId)
                || !CanLearnChosenCraft(person, craft))
            {
                return InvalidStudy(craft.Id, displayName, "This Craft can no longer be learned.");
            }

            var chance = CraftRules.GetNewCraftStudyChance(primaryStat);
            var success = _random.NextDouble() < chance;
            if (!success)
            {
                PublishStudyEvent(person, craft, false, true, chance, 0, 0);
                return new CraftEducationResult(
                    true, false, true, craft.Id, displayName,
                    0, 0, 0.0, 0, chance,
                    $"The attempt to learn {displayName} was unsuccessful.");
            }

            component.ChosenCraftId = craft.Id;
            SynchronizeCraftIds(component);
            var state = GetProgressState(component, craft.Id);
            SeedPriorCareerExperience(person, craft, state);
            var progress = GetProgress(person, craft.Id)!;
            if (progress.MasteryLevel > 1)
                PublishMasteryIncrease(person, craft, 1, progress);

            PublishStudyEvent(
                person,
                craft,
                true,
                true,
                chance,
                0,
                progress.MasteryLevel);

            return new CraftEducationResult(
                true, true, true, craft.Id, displayName,
                0, progress.MasteryLevel, progress.MasteryProgress,
                progress.RelevantExperienceYears, chance,
                $"{displayName} was learned successfully.");
        }

        var before = GetProgress(person, craft.Id)!;
        if (before.MasteryLevel >= 5)
            return InvalidStudy(craft.Id, displayName, "Master Crafts cannot be improved through education.");
        if (!MeetsTrainingAvailability(person, craft))
            return InvalidStudy(craft.Id, displayName, "Craft instruction is not available in the current settlement or period.");

        var improvementChance = CraftRules.GetCraftImprovementChance(primaryStat, before.MasteryLevel);
        var improvementSuccess = _random.NextDouble() < improvementChance;
        if (improvementSuccess)
        {
            var state = GetProgressState(component, craft.Id);
            state.EducationProgress += EducationProgressGain;
        }

        var after = GetProgress(person, craft.Id)!;
        if (improvementSuccess && after.MasteryLevel > before.MasteryLevel)
            PublishMasteryIncrease(person, craft, before.MasteryLevel, after);

        PublishStudyEvent(
            person,
            craft,
            improvementSuccess,
            false,
            improvementChance,
            before.MasteryLevel,
            after.MasteryLevel);

        return new CraftEducationResult(
            true,
            improvementSuccess,
            false,
            craft.Id,
            displayName,
            before.MasteryLevel,
            after.MasteryLevel,
            after.MasteryProgress,
            after.RelevantExperienceYears,
            improvementChance,
            improvementSuccess
                ? $"Study added {EducationProgressGain:0.#} Mastery progress to {displayName}."
                : $"The {displayName} course did not improve Mastery this year.");
    }

    public bool CanLearnCraft(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null || !person.Tags.Has("state.alive") || SimulationState.IsInactive(person))
            return false;

        var component = GetRequired(person);
        if (!string.IsNullOrWhiteSpace(component.InheritedCraftId)
            || GetKnownCraftIds(component).Contains(craft.Id, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return MeetsTrainingAvailability(person, craft);
    }

    public double GetLearningWeight(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null || !CanLearnCraft(person, craft.Id))
            return 0.0;

        var location = _localOpportunities.GetOpportunitySnapshot(person);
        var stats = GetCraftStats(person);
        var temperament = _personality.GetPersonality(person)?.Temperament;
        return GetSelectionWeight(
            craft,
            _gameState.Year,
            person.Age,
            _family.GetSex(person),
            stats,
            temperament,
            location);
    }

    public bool LearnCraft(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null || !CanLearnCraft(person, craft.Id))
            return false;

        var component = GetMutable(person);
        component.InheritedCraftId = craft.Id;
        SynchronizeCraftIds(component);
        var state = GetProgressState(component, craft.Id);
        SeedPriorCareerExperience(person, craft, state);
        return true;
    }

    public void SetCrafts(IPerson person, IEnumerable<string> craftIds)
    {
        ArgumentNullException.ThrowIfNull(craftIds);
        var component = GetMutable(person);
        var ids = craftIds
            .Select(_catalog.CanonicalizeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        component.ArchivedCraftIds.AddRange(ids.Skip(CraftRules.MaximumCrafts));
        var active = ids.Take(CraftRules.MaximumCrafts).ToList();
        component.InheritedCraftId = active.Count >= 2 ? active[0] : null;
        component.ChosenCraftId = active.Count >= 2 ? active[1] : active.FirstOrDefault();
        SynchronizeCraftIds(component);

        foreach (var id in GetKnownCraftIds(component))
        {
            var craft = _catalog.Find(id);
            if (craft is null)
                continue;
            var state = GetProgressState(component, id);
            SeedPriorCareerExperience(person, craft, state);
        }
    }

    public IReadOnlyList<string> GenerateCandidateCraftIds(
        string deterministicKey,
        string? formalCareerId,
        int year,
        Sex sex,
        int age,
        int strength,
        int intellect,
        string temperament,
        TownInfo town)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deterministicKey);
        ArgumentNullException.ThrowIfNull(town);

        var location = _localOpportunities.GetOpportunitySnapshot(town);
        var tags = OpportunityTags(location);
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = strength,
            ["intellect"] = intellect
        };

        var available = _catalog.All
            .Where(craft => craft.MeetsHardAvailability(year, age, town.SettlementClass, tags))
            .Select(craft => new WeightedCraft(
                craft,
                GetSelectionWeight(craft, year, age, sex, stats, temperament, location)
                * CareerAffinityMultiplier(craft, formalCareerId)))
            .Where(item => item.Weight > 0)
            .ToList();

        if (available.Count == 0)
            return [];

        var random = new DeterministicCraftRandom(
            $"{_gameState.DynastySurname}|{deterministicKey}|candidate-crafts");

        var hasCareerLinkedCraft = !string.IsNullOrWhiteSpace(formalCareerId)
            && available.Any(item => CareerAffinityMultiplier(item.Craft, formalCareerId) > 1.0);
        var acquisitionChance = hasCareerLinkedCraft
            ? 0.70
            : CraftRules.GetGeneratedAdultBaseChance(year);

        var result = new List<string>();
        if (random.NextDouble() < acquisitionChance)
        {
            var selected = ChooseWeighted(available, random.NextDouble());
            result.Add(selected.Id);
        }

        if (result.Count > 0 && random.NextDouble() < 0.10)
        {
            var remaining = available
                .Where(item => !result.Contains(item.Craft.Id, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (remaining.Count > 0)
                result.Add(ChooseWeighted(remaining, random.NextDouble()).Id);
        }

        return result;
    }

    internal IReadOnlyList<string> GenerateCraftIdsForPerson(
        IPerson person,
        string? formalCareerId,
        int year)
    {
        _stats.EnsureStats(person);

        var location = _localOpportunities.GetOpportunitySnapshot(person);
        var stats = GetCraftStats(person);
        return GenerateCandidateCraftIds(
            person.Id.ToString("N"),
            formalCareerId,
            year,
            _family.GetSex(person),
            person.Age,
            stats["strength"],
            stats["intellect"],
            _personality.GetPersonality(person)?.Temperament ?? "Phlegmatic",
            location.Town);
    }

    public bool StartOccupation(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null
            || !person.Tags.Has("state.alive")
            || person.Age < 18
            || person.Tags.Has("state.imprisoned")
            || !KnowsCraft(person, craft.Id))
        {
            return false;
        }

        var component = GetMutable(person);
        if (component.ActiveCraftOccupationId?.Equals(craft.Id, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (!string.IsNullOrWhiteSpace(component.ActiveCraftOccupationId))
            EndOccupation(person, "switching profession");

        var career = _career.GetCareer(person);
        if (!career.IsRetired)
            _career.AssignCareer(person, null, 0, career.JobSatisfaction);

        component.ActiveCraftOccupationId = craft.Id;
        person.Tags.Add("career.craft_self_employed");
        person.Tags.Add("employment.craft");

        var displayName = _catalog.ResolveDisplayName(craft.Id, _gameState.Year);
        var progress = GetProgress(person, craft.Id);
        _events.Publish(new GameEvent
        {
            Type = "craft.self_employment_started",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = craft.Id,
                ["craftName"] = displayName,
                ["mastery"] = progress?.MasteryName ?? "Novice",
                ["text"] =
                    $"At age {person.Age}, {_family.GetDisplayName(person)} began earning a living as a self-employed {craft.SelfEmploymentTitle.ToLowerInvariant()}."
            }
        });

        return true;
    }

    public bool EndOccupation(IPerson person, string reason = "ended")
    {
        var component = GetMutable(person);
        var craft = _catalog.Find(component.ActiveCraftOccupationId);
        if (craft is null)
        {
            component.ActiveCraftOccupationId = null;
            person.Tags.Remove("career.craft_self_employed");
            person.Tags.Remove("employment.craft");
            return false;
        }

        component.ActiveCraftOccupationId = null;
        person.Tags.Remove("career.craft_self_employed");
        person.Tags.Remove("employment.craft");

        var displayName = _catalog.ResolveDisplayName(craft.Id, _gameState.Year);
        _events.Publish(new GameEvent
        {
            Type = "craft.self_employment_ended",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = craft.Id,
                ["craftName"] = displayName,
                ["reason"] = reason,
                ["text"] = reason.Equals("formal employment", StringComparison.OrdinalIgnoreCase)
                    ? $"{_family.GetDisplayName(person)} left self-employment in {displayName} to take a formal position."
                    : reason.Equals("switching profession", StringComparison.OrdinalIgnoreCase)
                        ? $"{_family.GetDisplayName(person)} stopped practicing {displayName} as a profession to work in another Craft."
                        : $"{_family.GetDisplayName(person)} ended self-employment in {displayName}."
            }
        });

        return true;
    }

    public double GetApplicationBonus(IPerson person, string careerId) =>
        CraftRules.GetApplicationBonus(GetKnownCrafts(person), careerId);

    public CraftCareerExperience GetCareerExperience(IPerson person, string careerId)
    {
        var exact = 0;
        var related = 0;

        foreach (var craft in GetKnownCrafts(person))
        {
            var progress = GetProgress(person, craft.Id);
            if (progress is null || progress.SelfEmploymentYears <= 0)
                continue;

            if (craft.PrimaryCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase))
                exact += progress.SelfEmploymentYears;
            else if (craft.SecondaryCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase))
                related += progress.SelfEmploymentYears;
        }

        return new CraftCareerExperience(exact, related);
    }

    public decimal GetExpectedAnnualIncome(IPerson person)
    {
        var active = GetActiveCraft(person);
        if (active is null)
            return 0m;

        var progress = GetProgress(person, active.Id);
        if (progress is null)
            return 0m;

        var income = GetExpectedAnnualIncome(active, progress.MasteryLevel);
        var recoverReduction = ReadPercent(person, "modifier.salary.recover.");
        if (recoverReduction > 0m)
        {
            income = Math.Round(
                income * (1m - recoverReduction / 100m),
                0,
                MidpointRounding.AwayFromZero);
        }

        income = _workCapacity
            .GetWorkCapacity(person)
            .Apply(income);

        return ApplyTownIncomeMultiplier(
            person,
            active,
            income);
    }

    public decimal GetAnnualIncome(IPerson person)
    {
        var component = GetMutable(person);
        var active = _catalog.Find(component.ActiveCraftOccupationId);
        if (active is null)
            return 0m;

        if (!person.Tags.Has("state.alive") || person.Tags.Has("state.imprisoned"))
        {
            component.LastAnnualIncome = 0m;
            component.LastIncomeYear = _gameState.Year;
            return 0m;
        }

        var progress = GetProgress(person, active.Id)!;
        var baseSalary = active.BaseSalary;
        var randomRoll = _random.NextInt(0, 94);
        var income = CraftRules.CalculateAnnualIncome(
            baseSalary,
            progress.MasteryLevel,
            randomRoll);

        var recoverReduction = ReadPercent(person, "modifier.salary.recover.");
        if (recoverReduction > 0m)
        {
            income = Math.Round(
                income * (1m - recoverReduction / 100m),
                0,
                MidpointRounding.AwayFromZero);
        }

        income = _workCapacity
            .GetWorkCapacity(person)
            .Apply(income);

        income = ApplyTownIncomeMultiplier(person, active, income);

        component.LastAnnualIncome = income;
        component.LastIncomeYear = _gameState.Year;

        _events.Publish(new GameEvent
        {
            Type = "craft.income",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = active.Id,
                ["craftName"] = _catalog.ResolveDisplayName(active.Id, _gameState.Year),
                ["mastery"] = progress.MasteryName,
                ["amount"] = income.ToString(CultureInfo.InvariantCulture),
                ["expected"] = GetExpectedAnnualIncome(person).ToString(CultureInfo.InvariantCulture),
                ["baseSalary"] = baseSalary.ToString(CultureInfo.InvariantCulture),
                ["randomRoll"] = randomRoll.ToString(CultureInfo.InvariantCulture),
                ["suppressChronicle"] = "true"
            }
        });

        if (randomRoll == 94
            && progress.MasteryLevel >= 5)
        {
            var commissionValue = ApplyTownIncomeMultiplier(
                person,
                active,
                _workCapacity
                    .GetWorkCapacity(person)
                    .Apply(CraftRules.CalculateAnnualIncome(
                        baseSalary,
                        progress.MasteryLevel,
                        94)));

            _events.Publish(new GameEvent
            {
                Type = "craft.major_commission",
                Year = _gameState.Year,
                SubjectId = person.Id,
                Data = new Dictionary<string, string>
                {
                    ["craftId"] = active.Id,
                    ["craftName"] = _catalog.ResolveDisplayName(active.Id, _gameState.Year),
                    ["count"] = "1",
                    ["commissionValue"] = commissionValue.ToString(CultureInfo.InvariantCulture),
                    ["text"] = $"{_family.GetDisplayName(person)} secured a major {active.Name} commission worth {commissionValue:N0} zł."
                }
            });
        }

        return income;
    }

    decimal IIncomeProvider.GetExpectedAnnualIncome(IPerson person) =>
        GetExpectedAnnualIncome(person);

    internal void RecordWorkYear(IPerson person)
    {
        if (!person.Tags.Has("state.alive")
            || person.Tags.Has("state.imprisoned")
            || !_workCapacity.GetWorkCapacity(person).CanWork)
        {
            return;
        }

        var component = GetMutable(person);
        var activeCraftId = _catalog.CanonicalizeId(component.ActiveCraftOccupationId);
        var career = _career.GetCareer(person);
        var formalCareerId = !career.IsRetired && career.JobLevel > 0
            ? career.CareerId
            : null;

        foreach (var id in GetKnownCraftIds(component))
        {
            var craft = _catalog.Find(id);
            if (craft is null)
                continue;

            var isActiveCraft = activeCraftId?.Equals(craft.Id, StringComparison.OrdinalIgnoreCase) == true;
            var isRelatedCareer = !string.IsNullOrWhiteSpace(formalCareerId)
                && IsCareerRelevant(craft, formalCareerId!);
            if (!isActiveCraft && !isRelatedCareer)
                continue;

            var state = GetProgressState(component, craft.Id);
            var before = GetProgress(person, craft.Id)!;
            state.ExperienceProgress += CraftRules.GetExperienceProgressGain(GetPrimaryStat(person, craft));

            if (isActiveCraft)
            {
                state.SelfEmploymentYears++;
                component.CraftWorkYearsByCraft[craft.Id] = state.SelfEmploymentYears;
            }

            var after = GetProgress(person, craft.Id)!;
            if (after.MasteryLevel > before.MasteryLevel)
                PublishMasteryIncrease(person, craft, before.MasteryLevel, after);
        }
    }

    private decimal GetExpectedAnnualIncome(
        CraftInfo craft,
        int masteryLevel) =>
        CraftRules.GetExpectedAnnualIncome(
            craft.BaseSalary,
            masteryLevel);

    private decimal ApplyTownIncomeMultiplier(
        IPerson person,
        CraftInfo craft,
        decimal income)
    {
        if (income <= 0m)
            return 0m;

        var town = _localOpportunities.GetOpportunitySnapshot(person).Town;
        var strength = _economicStrength.ResolveCraft(town, craft);
        var multiplier = _prosperity.GetIncomeMultiplier(town, strength);
        return Math.Round(
            income * multiplier,
            0,
            MidpointRounding.AwayFromZero);
    }

    private bool CanLearnChosenCraft(IPerson person, CraftInfo craft)
    {
        var component = GetRequired(person);
        return string.IsNullOrWhiteSpace(component.ChosenCraftId)
            && !GetKnownCraftIds(component).Contains(craft.Id, StringComparer.OrdinalIgnoreCase)
            && MeetsTrainingAvailability(person, craft);
    }

    private bool MeetsTrainingAvailability(IPerson person, CraftInfo craft)
    {
        if (!person.Tags.Has("state.alive") || SimulationState.IsInactive(person))
            return false;

        var location = _localOpportunities.GetOpportunitySnapshot(person);
        return craft.MeetsHardAvailability(
            _gameState.Year,
            person.Age,
            location.Town.SettlementClass,
            OpportunityTags(location));
    }

    private void SeedPriorCareerExperience(
        IPerson person,
        CraftInfo craft,
        CraftProgressState state)
    {
        if (state.PriorCareerExperienceSeeded)
            return;

        var years = GetRelevantCareerYears(person, craft);
        state.CreditedPreLearningCareerYears = years;
        state.ExperienceProgress +=
            years * CraftRules.GetExperienceProgressGain(GetPrimaryStat(person, craft));
        state.PriorCareerExperienceSeeded = true;
    }

    private int GetRelevantExperienceYears(
        IPerson person,
        CraftInfo craft,
        CraftProgressState state) =>
        state.SelfEmploymentYears + GetRelevantCareerYears(person, craft);

    private int GetRelevantCareerYears(IPerson person, CraftInfo craft) =>
        _career.GetExperienceYearsByCareer(person)
            .Where(pair => pair.Value > 0 && IsCareerRelevant(craft, pair.Key))
            .Sum(pair => pair.Value);

    private static bool IsCareerRelevant(CraftInfo craft, string careerId) =>
        craft.PrimaryCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase)
        || craft.SecondaryCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase);

    private int GetPrimaryStat(IPerson person, CraftInfo craft) =>
        _stats.GetStats(person)
            .FirstOrDefault(stat => stat.Id.Equals(craft.PrimaryStat, StringComparison.OrdinalIgnoreCase))?.Value
        ?? 3;

    private void PublishMasteryIncrease(
        IPerson person,
        CraftInfo craft,
        int previousLevel,
        CraftProgressSnapshot progress)
    {
        if (progress.MasteryLevel <= previousLevel)
            return;

        var isMaster = progress.MasteryLevel >= 5;
        _events.Publish(new GameEvent
        {
            Type = isMaster ? "craft.became_master" : "craft.mastery_increased",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = craft.Id,
                ["craftName"] = progress.CraftName,
                ["previousLevel"] = previousLevel.ToString(CultureInfo.InvariantCulture),
                ["level"] = progress.MasteryLevel.ToString(CultureInfo.InvariantCulture),
                ["mastery"] = progress.MasteryName,
                ["progress"] = progress.MasteryProgress.ToString("0.##", CultureInfo.InvariantCulture),
                ["experienceYears"] = progress.RelevantExperienceYears.ToString(CultureInfo.InvariantCulture),
                ["text"] = isMaster
                    ? $"{_family.GetDisplayName(person)} became a Master of {progress.CraftName} after {progress.RelevantExperienceYears} years of relevant professional experience."
                    : $"{_family.GetDisplayName(person)} advanced to {progress.MasteryName} in {progress.CraftName}."
            }
        });
    }

    private void PublishStudyEvent(
        IPerson person,
        CraftInfo craft,
        bool success,
        bool learningNewCraft,
        double chance,
        int previousLevel,
        int currentLevel)
    {
        var displayName = _catalog.ResolveDisplayName(craft.Id, _gameState.Year);
        _events.Publish(new GameEvent
        {
            Type = learningNewCraft
                ? success ? "craft.learned" : "craft.education_failed"
                : success ? "craft.education_success" : "craft.education_failed",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = craft.Id,
                ["craftName"] = displayName,
                ["learningMode"] = "education",
                ["chance"] = chance.ToString("0.00", CultureInfo.InvariantCulture),
                ["previousLevel"] = previousLevel.ToString(CultureInfo.InvariantCulture),
                ["level"] = currentLevel.ToString(CultureInfo.InvariantCulture),
                ["suppressChronicle"] = success ? "false" : "true",
                ["text"] = learningNewCraft
                    ? success
                        ? $"{_family.GetDisplayName(person)} successfully learned {displayName} through formal instruction."
                        : $"{_family.GetDisplayName(person)} studied {displayName}, but did not learn the Craft."
                    : success
                        ? $"{_family.GetDisplayName(person)} completed advanced study in {displayName}."
                        : $"{_family.GetDisplayName(person)} studied {displayName}, but made no lasting Mastery progress."
            }
        });
    }

    private static CraftEducationResult InvalidStudy(string craftId, string craftName, string message) =>
        new(false, false, false, craftId, craftName, 0, 0, 0.0, 0, 0.0, message);

    private double GetSelectionWeight(
        CraftInfo craft,
        int year,
        int age,
        Sex sex,
        IReadOnlyDictionary<string, int> stats,
        string? temperament,
        LocationOpportunitySnapshot location)
    {
        var tags = OpportunityTags(location);
        if (!craft.MeetsHardAvailability(year, age, location.Town.SettlementClass, tags))
            return 0.0;

        var context = new ContextWeightContext(
            year,
            age,
            sex,
            temperament,
            SettlementClass: location.Town.SettlementClass);

        return craft.BaseWeight
            * CraftRules.TownMultiplier(craft.TownPreference, location.Town.SettlementClass)
            * CraftRules.GetStatSelectionMultiplier(craft, stats)
            * CraftRules.PreferredOpportunityMultiplier(craft, tags)
            * _context.GetMultiplier(craft.Id, context);
    }

    private static double CareerAffinityMultiplier(CraftInfo craft, string? formalCareerId)
    {
        if (string.IsNullOrWhiteSpace(formalCareerId))
            return 1.0;
        if (craft.PrimaryCareerIds.Contains(formalCareerId, StringComparer.OrdinalIgnoreCase))
            return 3.0;
        if (craft.SecondaryCareerIds.Contains(formalCareerId, StringComparer.OrdinalIgnoreCase))
            return 1.75;
        return 1.0;
    }

    private static CraftInfo ChooseWeighted(IReadOnlyList<WeightedCraft> choices, double unitRoll)
    {
        var total = choices.Sum(choice => choice.Weight);
        var target = unitRoll * total;
        var cumulative = 0.0;
        foreach (var choice in choices)
        {
            cumulative += choice.Weight;
            if (target <= cumulative)
                return choice.Craft;
        }
        return choices[^1].Craft;
    }

    private IReadOnlyDictionary<string, int> GetCraftStats(IPerson person) =>
        _stats.GetStats(person)
            .Where(stat => stat.Id is "strength" or "intellect")
            .ToDictionary(stat => stat.Id, stat => stat.Value, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> OpportunityTags(LocationOpportunitySnapshot location) =>
        location.RegionOpportunityTags
            .Concat(location.TownOpportunityTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static decimal ReadPercent(IPerson person, string prefix)
    {
        foreach (var tag in person.Tags.All)
        {
            if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (decimal.TryParse(tag[prefix.Length..], NumberStyles.Number, CultureInfo.InvariantCulture, out var percent))
                return Math.Clamp(percent, 0m, 50m);
        }
        return 0m;
    }

    internal void ReconcileAll()
    {
        foreach (var person in _gameState.People)
            ReconcilePerson(person);
    }

    internal void ReconcilePerson(IPerson person)
    {
        var component = person.Components.Get<CraftComponent>();
        if (component is null)
        {
            component = new CraftComponent();
            person.Components.Set(component);
        }

        NormalizeAndMigrate(person, component);
    }

    private CraftComponent GetRequired(IPerson person) =>
        person.Components.Get<CraftComponent>()
        ?? throw new InvalidOperationException(
            "Craft state is missing. Run state reconciliation before reading it.");

    private CraftComponent GetMutable(IPerson person)
    {
        ReconcilePerson(person);
        return GetRequired(person);
    }

    private void NormalizeAndMigrate(IPerson person, CraftComponent component)
    {
        component.CraftIds ??= [];
        component.ArchivedCraftIds ??= [];
        component.CraftWorkYearsByCraft ??=
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        component.ProgressByCraft ??=
            new Dictionary<string, CraftProgressState>(StringComparer.OrdinalIgnoreCase);

        var legacyIds = component.CraftIds
            .Select(_catalog.CanonicalizeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        component.InheritedCraftId = _catalog.CanonicalizeId(component.InheritedCraftId);
        component.ChosenCraftId = _catalog.CanonicalizeId(component.ChosenCraftId);

        if (legacyIds.Count > 0)
        {
            var unassigned = legacyIds
                .Where(id => !id.Equals(component.InheritedCraftId, StringComparison.OrdinalIgnoreCase)
                    && !id.Equals(component.ChosenCraftId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (string.IsNullOrWhiteSpace(component.InheritedCraftId))
            {
                var inherited = unassigned.FirstOrDefault(id =>
                    WasLearnedFromParent(person, id));
                if (!string.IsNullOrWhiteSpace(inherited))
                {
                    component.InheritedCraftId = inherited;
                    unassigned.RemoveAll(id => id.Equals(inherited, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (string.IsNullOrWhiteSpace(component.InheritedCraftId)
                && !string.IsNullOrWhiteSpace(component.ChosenCraftId)
                && unassigned.Count > 0)
            {
                component.InheritedCraftId = unassigned[0];
                unassigned.RemoveAt(0);
            }

            if (string.IsNullOrWhiteSpace(component.InheritedCraftId)
                && string.IsNullOrWhiteSpace(component.ChosenCraftId)
                && unassigned.Count >= 2)
            {
                // Older generated adults did not record Craft origin. When two
                // Crafts exist, preserve both by mapping them into the two new
                // non-replaceable slots rather than silently dropping one.
                component.InheritedCraftId = unassigned[0];
                component.ChosenCraftId = unassigned[1];
                unassigned.RemoveRange(0, 2);
            }
            else if (string.IsNullOrWhiteSpace(component.ChosenCraftId)
                && unassigned.Count > 0)
            {
                // A single legacy Craft with no parental learning record is
                // treated as chosen. This keeps the inherited slot reserved
                // for actual parental transmission.
                component.ChosenCraftId = unassigned[0];
                unassigned.RemoveAt(0);
            }

            component.ArchivedCraftIds.AddRange(unassigned);
        }

        component.ArchivedCraftIds = component.ArchivedCraftIds
            .Select(_catalog.CanonicalizeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Where(id => !id.Equals(component.InheritedCraftId, StringComparison.OrdinalIgnoreCase)
                && !id.Equals(component.ChosenCraftId, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        SynchronizeCraftIds(component);

        var normalizedLegacyYears = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in component.CraftWorkYearsByCraft)
        {
            var canonical = _catalog.CanonicalizeId(pair.Key);
            if (canonical is null || pair.Value <= 0)
                continue;
            normalizedLegacyYears.TryGetValue(canonical, out var existing);
            normalizedLegacyYears[canonical] = Math.Max(existing, pair.Value);
        }
        component.CraftWorkYearsByCraft = normalizedLegacyYears;

        var normalizedProgress = new Dictionary<string, CraftProgressState>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in component.ProgressByCraft)
        {
            var canonical = _catalog.CanonicalizeId(pair.Key)
                ?? _catalog.CanonicalizeId(pair.Value?.CraftId);
            if (canonical is null || pair.Value is null)
                continue;
            pair.Value.CraftId = canonical;
            normalizedProgress[canonical] = pair.Value;
        }
        component.ProgressByCraft = normalizedProgress;

        foreach (var id in GetKnownCraftIds(component))
            _ = GetProgressState(component, id);

        if (!component.VocationMigrationCompleted)
        {
            foreach (var id in GetKnownCraftIds(component))
            {
                var craft = _catalog.Find(id);
                if (craft is null)
                    continue;

                var state = GetProgressState(component, id);
                if (component.CraftWorkYearsByCraft.TryGetValue(id, out var legacyYears))
                {
                    state.SelfEmploymentYears = Math.Max(state.SelfEmploymentYears, legacyYears);
                    if (state.ExperienceProgress <= 0.0000001 && legacyYears > 0)
                    {
                        state.ExperienceProgress +=
                            legacyYears * CraftRules.GetExperienceProgressGain(GetPrimaryStat(person, craft));
                    }
                }

                SeedPriorCareerExperience(person, craft, state);
            }

            component.VocationMigrationCompleted = true;
        }

        var active = _catalog.CanonicalizeId(component.ActiveCraftOccupationId);
        component.ActiveCraftOccupationId = active;
        if (active is null || !GetKnownCraftIds(component).Contains(active, StringComparer.OrdinalIgnoreCase))
        {
            component.ActiveCraftOccupationId = null;
            person.Tags.Remove("career.craft_self_employed");
            person.Tags.Remove("employment.craft");
        }
        else
        {
            person.Tags.Add("career.craft_self_employed");
            person.Tags.Add("employment.craft");
        }
    }


    private bool WasLearnedFromParent(IPerson person, string craftId) =>
        _events.AllEvents.Any(gameEvent =>
            gameEvent.SubjectId == person.Id
            && gameEvent.Type.Equals("craft.learned", StringComparison.OrdinalIgnoreCase)
            && gameEvent.Data.TryGetValue("craftId", out var learnedCraftId)
            && learnedCraftId.Equals(craftId, StringComparison.OrdinalIgnoreCase)
            && gameEvent.Data.TryGetValue("learningMode", out var learningMode)
            && (learningMode.Equals("passive", StringComparison.OrdinalIgnoreCase)
                || learningMode.Equals("taught", StringComparison.OrdinalIgnoreCase)));

    private static IReadOnlyList<string> GetKnownCraftIds(CraftComponent component) =>
        new[] { component.InheritedCraftId, component.ChosenCraftId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void SynchronizeCraftIds(CraftComponent component)
    {
        component.CraftIds = GetKnownCraftIds(component).ToList();
    }

    private static CraftProgressState GetProgressState(CraftComponent component, string craftId)
    {
        if (!component.ProgressByCraft.TryGetValue(craftId, out var state))
        {
            state = new CraftProgressState { CraftId = craftId };
            component.ProgressByCraft[craftId] = state;
        }

        state.CraftId = craftId;
        state.ExperienceProgress = Math.Max(0.0, state.ExperienceProgress);
        state.EducationProgress = Math.Max(0.0, state.EducationProgress);
        state.SelfEmploymentYears = Math.Max(0, state.SelfEmploymentYears);
        state.CreditedPreLearningCareerYears = Math.Max(0, state.CreditedPreLearningCareerYears);
        return state;
    }

    private sealed record WeightedCraft(CraftInfo Craft, double Weight);
}
