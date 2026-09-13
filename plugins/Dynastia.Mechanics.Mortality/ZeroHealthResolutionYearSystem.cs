using Dynastia.Contracts;

namespace Dynastia.Mechanics.Mortality;

public sealed class ZeroHealthResolutionYearSystem : IYearSystem
{
    private readonly IHealthService _health;
    private readonly MortalityDeathService _deaths;

    public ZeroHealthResolutionYearSystem(
        IHealthService health,
        MortalityDeathService deaths)
    {
        _health = health;
        _deaths = deaths;
    }

    public string Id => "mortality.resolve_zero_health";
    public YearPhase Phase => YearPhase.LateMortality;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        while (true)
        {
            var zeroHealthPerson = gameState.People.FirstOrDefault(
                person =>
                    person.Tags.Has("state.alive")
                    && !person.Tags.Has("state.dead")
                    && _health.GetHealth(person).Current <= 0);

            if (zeroHealthPerson is null)
                return;

            _deaths.TryResolveZeroHealth(
                gameState,
                zeroHealthPerson);
        }
    }
}
