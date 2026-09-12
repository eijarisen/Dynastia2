using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

public sealed class StandardEducationService : IEducationService
{
    private readonly IFamilyService _family;

    public StandardEducationService(
        IFamilyService family)
    {
        _family = family;
    }

    public void EnsureEducation(IPerson person)
    {
        if (person.Components.Has<EducationComponent>())
            return;

        person.Components.Set(
            new EducationComponent
            {
                Level = 0
            });
    }

    public int GetEducationLevel(IPerson person)
    {
        var component =
            GetRequired(person);

        if (component.Level == 0
            && _family.GetGeneration(person) == 0
            && _family.IsBloodline(person))
        {
            var bytes =
                person.Id.ToByteArray();

            component.Level =
                1 + bytes[6] % 3;
        }

        return component.Level;
    }

    public void SetEducationLevel(IPerson person, int level)
    {
        GetRequired(person).Level =
            Math.Clamp(level, 0, 5);
    }

    public void IncreaseEducation(IPerson person, int amount = 1)
    {
        SetEducationLevel(
            person,
            GetEducationLevel(person) + amount);
    }

    private EducationComponent GetRequired(IPerson person)
    {
        EnsureEducation(person);

        return person.Components.Get<EducationComponent>()
            ?? throw new InvalidOperationException(
                "Education component could not be created.");
    }
}
