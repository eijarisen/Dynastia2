using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class JusticePlugin : IGamePlugin
{
    private const string CrimesPath = "Common/crimes.json";

    public void Initialize(IGamePluginContext context)
    {
        var family = context.GetService<IFamilyService>() ?? throw new InvalidOperationException("Family service is unavailable.");
        var stats = context.GetService<IStatsService>() ?? throw new InvalidOperationException("Stats service is unavailable.");
        var career = context.GetService<ICareerService>() ?? throw new InvalidOperationException("Career service is unavailable.");
        var economy = context.GetService<IEconomyService>() ?? throw new InvalidOperationException("Economy service is unavailable.");
        var economyBalance = context.GetService<IEconomyBalanceService>() ?? throw new InvalidOperationException("Economy balance service is unavailable.");
        var data = context.GetService<IGameDataService>() ?? throw new InvalidOperationException("Game data service is unavailable.");
        var random = context.GetService<IGameRandom>() ?? throw new InvalidOperationException("Random service is unavailable.");
        var events = context.GetService<IGameEventBus>() ?? throw new InvalidOperationException("Game event bus is unavailable.");
        var systems = context.GetService<IYearSystemRegistry>() ?? throw new InvalidOperationException("Year system registry is unavailable.");
        var guards = context.GetService<IActionGuardRegistry>() ?? throw new InvalidOperationException("Action guard registry is unavailable.");

        var crimes = JsonSerializer.Deserialize<List<CrimeDefinition>>(
            data.ReadText(CrimesPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Could not read {CrimesPath}.");
        ValidateCrimes(crimes);

        var justice = new StandardJusticeService();
        context.AddService<IJusticeService>(justice);
        guards.Register(new PrisonActionGuard(justice));
        systems.Register(new PrisonStatusYearSystem(justice, family, events));
        systems.Register(new CrimeYearSystem(justice, family, stats, career, economy, economyBalance, random, events, crimes));
        context.Log("Justice mechanics registered.");
    }

    private static void ValidateCrimes(IReadOnlyList<CrimeDefinition> crimes)
    {
        if (crimes.Count == 0) throw new InvalidDataException("Crime data is empty.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var crime in crimes)
        {
            if (string.IsNullOrWhiteSpace(crime.Id) || string.IsNullOrWhiteSpace(crime.Name) || string.IsNullOrWhiteSpace(crime.Description) || string.IsNullOrWhiteSpace(crime.Category))
                throw new InvalidDataException("Every crime needs id, name, category and description.");
            if (!ids.Add(crime.Id)) throw new InvalidDataException($"Duplicate crime ID '{crime.Id}'.");
            if (crime.SentenceMin <= 0 || crime.SentenceMax < crime.SentenceMin) throw new InvalidDataException($"Crime '{crime.Id}' has an invalid sentence range.");
            if (crime.Weight <= 0) throw new InvalidDataException($"Crime '{crime.Id}' needs a positive weight.");
            if (crime.DetectionBase is < 0 or > 1) throw new InvalidDataException($"Crime '{crime.Id}' has invalid detectionBase.");
            if (crime.IsProfitCrime && (crime.ProfitMin <= 0 || crime.ProfitMax < crime.ProfitMin || crime.SuccessBase is < 0 or > 1))
                throw new InvalidDataException($"Crime '{crime.Id}' has invalid profit/success data.");
        }
    }
}
