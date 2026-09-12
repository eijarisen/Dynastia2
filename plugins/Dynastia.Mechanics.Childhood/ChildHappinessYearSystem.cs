using Dynastia.Contracts;

namespace Dynastia.Mechanics.Childhood;

public sealed class ChildHappinessYearSystem : IYearSystem
{
    private readonly IChildHappinessService _happiness;
    private readonly IHealthService _health;
    private readonly IEconomyService _economy;
    private readonly IPersonalityService _personality;
    private readonly IGameRandom _random;

    public ChildHappinessYearSystem(
        IChildHappinessService happiness,
        IHealthService health,
        IEconomyService economy,
        IPersonalityService personality,
        IGameRandom random)
    {
        _happiness = happiness;
        _health = health;
        _economy = economy;
        _personality = personality;
        _random = random;
    }

    public string Id => "childhood.happiness";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var child in gameState.People.Where(p =>
                     p.Tags.Has("state.alive") && p.Age < 18))
        {
            _happiness.EnsureHappiness(child);
            var health = _health.GetHealth(child).Current;
            var household = _economy.GetHousehold(child);

            if (health < 35)
                _happiness.ChangeHappiness(child, -1);
            else if (health < 60 && _random.NextDouble() < 0.45)
                _happiness.ChangeHappiness(child, -1);
            else if (health >= 90
                     && child.Tags.Has("personality.sanguine")
                     && _random.NextDouble() < 0.30)
                _happiness.ChangeHappiness(child, 1);

            if (household is not null
                && household.Wealth <= 0
                && _random.NextDouble() < 0.55)
            {
                _happiness.ChangeHappiness(child, -1);
            }

            var temperamentCurrent = _happiness.GetHappiness(child)?.Value ?? 3;

            if ((child.Tags.Has("personality.melancholic")
                 || child.Tags.Has("personality.choleric"))
                && _random.NextDouble() < 0.15)
            {
                _happiness.ChangeHappiness(child, -1);
            }
            else if (child.Tags.Has("personality.phlegmatic")
                     && temperamentCurrent != 3
                     && _random.NextDouble() < 0.35)
            {
                _happiness.ChangeHappiness(child, temperamentCurrent < 3 ? 1 : -1);
            }
            else if (child.Tags.Has("personality.sanguine")
                     && temperamentCurrent < 3
                     && _random.NextDouble() < 0.25)
            {
                _happiness.ChangeHappiness(child, 1);
            }

            var current = _happiness.GetHappiness(child)?.Value ?? 3;
            if (child.Age < 5 || current > 2)
                continue;

            var distressChance = current == 1 ? 0.10 : 0.05;
            distressChance = PersonalityInfluence.AdjustProbability(
                distressChance,
                child,
                melancholic: 0.25,
                phlegmatic: -0.20,
                sanguine: -0.10,
                choleric: 0.20);

            if (_random.NextDouble() < distressChance)
            {
                var condition = _random.NextDouble() < 0.5
                    ? "anxiety"
                    : "depression";
                _health.AddCondition(child, condition);
            }

            var moralsDownChance = current == 1 ? 0.04 : 0.02;
            moralsDownChance = PersonalityInfluence.AdjustProbability(
                moralsDownChance,
                child,
                melancholic: 0.20,
                phlegmatic: -0.25,
                choleric: 0.20);

            if (_random.NextDouble() < moralsDownChance)
                _personality.ShiftMorals(child, -1);
        }
    }
}
