using Dynastia.Contracts;

namespace Dynastia.Mechanics.Wellbeing;

public sealed class WellbeingCleanupYearSystem :
    IYearSystem
{
    private const string RecoverIncomePrefix =
        "modifier.salary.recover.";

    public string Id =>
        "wellbeing.cleanup";

    public YearPhase Phase =>
        YearPhase.PostYear;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        foreach (var person in
            gameState.People)
        {
            person.Tags.Remove(
                "modifier.recover");

            foreach (var tag in
                person.Tags.All
                    .Where(
                        tag =>
                            tag.StartsWith(
                                RecoverIncomePrefix,
                                StringComparison.OrdinalIgnoreCase))
                    .ToList())
            {
                person.Tags.Remove(
                    tag);
            }
        }
    }
}
