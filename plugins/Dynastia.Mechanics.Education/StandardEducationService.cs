using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

public sealed class StandardEducationService : IEducationService
{
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly EducationEraCatalog _eras;

    public StandardEducationService(
        IFamilyService family,
        IStatsService stats,
        EducationEraCatalog eras)
    {
        _family = family;
        _stats = stats;
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
        return GetRequired(person).Level;
    }

    internal void ReconcilePerson(IPerson person)
    {
        EnsureEducation(person);

        var component =
            person.Components.Get<EducationComponent>()
            ?? throw new InvalidOperationException(
                "Education component could not be created during reconciliation.");

        if (component.IsInitialized)
            return;

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

    public void SetEducationLevel(IPerson person, int level)
    {
        EnsureEducation(person);
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


    public double GetPaidEducationSuccessChance(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var intellect = _stats.GetStats(person)
            .First(stat => stat.Id.Equals(
                "intellect",
                StringComparison.OrdinalIgnoreCase))
            .Value;

        return PersonalityInfluence.AdjustProbability(
            EducationProgressionRules.GetPaidEducationSuccessChance(intellect),
            person,
            melancholic: 0.10,
            choleric: -0.10);
    }

    public EducationGenerationRange GetGeneratedAdultRange(
        int year)
    {
        var rule = _eras.GetRule(year);

        return new EducationGenerationRange(
            rule.GeneratedAdultMinLevel,
            rule.GeneratedAdultMaxLevel);
    }

    private static EducationComponent GetRequired(IPerson person)
    {
        return person.Components.Get<EducationComponent>()
            ?? throw new InvalidOperationException(
                "Education state is missing. Run state reconciliation before reading education.");
    }
}
