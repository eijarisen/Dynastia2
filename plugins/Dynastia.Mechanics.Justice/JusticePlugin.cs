using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed partial class JusticePlugin : IGamePlugin
{
    private const string CrimesPath = "Common/crimes.json";
    private const string AttemptRulesPath = "Justice/crime_attempt_rules.json";
    private const string AttemptContextPath = "Justice/crime_attempt_context_weights.csv";
    private const string CrimeContextPath = "Justice/crime_context_weights.csv";
    private const string OpportunityTagsPath = "Towns/opportunity_tags.csv";

    private static readonly HashSet<string> AllowedStats =
        new(["strength", "intellect", "appeal"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedSettlementPreferences =
        new(["Universal", "Rural", "Urban"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedBehaviorTags =
        new(new[]
        {
            "profit", "property", "financial", "planned", "violent", "impulsive",
            "organized", "technology", "severe", "extreme", "employment", "rural"
        }, StringComparer.OrdinalIgnoreCase);

    public void Initialize(IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
        var family = context.GetService<IFamilyService>() ?? throw new InvalidOperationException("Family service is unavailable.");
        var personality = context.GetService<IPersonalityService>() ?? throw new InvalidOperationException("Personality service is unavailable.");
        var stats = context.GetService<IStatsService>() ?? throw new InvalidOperationException("Stats service is unavailable.");
        var career = context.GetService<ICareerService>() ?? throw new InvalidOperationException("Career service is unavailable.");
        var economy = context.GetService<IEconomyService>() ?? throw new InvalidOperationException("Economy service is unavailable.");
        var economyBalance = context.GetService<IEconomyBalanceService>() ?? throw new InvalidOperationException("Economy balance service is unavailable.");
        var localOpportunities = context.GetService<ILocalCareerOpportunityService>() ?? throw new InvalidOperationException("Local opportunity service is unavailable.");
        var contextWeights = context.GetService<IContextWeightService>() ?? throw new InvalidOperationException("Context-weight service is unavailable.");
        var data = context.GetService<IGameDataService>() ?? throw new InvalidOperationException("Game data service is unavailable.");
        var random = context.GetService<IGameRandom>() ?? throw new InvalidOperationException("Random service is unavailable.");
        var events = context.GetService<IGameEventBus>() ?? throw new InvalidOperationException("Game event bus is unavailable.");
        var systems = context.GetService<IYearSystemRegistry>() ?? throw new InvalidOperationException("Year system registry is unavailable.");
        var actions = context.GetService<IActionRegistry>() ?? throw new InvalidOperationException("Action registry is unavailable.");
        var income = context.GetService<IIncomeProviderRegistry>() ?? throw new InvalidOperationException("Income provider registry is unavailable.");
        var guards = context.GetService<IActionGuardRegistry>() ?? throw new InvalidOperationException("Action guard registry is unavailable.");
        var stressModifiers = context.GetService<IStressModifierRegistry>() ?? throw new InvalidOperationException("Stress modifier registry is unavailable.");
        var stress = context.GetService<IStressService>();
        var workCapacity = context.GetService<IWorkCapacityService>()
            ?? throw new InvalidOperationException("Work capacity service is unavailable.");

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var crimes = CatalogValidation.DeserializeJson<List<CrimeDefinition>>(
            data,
            CrimesPath,
            jsonOptions);

        var attemptRules = CatalogValidation.DeserializeJson<CrimeAttemptRules>(
            data,
            AttemptRulesPath,
            jsonOptions);
        attemptRules.Validate(AttemptRulesPath);

        ValidateCrimes(
            crimes,
            ParseOpportunityTags(data.ReadText(OpportunityTagsPath)),
            career.GetKnownCareerFamilies());

        var historicalCrimes = CrimeHistoricalCatalog.Load(
            data,
            crimes.Select(crime => crime.Id));
        var criminalCatalog = CriminalOccupationCatalog.Load(data);
        var courtRules = CourtJusticeRules.Load(data);
        var attemptContext = contextWeights.LoadGlobalCatalog(AttemptContextPath);
        var crimeContext = contextWeights.LoadCatalog(
            CrimeContextPath,
            crimes.Select(crime => crime.Id));

        var justice = new StandardJusticeService(
            context.GetService<IGameState>()!,
            family,
            career,
            courtRules,
            () => context.GetService<IFamilyRelationService>());
        var criminalOccupation = new CriminalOccupationService(
            context.GetService<IGameState>()!,
            family,
            stats,
            career,
            economy,
            justice,
            random,
            events,
            criminalCatalog,
            crimes,
            historicalCrimes,
            () => context.GetService<ICraftService>(),
            workCapacity);
        context.AddService<IJusticeService>(justice);
        context.AddService<ICriminalOccupationService>(criminalOccupation);
        income.Register(criminalOccupation);

        context.GetService<IStateReconciliationLifecycle>()?
            .Register(
                "justice.components",
                Enum.GetValues<ReconciliationLifecycleStage>(),
                _ =>
                {
                    var people = context.GetService<IGameState>()!.People;
                    justice.ReconcileAll(people);
                    criminalOccupation.ReconcileAll(people);
                },
                order: 42);

        stressModifiers.Register(new JusticeStressModifierProvider(justice));
        guards.Register(new PrisonActionGuard(justice));
        RegisterCriminalOccupationActions(
            actions,
            criminalOccupation,
            family,
            economy,
            personality,
            random,
            events,
            criminalCatalog.Rules);
        RegisterCourtActions(
            actions,
            justice,
            criminalOccupation,
            family,
            economy,
            stats,
            courtRules);
        systems.Register(new PrisonStatusYearSystem(justice, family, events));
        systems.Register(new CriminalOccupationYearSystem(criminalOccupation));
        systems.Register(new CrimeYearSystem(
            justice,
            family,
            stats,
            career,
            economy,
            economyBalance,
            localOpportunities,
            stress,
            random,
            events,
            crimes,
            historicalCrimes,
            attemptRules,
            attemptContext,
            crimeContext));
        context.Log("Justice mechanics registered.");
    }

    private static void ValidateCrimes(
        IReadOnlyList<CrimeDefinition> crimes,
        IReadOnlySet<string> knownOpportunityTags,
        IReadOnlyCollection<string> knownCareerFamilies)
    {
        if (crimes.Count == 0)
            throw CatalogValidation.Error(CrimesPath, "at least one crime definition", field: "Root", value: crimes.Count);

        var families = knownCareerFamilies.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < crimes.Count; index++)
        {
            var crime = crimes[index];
            var item = string.IsNullOrWhiteSpace(crime.Id) ? $"index {index}" : crime.Id;

            if (string.IsNullOrWhiteSpace(crime.Id))
                throw CatalogValidation.Error(CrimesPath, "a non-empty crime ID", item: item, field: "id", value: crime.Id);
            if (string.IsNullOrWhiteSpace(crime.Name))
                throw CatalogValidation.Error(CrimesPath, "a non-empty display name", item: item, field: "name", value: crime.Name);
            if (string.IsNullOrWhiteSpace(crime.Category))
                throw CatalogValidation.Error(CrimesPath, "a non-empty category", item: item, field: "category", value: crime.Category);
            if (string.IsNullOrWhiteSpace(crime.Description))
                throw CatalogValidation.Error(CrimesPath, "a non-empty description", item: item, field: "description", value: crime.Description);
            if (!ids.TryAdd(crime.Id, index))
                throw CatalogValidation.Error(CrimesPath, $"a unique ID; first defined at item index {ids[crime.Id]}", item: crime.Id, field: "id", value: crime.Id);

            if (crime.StartYear < GameCalendarConfiguration.GameStartYear)
                throw CatalogValidation.Error(CrimesPath, $"a year at or after {GameCalendarConfiguration.GameStartYear}", item: crime.Id, field: "startYear", value: crime.StartYear);
            if (crime.EndYear is int endYear && endYear < crime.StartYear)
                throw CatalogValidation.Error(CrimesPath, $"a year at or after startYear ({crime.StartYear})", item: crime.Id, field: "endYear", value: endYear);
            if (crime.MinimumAge < 18)
                throw CatalogValidation.Error(CrimesPath, "an age of at least 18", item: crime.Id, field: "minimumAge", value: crime.MinimumAge);
            if (crime.MaximumAge is int maxAge && maxAge < crime.MinimumAge)
                throw CatalogValidation.Error(CrimesPath, $"an age at or above minimumAge ({crime.MinimumAge})", item: crime.Id, field: "maximumAge", value: maxAge);

            if (!AllowedStats.Contains(crime.PrimaryStat))
                throw CatalogValidation.Error(CrimesPath, $"one of: {string.Join(", ", AllowedStats)}", item: crime.Id, field: "primaryStat", value: crime.PrimaryStat);
            if (crime.SecondaryStat is not null && !AllowedStats.Contains(crime.SecondaryStat))
                throw CatalogValidation.Error(CrimesPath, $"one of: {string.Join(", ", AllowedStats)}, or null", item: crime.Id, field: "secondaryStat", value: crime.SecondaryStat);
            if (crime.SecondaryStat?.Equals(crime.PrimaryStat, StringComparison.OrdinalIgnoreCase) == true)
                throw CatalogValidation.Error(CrimesPath, "a stat different from primaryStat", item: crime.Id, field: "secondaryStat", value: crime.SecondaryStat);
            if (!AllowedSettlementPreferences.Contains(crime.SettlementPreference))
                throw CatalogValidation.Error(CrimesPath, $"one of: {string.Join(", ", AllowedSettlementPreferences)}", item: crime.Id, field: "settlementPreference", value: crime.SettlementPreference);

            var unknownBehaviorTag = crime.BehaviorTags.FirstOrDefault(tag => !AllowedBehaviorTags.Contains(tag));
            if (unknownBehaviorTag is not null)
                throw CatalogValidation.Error(CrimesPath, "a known behavior tag", item: crime.Id, field: "behaviorTags", value: unknownBehaviorTag);
            var unknownOpportunityTag = crime.PreferredOpportunityTags.FirstOrDefault(tag => !knownOpportunityTags.Contains(tag));
            if (unknownOpportunityTag is not null)
                throw CatalogValidation.Error(CrimesPath, "a known opportunity tag", item: crime.Id, field: "preferredOpportunityTags", value: unknownOpportunityTag);
            var unknownCareerFamily = crime.PreferredCareerFamilies.FirstOrDefault(family => !families.Contains(family));
            if (unknownCareerFamily is not null)
                throw CatalogValidation.Error(CrimesPath, "a known CareerFamily", item: crime.Id, field: "preferredCareerFamilies", value: unknownCareerFamily);

            if (crime.SentenceMin <= 0)
                throw CatalogValidation.Error(CrimesPath, "an integer greater than 0", item: crime.Id, field: "sentenceMin", value: crime.SentenceMin);
            if (crime.SentenceMax < crime.SentenceMin)
                throw CatalogValidation.Error(CrimesPath, $"an integer of at least sentenceMin ({crime.SentenceMin})", item: crime.Id, field: "sentenceMax", value: crime.SentenceMax);
            if (crime.Weight <= 0)
                throw CatalogValidation.Error(CrimesPath, "a number greater than 0", item: crime.Id, field: "weight", value: crime.Weight);
            if (crime.DetectionBase is < 0 or > 1)
                throw CatalogValidation.Error(CrimesPath, "a number from 0 through 1", item: crime.Id, field: "detectionBase", value: crime.DetectionBase);
            if (crime.PovertyMultiplier <= 0)
                throw CatalogValidation.Error(CrimesPath, "a number greater than 0", item: crime.Id, field: "povertyMultiplier", value: crime.PovertyMultiplier);
            if (crime.StressWeightPerPoint is < 0 or > 0.50)
                throw CatalogValidation.Error(CrimesPath, "a number from 0 through 0.5", item: crime.Id, field: "stressWeightPerPoint", value: crime.StressWeightPerPoint);

            if (crime.IsProfitCrime)
            {
                if (crime.ProfitMin <= 0)
                    throw CatalogValidation.Error(CrimesPath, "an integer greater than 0 for a profit crime", item: crime.Id, field: "profitMin", value: crime.ProfitMin);
                if (crime.ProfitMax < crime.ProfitMin)
                    throw CatalogValidation.Error(CrimesPath, $"an integer of at least profitMin ({crime.ProfitMin})", item: crime.Id, field: "profitMax", value: crime.ProfitMax);
                if (crime.SuccessBase is < 0 or > 1)
                    throw CatalogValidation.Error(CrimesPath, "a number from 0 through 1", item: crime.Id, field: "successBase", value: crime.SuccessBase);
            }
        }
    }

    private static IReadOnlySet<string> ParseOpportunityTags(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length > 0 && !string.IsNullOrWhiteSpace(fields[0]))
                result.Add(fields[0].Trim());
        }
        return result;
    }
}
