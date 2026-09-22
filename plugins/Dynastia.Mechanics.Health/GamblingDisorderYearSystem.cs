using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

internal sealed class GamblingDisorderYearSystem : IYearSystem
{
    private readonly IHealthService _health;
    private readonly IEconomyService _economy;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public GamblingDisorderYearSystem(
        IHealthService health,
        IEconomyService economy,
        IGameRandom random,
        IGameEventBus events)
    {
        _health = health;
        _economy = economy;
        _random = random;
        _events = events;
    }

    public string Id => "health.gambling_disorder_finances";
    public YearPhase Phase => YearPhase.Finances;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["economy.household_finances"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People.Where(person =>
                     person.Tags.Has("state.alive")
                     && !SimulationState.IsInactive(person)
                     && person.Age >= 18
                     && !person.Tags.Has("state.imprisoned")
                     && _health.HasCondition(person, "gambling_disorder")
                     && _economy.GetHousehold(person) is not null).ToList())
        {
            ResolveOutcome(gameState, person);
        }
    }

    private void ResolveOutcome(IGameState gameState, IPerson person)
    {
        var town = _economy.GetResidenceTown(person);
        var reference = Math.Max(
            _economy.GetProjectedAnnualIncome(person),
            4m * _economy.GetLivingCostPerPerson(town));
        reference = Math.Max(0m, reference);

        var loss = _random.NextDouble() < 0.80;
        var fraction = loss
            ? 0.10m + (decimal)_random.NextDouble() * 0.15m
            : 0.05m + (decimal)_random.NextDouble() * 0.15m;
        var amount = Math.Round(reference * fraction, 0, MidpointRounding.AwayFromZero);
        if (amount <= 0m)
            amount = 1m;

        if (loss)
        {
            _economy.ChangeWealthAllowDebt(person, -amount);
            _economy.RecordRealizedExpense(person, "gambling losses", amount);
        }
        else
        {
            _economy.ChangeWealth(person, amount);
        }

        _events.Publish(new GameEvent
        {
            Type = "health.gambling_outcome",
            Year = gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["outcome"] = loss ? "loss" : "win",
                ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                ["reference"] = reference.ToString(CultureInfo.InvariantCulture),
                ["suppressChronicle"] = "true"
            }
        });
    }
}
