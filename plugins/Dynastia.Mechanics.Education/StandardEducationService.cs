using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

public sealed class StandardEducationService : IEducationService
{
    private readonly IFamilyService _family;
    private readonly EducationEraCatalog _eras;

    public StandardEducationService(
        IFamilyService family,
        EducationEraCatalog eras)
    {
        _family = family;
        _eras = eras;
    }

    public void EnsureEducation(IPerson person)
    {
        if (person.Components.Has<EducationComponent>())
            return;

        person.Components.Set(
            new EducationComponent
            {
                Level = 0,
                IsInitialized = false
            });
    }

    public int GetEducationLevel(IPerson person)
    {
        var component =
            GetRequired(person);

        if (!component.IsInitialized)
        {
            if (component.Level == 0
                && _family.GetGeneration(person) == 0
                && _family.IsBloodline(person))
            {
                var bytes =
                    person.Id.ToByteArray();

                component.Level =
                    1 + bytes[6] % 3;
            }

            component.IsInitialized = true;
        }

        return component.Level;
    }

    public void SetEducationLevel(IPerson person, int level)
    {
        var component = GetRequired(person);
        component.Level =
            Math.Clamp(level, 0, 5);
        component.IsInitialized = true;
    }

    public void IncreaseEducation(IPerson person, int amount = 1)
    {
        SetEducationLevel(
            person,
            GetEducationLevel(person) + amount);
    }

    public EducationGenerationRange GetGeneratedAdultRange(
        int year)
    {
        var rule = _eras.GetRule(year);

        return new EducationGenerationRange(
            rule.GeneratedAdultMinLevel,
            rule.GeneratedAdultMaxLevel);
    }

    private EducationComponent GetRequired(IPerson person)
    {
        EnsureEducation(person);

        return person.Components.Get<EducationComponent>()
            ?? throw new InvalidOperationException(
                "Education component could not be created.");
    }
}
