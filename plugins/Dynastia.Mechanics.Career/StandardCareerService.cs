using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class StandardCareerService : ICareerService
{
    public const decimal BaseIncomePerLevel = 500m;

    private readonly IFamilyService _family;
    private readonly IGameRandom _random;

    public StandardCareerService(
        IFamilyService family,
        IGameRandom random)
    {
        _family = family;
        _random = random;
    }

    public void EnsureCareer(IPerson person)
    {
        if (person.Components.Has<CareerComponent>())
            return;

        person.Components.Set(
            new CareerComponent
            {
                JobLevel =
                    person.Age >= 18
                        ? _random.NextInt(0, 3)
                        : 0,
                JobSatisfaction =
                    _random.NextInt(1, 5),
                LastIncome = 0,
                IsRetired = false
            });
    }

    public CareerSnapshot GetCareer(IPerson person)
    {
        var career = GetRequired(person);

        return new CareerSnapshot(
            career.JobLevel,
            ResolveJobTitle(person, career),
            career.JobSatisfaction,
            ResolveJobSatisfactionText(career.JobSatisfaction),
            career.LastIncome,
            GetAnnualIncome(person),
            career.IsRetired);
    }

    public void InitializeCareer(
        IPerson person,
        int jobLevel,
        int jobSatisfaction)
    {
        person.Components.Set(
            new CareerComponent
            {
                JobLevel = Math.Clamp(jobLevel, 0, 5),
                JobSatisfaction = Math.Clamp(jobSatisfaction, 1, 5),
                LastIncome = 0,
                IsRetired = false
            });
    }

    public void SetJobLevel(IPerson person, int jobLevel)
    {
        GetRequired(person).JobLevel =
            Math.Clamp(jobLevel, 0, 5);
    }

    public void ChangeJobSatisfaction(
        IPerson person,
        int amount)
    {
        var career = GetRequired(person);

        career.JobSatisfaction =
            Math.Clamp(
                career.JobSatisfaction + amount,
                1,
                5);
    }

    public void Retire(IPerson person)
    {
        var career = GetRequired(person);

        if (career.IsRetired)
            return;

        career.LastIncome =
            career.JobLevel * BaseIncomePerLevel;

        career.JobLevel = 0;
        career.IsRetired = true;
    }

    public decimal GetAnnualIncome(IPerson person)
    {
        var career = GetRequired(person);

        if (career.IsRetired)
            return career.LastIncome * 0.10m;

        if (career.JobLevel > 0)
            return career.JobLevel * BaseIncomePerLevel;

        return 0;
    }

    private string ResolveJobTitle(
        IPerson person,
        CareerComponent career)
    {
        if (person.Tags.Has("role.nanny"))
            return "Nanny";

        if (person.Tags.Has("state.imprisoned"))
            return "Imprisoned";

        if (career.IsRetired)
            return "Retired";

        if (_family.GetSex(person) == Sex.Female
            && career.JobLevel == 0
            && _family.GetChildren(person).Count > 0)
        {
            return "Housewife";
        }

        if (person.Age < 6)
            return "Preschool";

        if (person.Age < 18)
            return "Student";

        return career.JobLevel switch
        {
            0 => "Unemployed",
            1 => "Laborer",
            2 => "Clerk",
            3 => "Manager",
            4 => "Director",
            5 => "Magnate",
            _ => "Unemployed"
        };
    }

    private static string ResolveJobSatisfactionText(int value) =>
        value switch
        {
            1 => "Miserable",
            2 => "Unhappy",
            3 => "Content",
            4 => "Satisfied",
            5 => "Thriving",
            _ => string.Empty
        };

    private CareerComponent GetRequired(IPerson person)
    {
        EnsureCareer(person);

        return person.Components.Get<CareerComponent>()
            ?? throw new InvalidOperationException(
                "Career component could not be created.");
    }
}
