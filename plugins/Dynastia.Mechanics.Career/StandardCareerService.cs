using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class StandardCareerService :
    ICareerService
{
    public const decimal DefaultBaseIncomePerLevel =
        500m;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IGameRandom _random;
    private readonly CareerCatalog _catalog;

    internal StandardCareerService(
        IGameState gameState,
        IFamilyService family,
        IGameRandom random,
        CareerCatalog catalog)
    {
        _gameState = gameState;
        _family = family;
        _random = random;
        _catalog = catalog;
    }

    public void EnsureCareer(
        IPerson person)
    {
        if (person.Components.Has<
            CareerComponent>())
        {
            EnsureValidAssignment(
                person,
                person.Components.Get<
                    CareerComponent>()
                ?? throw new InvalidOperationException(
                    "Career component is unavailable."));

            return;
        }

        var jobLevel =
            person.Age >= 18
                && !person.Tags.Has(
                    "role.nanny")
                ? _random.NextInt(
                    0,
                    3)
                : 0;

        var component =
            new CareerComponent
            {
                JobLevel =
                    jobLevel,

                JobSatisfaction =
                    _random.NextInt(
                        1,
                        5),

                LastIncome =
                    0,

                IsRetired =
                    false
            };

        if (jobLevel > 0)
        {
            component.CareerId =
                SelectCareerForEntry(
                    person)
                .Id;
        }

        person.Components.Set(
            component);
    }

    public CareerSnapshot GetCareer(
        IPerson person)
    {
        var career =
            GetRequired(
                person);

        var definition =
            ResolveDefinition(
                person,
                career);

        return new CareerSnapshot(
            career.JobLevel,
            ResolveJobTitle(
                person,
                career,
                definition),
            career.JobSatisfaction,
            ResolveJobSatisfactionText(
                career.JobSatisfaction),
            career.LastIncome,
            GetAnnualIncome(
                person),
            career.IsRetired,
            definition?.Id,
            definition?.Name,
            definition?.BaseSalary
                ?? 0);
    }

    public void InitializeCareer(
        IPerson person,
        int jobLevel,
        int jobSatisfaction)
    {
        var level =
            Math.Clamp(
                jobLevel,
                0,
                5);

        var component =
            new CareerComponent
            {
                JobLevel =
                    level,

                JobSatisfaction =
                    Math.Clamp(
                        jobSatisfaction,
                        1,
                        5),

                LastIncome =
                    0,

                IsRetired =
                    false,

                CareerId =
                    level > 0
                        ? SelectCareerForEntry(
                            person)
                            .Id
                        : null
            };

        person.Components.Set(
            component);
    }

    public void SetJobLevel(
        IPerson person,
        int jobLevel)
    {
        var career =
            GetRequired(
                person);

        var targetLevel =
            Math.Clamp(
                jobLevel,
                0,
                5);

        if (targetLevel <= 0)
        {
            career.JobLevel =
                0;

            if (!career.IsRetired)
            {
                // Leaving employment deliberately clears the career.
                // A future successful job search therefore draws again
                // from the then-current employment pool.
                career.CareerId =
                    null;
            }

            return;
        }

        var definition =
            _catalog.Find(
                career.CareerId);

        if (career.JobLevel <= 0
            || definition is null)
        {
            career.CareerId =
                SelectCareerForEntry(
                    person)
                    .Id;
        }

        career.JobLevel =
            targetLevel;
    }

    public void ChangeJobSatisfaction(
        IPerson person,
        int amount)
    {
        var career =
            GetRequired(
                person);

        career.JobSatisfaction =
            Math.Clamp(
                career.JobSatisfaction
                + amount,
                1,
                5);
    }

    public void Retire(
        IPerson person)
    {
        var career =
            GetRequired(
                person);

        if (career.IsRetired)
            return;

        career.LastIncome =
            GetActiveSalary(
                person,
                career);

        career.JobLevel =
            0;

        career.IsRetired =
            true;
    }

    public decimal GetAnnualIncome(
        IPerson person)
    {
        var career =
            GetRequired(
                person);

        if (career.IsRetired)
        {
            return career.LastIncome
                * 0.10m;
        }

        return GetActiveSalary(
            person,
            career);
    }

    internal CareerObsolescencePressure
        GetObsolescencePressure(
            IPerson person,
            int gameYear)
    {
        var career =
            GetRequired(
                person);

        if (career.JobLevel <= 0
            || career.IsRetired)
        {
            return CareerObsolescencePressure.None;
        }

        var definition =
            ResolveDefinition(
                person,
                career);

        return definition?
            .GetObsolescencePressure(
                gameYear)
            ?? CareerObsolescencePressure.None;
    }

    internal CareerDefinition? GetDefinition(
        IPerson person)
    {
        var career =
            GetRequired(
                person);

        return ResolveDefinition(
            person,
            career);
    }

    private decimal GetActiveSalary(
        IPerson person,
        CareerComponent career)
    {
        if (career.JobLevel <= 0)
            return 0;

        var definition =
            ResolveDefinition(
                person,
                career);

        var baseSalary =
            definition?.BaseSalary
            ?? DefaultBaseIncomePerLevel;

        return baseSalary
            * career.JobLevel;
    }

    private string ResolveJobTitle(
        IPerson person,
        CareerComponent career,
        CareerDefinition? definition)
    {
        // Existing special-status precedence is intentionally preserved.
        if (person.Tags.Has(
            "role.nanny"))
        {
            return "Nanny";
        }

        if (person.Tags.Has(
            "state.imprisoned"))
        {
            return "Imprisoned";
        }

        if (career.IsRetired)
            return "Retired";

        if (_family.GetSex(
                person)
                == Sex.Female
            && career.JobLevel == 0
            && _family.GetChildren(
                person)
                .Count > 0)
        {
            return "Housewife";
        }

        if (person.Age < 6)
            return "Preschool";

        if (person.Age < 18)
            return "Student";

        if (career.JobLevel <= 0)
            return "Unemployed";

        return definition?
            .GetTitle(
                career.JobLevel)
            ?? career.JobLevel switch
            {
                1 => "Laborer",
                2 => "Clerk",
                3 => "Manager",
                4 => "Director",
                5 => "Magnate",
                _ => "Unemployed"
            };
    }

    private CareerDefinition? ResolveDefinition(
        IPerson person,
        CareerComponent career)
    {
        EnsureValidAssignment(
            person,
            career);

        return _catalog.Find(
            career.CareerId);
    }

    private void EnsureValidAssignment(
        IPerson person,
        CareerComponent career)
    {
        if (career.JobLevel <= 0)
        {
            if (!career.IsRetired)
            {
                career.CareerId =
                    null;
            }

            return;
        }

        if (_catalog.Find(
            career.CareerId)
            is not null)
        {
            // A valid existing career is retained even if obsolete.
            return;
        }

        // Old saves have an employed JobLevel but no CareerId.
        // Preserve the level and assign a career that was open to
        // entrants in the loaded game's effective technological year.
        career.CareerId =
            SelectCareerForEntry(
                person)
                .Id;
    }

    private CareerDefinition SelectCareerForEntry(
        IPerson person)
    {
        return _catalog.SelectForEntry(
            _family.GetSex(
                person),
            _gameState.Year,
            _random);
    }

    private static string
        ResolveJobSatisfactionText(
            int value)
    {
        return value switch
        {
            1 => "Miserable",
            2 => "Unhappy",
            3 => "Content",
            4 => "Satisfied",
            5 => "Thriving",
            _ => string.Empty
        };
    }

    private CareerComponent GetRequired(
        IPerson person)
    {
        EnsureCareer(
            person);

        return person.Components.Get<
            CareerComponent>()
            ?? throw new InvalidOperationException(
                "Career component could not be created.");
    }
}
