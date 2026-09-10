using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class CareerHealthModifierProvider :
    IAnnualHealthModifierProvider
{
    private const double RetirementBonus = 5;
    private const double MiserableJobPenalty = 3;
    private const double WorkHarderPenalty = 5;

    private readonly ICareerService _career;

    public CareerHealthModifierProvider(
        ICareerService career)
    {
        _career = career;
    }

    public string Id => "career.health_modifiers";

    public double GetAnnualHealthChange(IPerson person)
    {
        var career = _career.GetCareer(person);
        var change = 0.0;

        if (career.IsRetired)
            change += RetirementBonus;

        if (!career.IsRetired
            && career.JobLevel > 0
            && career.JobSatisfaction == 1)
        {
            change -= MiserableJobPenalty;
        }

        if (person.Tags.Has("modifier.work_harder"))
            change -= WorkHarderPenalty;

        return change;
    }
}
