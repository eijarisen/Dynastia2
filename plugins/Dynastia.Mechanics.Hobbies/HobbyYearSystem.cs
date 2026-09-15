using Dynastia.Contracts;

namespace Dynastia.Mechanics.Hobbies;

internal sealed class HobbyYearSystem :
    IYearSystem
{
    private readonly StandardHobbyService _hobbies;

    public HobbyYearSystem(
        StandardHobbyService hobbies)
    {
        _hobbies = hobbies;
    }

    public string Id =>
        "hobbies.acquire";

    public YearPhase Phase =>
        YearPhase.PostYear;

    public IReadOnlyCollection<string> Before =>
        [];

    public IReadOnlyCollection<string> After =>
        ["personality.post_year_reconcile"];

    public void Execute(
        IGameState gameState)
    {
        _hobbies.ReconcileAll();
    }
}
