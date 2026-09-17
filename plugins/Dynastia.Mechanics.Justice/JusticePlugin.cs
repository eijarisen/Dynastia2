using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class JusticePlugin : IGamePlugin
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
        var family = context.GetService<IFamilyService>() ?? throw new InvalidOperationException("Family service is unavailable.");
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
        var guards = context.GetService<IActionGuardRegistry>() ?? throw new InvalidOperationException("Action guard registry is unavailable.");
        var stressModifiers = context.GetService<IStressModifierRegistry>() ?? throw new InvalidOperationException("Stress modifier registry is unavailable.");
        var stress = context.GetService<IStressService>();

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var crimes = JsonSerializer.Deserialize<List<CrimeDefinition>>(
            data.ReadText(CrimesPath),
            jsonOptions)
            ?? throw new InvalidDataException($"Could not read {CrimesPath}.");

        var attemptRules = JsonSerializer.Deserialize<CrimeAttemptRules>(
            data.ReadText(AttemptRulesPath),
            jsonOptions)
            ?? throw new InvalidDataException($"Could not read {AttemptRulesPath}.");
        attemptRules.Validate();

        ValidateCrimes(
            crimes,
            ParseOpportunityTags(data.ReadText(OpportunityTagsPath)),
            career.GetKnownCareerFamilies());

        var historicalCrimes = CrimeHistoricalCatalog.Load(
            data,
            crimes.Select(crime => crime.Id));
        var attemptContext = contextWeights.LoadGlobalCatalog(AttemptContextPath);
        var crimeContext = contextWeights.LoadCatalog(
            CrimeContextPath,
            crimes.Select(crime => crime.Id));

        var justice = new StandardJusticeService();
        context.AddService<IJusticeService>(justice);

        context.GetService<IStateReconciliationLifecycle>()?
            .Register(
                "justice.components",
                Enum.GetValues<ReconciliationLifecycleStage>(),
                _ => justice.ReconcileAll(context.GetService<IGameState>()!.People),
                order: 42);

        stressModifiers.Register(new JusticeStressModifierProvider(justice));
        guards.Register(new PrisonActionGuard(justice));
        systems.Register(new PrisonStatusYearSystem(justice, family, events));
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
            throw new InvalidDataException("Crime data is empty.");

        var families = knownCareerFamilies.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var crime in crimes)
        {
            if (string.IsNullOrWhiteSpace(crime.Id)
                || string.IsNullOrWhiteSpace(crime.Name)
                || string.IsNullOrWhiteSpace(crime.Description)
                || string.IsNullOrWhiteSpace(crime.Category))
                throw new InvalidDataException("Every crime needs id, name, category and description.");
            if (!ids.Add(crime.Id))
                throw new InvalidDataException($"Duplicate crime ID '{crime.Id}'.");
            if (crime.StartYear < GameCalendarConfiguration.GameStartYear
                || crime.EndYear is int endYear && endYear < crime.StartYear)
                throw new InvalidDataException($"Crime '{crime.Id}' has an invalid era range.");
            if (crime.MinimumAge < 18
                || crime.MaximumAge is int maxAge && maxAge < crime.MinimumAge)
                throw new InvalidDataException($"Crime '{crime.Id}' has an invalid age range.");
            if (!AllowedStats.Contains(crime.PrimaryStat)
                || crime.SecondaryStat is not null
                    && (!AllowedStats.Contains(crime.SecondaryStat)
                        || crime.SecondaryStat.Equals(crime.PrimaryStat, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Crime '{crime.Id}' has invalid aptitude stat metadata.");
            if (!AllowedSettlementPreferences.Contains(crime.SettlementPreference))
                throw new InvalidDataException($"Crime '{crime.Id}' has invalid settlement preference '{crime.SettlementPreference}'.");
            if (crime.BehaviorTags.Any(tag => !AllowedBehaviorTags.Contains(tag)))
                throw new InvalidDataException($"Crime '{crime.Id}' has an unknown behavior tag.");
            if (crime.PreferredOpportunityTags.Any(tag => !knownOpportunityTags.Contains(tag)))
                throw new InvalidDataException($"Crime '{crime.Id}' references an unknown opportunity tag.");
            if (crime.PreferredCareerFamilies.Any(family => !families.Contains(family)))
                throw new InvalidDataException($"Crime '{crime.Id}' references an unknown CareerFamily.");
            if (crime.SentenceMin <= 0 || crime.SentenceMax < crime.SentenceMin)
                throw new InvalidDataException($"Crime '{crime.Id}' has an invalid sentence range.");
            if (crime.Weight <= 0)
                throw new InvalidDataException($"Crime '{crime.Id}' needs a positive weight.");
            if (crime.DetectionBase is < 0 or > 1)
                throw new InvalidDataException($"Crime '{crime.Id}' has invalid detectionBase.");
            if (crime.PovertyMultiplier <= 0 || crime.StressWeightPerPoint is < 0 or > 0.50)
                throw new InvalidDataException($"Crime '{crime.Id}' has invalid poverty/stress weighting.");
            if (crime.IsProfitCrime
                && (crime.ProfitMin <= 0 || crime.ProfitMax < crime.ProfitMin || crime.SuccessBase is < 0 or > 1))
                throw new InvalidDataException($"Crime '{crime.Id}' has invalid profit/success data.");
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
