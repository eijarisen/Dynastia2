using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed class CareerStressModifierProvider : IStressModifierProvider
{
    private readonly ICareerService _career;

    public CareerStressModifierProvider(ICareerService career)
    {
        _career = career;
    }

    public string Id => "career.stress";

    public IEnumerable<StressContribution> GetStressContributions(IPerson person, int year)
    {
        var career = _career.GetCareer(person);
        if (career.IsEmployed && career.JobSatisfaction == 1)
            yield return new StressContribution("career.miserable_job", 1.5);
    }
}
