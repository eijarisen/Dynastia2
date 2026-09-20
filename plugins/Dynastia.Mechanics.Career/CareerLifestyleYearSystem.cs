using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class CareerLifestyleYearSystem : IYearSystem
{
    private readonly StandardCareerService _career;
    private readonly IGameRandom _random;

    public CareerLifestyleYearSystem(
        StandardCareerService career,
        IGameRandom random)
    {
        _career = career;
        _random = random;
    }

    public string Id => "career.lifestyle_satisfaction";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People.Where(person =>
                     person.Tags.Has("state.alive")
                     && !SimulationState.IsInactive(person)))
        {
            var career = _career.GetCareer(person);
            if (!career.IsEmployed || career.IsRetired)
                continue;

            var stance = HouseholdLifestyleRules.GetStance(person);
            if (stance == HouseholdLifestyleStance.Balanced
                || _random.NextDouble() >= HouseholdLifestyleRules.GetMoraleShiftChance(person))
            {
                continue;
            }

            _career.ChangeJobSatisfaction(
                person,
                stance == HouseholdLifestyleStance.Lavish ? 1 : -1);
        }
    }
}
