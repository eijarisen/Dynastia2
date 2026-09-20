using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed class CareerStressModifierProvider : IStressModifierProvider
{
    private readonly ICareerService _career;
    private readonly IGameEventBus _events;

    public CareerStressModifierProvider(
        ICareerService career,
        IGameEventBus events)
    {
        _career = career;
        _events = events;
    }

    public string Id => "career.stress";

    public IEnumerable<StressContribution> GetStressContributions(IPerson person, int year)
    {
        var career = _career.GetCareer(person);
        if (career.IsEmployed)
        {
            var satisfactionStress =
                CareerPressureRules.GetLowSatisfactionStress(career.JobSatisfaction);
            if (satisfactionStress > 0)
            {
                yield return new StressContribution(
                    "career.low_satisfaction",
                    satisfactionStress);
            }
        }

        var recentOverwork = 0;
        for (var offset = 0; offset < 3; offset++)
        {
            if (_events.GetEventsForYear(year - offset).Any(gameEvent =>
                    gameEvent.SubjectId == person.Id
                    && gameEvent.Type.Equals(
                        "career.work_harder",
                        StringComparison.OrdinalIgnoreCase)))
            {
                recentOverwork++;
            }
        }

        var overworkStress =
            CareerPressureRules.GetRepeatedOverworkStress(recentOverwork);
        if (overworkStress > 0)
        {
            yield return new StressContribution(
                "career.overwork",
                overworkStress);
        }
    }
}
