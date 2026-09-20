using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed class CareerLifetimeEarningsYearSystem : IYearSystem
{
    private readonly StandardCareerService _career;
    private readonly CareerIncomeProvider _income;

    public CareerLifetimeEarningsYearSystem(
        StandardCareerService career)
    {
        _career = career;
        _income = new CareerIncomeProvider(career);
    }

    public string Id => "career.lifetime_earnings";
    public YearPhase Phase => YearPhase.Finances;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["economy.household_finances"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (!person.Tags.Has("state.alive")
                || SimulationState.IsInactive(person))
            {
                continue;
            }

            var snapshot = _career.GetCareer(person);
            if (snapshot.IsRetired || snapshot.JobLevel <= 0)
                continue;

            _career.RecordLifetimeCareerEarnings(
                person,
                _income.GetAnnualIncome(person),
                gameState.Year);
        }
    }
}
