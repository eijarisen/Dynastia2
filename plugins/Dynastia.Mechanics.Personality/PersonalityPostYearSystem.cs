using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

internal sealed class PersonalityPostYearSystem :
    IYearSystem
{
    private readonly StandardPersonalityService
        _personality;

    public PersonalityPostYearSystem(
        StandardPersonalityService personality)
    {
        _personality = personality;
    }

    public string Id =>
        "personality.post_year_reconcile";

    public YearPhase Phase =>
        YearPhase.PostYear;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        _personality.ReconcileAll();
    }
}
