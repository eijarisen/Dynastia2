using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class StandardCareerService :
    ICareerService,
    ICareerPresentationService
{
    public const decimal DefaultBaseIncomePerLevel =
        500m;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IGameRandom _random;
    private readonly CareerCatalog _catalog;
    private readonly HistoricalCareerPresentationCatalog _presentation;
    private readonly RetirementRuleCatalog
        _retirementRules;
    private readonly ILocalCareerOpportunityService
        _localOpportunities;
    private readonly IStatsService _stats;
    private readonly IEducationService _education;
    private readonly CareerEducationProfileCatalog _educationProfiles;
    private readonly IContextWeightCatalog _careerContext;
    private readonly Func<ICraftService?> _craftResolver;

    internal StandardCareerService(
        IGameState gameState,
        IFamilyService family,
        IGameRandom random,
        CareerCatalog catalog,
        HistoricalCareerPresentationCatalog presentation,
        RetirementRuleCatalog retirementRules,
        ILocalCareerOpportunityService localOpportunities,
        IStatsService stats,
        IEducationService education,
        CareerEducationProfileCatalog educationProfiles,
        IContextWeightCatalog careerContext,
        Func<ICraftService?> craftResolver)
    {
        _gameState = gameState;
        _family = family;
        _random = random;
        _catalog = catalog;
        _presentation = presentation;
        _retirementRules =
            retirementRules;
        _localOpportunities =
            localOpportunities;

        _stats =
            stats;
        _education =
            education;
        _educationProfiles =
            educationProfiles;
        _careerContext =
            careerContext;
        _craftResolver =
            craftResolver;
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
                ? InitialCareerProfileRules.ResolveJobLevel(
                    person.Age,
                    _education.GetEducationLevel(person),
                    _random.NextDouble())
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
                    person,
                    jobLevel)
                .Id;

            UpdatePeakCareer(
                component);
        }

        person.Components.Set(
            component);
    }

    internal void ReconcileAll(
        IEnumerable<IPerson> people)
    {
        foreach (var person in people)
            EnsureCareer(person);
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

        var year = _gameState.Year;
        var crafts = _craftResolver();
        var activeCraft = crafts?.GetActiveCraft(person);
        var isCraftSelfEmployed = activeCraft is not null;
        var annualIncome = isCraftSelfEmployed
            ? crafts!.GetExpectedAnnualIncome(person)
            : GetAnnualIncome(person);
        var jobTitle = isCraftSelfEmployed
            ? activeCraft!.SelfEmploymentTitle
            : ResolveJobTitle(person, career, definition);
        var statusId = isCraftSelfEmployed
            ? null
            : ResolveStatusId(person, career);

        return new CareerSnapshot(
            career.JobLevel,
            jobTitle,
            career.JobSatisfaction,
            ResolveJobSatisfactionText(
                career.JobSatisfaction),
            career.LastIncome,
            annualIncome,
            isCraftSelfEmployed ? false : career.IsRetired,
            isCraftSelfEmployed ? null : definition?.Id,
            isCraftSelfEmployed
                ? activeCraft!.Name
                : definition is null
                    ? null
                    : _presentation.ResolveCareerName(
                        definition.Id,
                        definition.Name,
                        year),
            isCraftSelfEmployed
                ? GetLevelOneSalary(activeCraft!.PrimaryCareerId)
                : definition?.BaseSalary ?? 0,
            career.PeakJobLevel,
            career.PeakCareerId,
            ResolvePeakJobTitle(career),
            statusId,
            career.JobLevel > 0 || isCraftSelfEmployed,
            isCraftSelfEmployed,
            activeCraft?.Id);
    }


    public bool IsEmployed(IPerson person)
    {
        var career = GetRequired(person);
        return (!career.IsRetired && career.JobLevel > 0)
            || _craftResolver()?.IsSelfEmployed(person) == true;
    }

    public string? GetCareerFamily(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        var career = GetRequired(person);
        return career.JobLevel > 0
            ? _catalog.Find(career.CareerId)?.CareerFamily
            : null;
    }

    public IReadOnlyCollection<string> GetKnownCareerFamilies() =>
        _catalog.CareerFamilies;

    public string GetStatusLabel(
        string statusId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            statusId);

        var fallback = statusId switch
        {
            "status.preschool" => "Preschool",
            "status.student" => "Student",
            "status.unemployed" => "Unemployed",
            "status.housewife" => "Housewife",
            "role.nanny" => "Nanny",
            "role.family_nanny" => "Family Caregiver",
            _ => statusId
        };

        return _presentation.ResolveStatus(
            statusId,
            fallback,
            _gameState.Year);
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

        if (level > 0)
            _stats.EnsureStats(person);

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
                            person,
                            level)
                            .Id
                        : null
            };

        UpdatePeakCareer(
            component);

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
            _craftResolver()?.EndOccupation(person, "ended");

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
                    person,
                    targetLevel)
                    .Id;
        }

        career.JobLevel =
            targetLevel;

        UpdatePeakCareer(
            career);
    }

    public void ChangeJobSatisfaction(
        IPerson person,
        int amount)
    {
        var career =
            GetRequired(
                person);

        var adjustedAmount =
            amount;

        if (amount != 0)
        {
            var direction =
                Math.Sign(
                    amount);

            if ((
                    person.Tags.Has(
                        "personality.melancholic")
                    || person.Tags.Has(
                        "personality.choleric")
                )
                && _random.NextDouble()
                    < 0.20)
            {
                adjustedAmount +=
                    direction;
            }
            else if (person.Tags.Has(
                    "personality.phlegmatic")
                && _random.NextDouble()
                    < 0.20)
            {
                adjustedAmount -=
                    direction;
            }
        }

        career.JobSatisfaction =
            Math.Clamp(
                career.JobSatisfaction
                + adjustedAmount,
                1,
                5);
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


    private CareerDefinition? ResolveDefinition(
        IPerson person,
        CareerComponent career)
    {
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
            UpdatePeakCareer(
                career);

            return;
        }

        // Old saves have an employed JobLevel but no CareerId.
        // Preserve the level and assign a career that was open to
        // entrants in the loaded game's effective technological year.
        career.CareerId =
            SelectCareerForEntry(
                person,
                career.JobLevel)
                .Id;

        UpdatePeakCareer(
            career);
    }

    private static void UpdatePeakCareer(
        CareerComponent career)
    {
        if (career.JobLevel <= 0
            || string.IsNullOrWhiteSpace(
                career.CareerId))
        {
            return;
        }

        if (career.JobLevel
            > career.PeakJobLevel)
        {
            career.PeakJobLevel =
                career.JobLevel;

            career.PeakCareerId =
                career.CareerId;

            return;
        }

        if (career.JobLevel
                == career.PeakJobLevel
            && string.IsNullOrWhiteSpace(
                career.PeakCareerId))
        {
            career.PeakCareerId =
                career.CareerId;
        }
    }

    private static CareerComponent GetRequired(
        IPerson person)
    {
        return person.Components.Get<
            CareerComponent>()
            ?? throw new InvalidOperationException(
                "Career state is missing. Run state reconciliation before reading career data.");
    }
}
