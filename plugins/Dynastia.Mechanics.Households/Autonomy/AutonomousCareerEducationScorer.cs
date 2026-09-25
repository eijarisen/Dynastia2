using System.Globalization;
using Dynastia.Contracts;
using static Dynastia.Mechanics.Households.AutonomousScoringHelpers;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousCareerEducationScorer : IAutonomousActionScorer
{
    private readonly IGamePluginContext _context;
    private readonly ICareerService _career;
    private readonly IStatsService _stats;

    public AutonomousCareerEducationScorer(
        IGamePluginContext context,
        ICareerService career,
        IStatsService stats)
    {
        _context = context;
        _career = career;
        _stats = stats;
    }

    public bool Handles(string actionId)
    {
        var id = actionId.ToLowerInvariant();
        return id switch
        {
            "career.seek_employment" => true,
            "career.help_seek_employment" => true,
            "career.find_another_job" or
            "career.help_find_better_job" => true,
            "education.help_learning" => true,
            "education.private_tutor" => true,
            "education.get_education" => true,
            "career.work_harder" => true,
            "career.quit_job" => true,
            "career.ask_to_quit" => true,
            _ when id.StartsWith("craft.start.", StringComparison.OrdinalIgnoreCase) => true,
            _ when id.StartsWith("craft.teach.", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

    public AutonomousActionCandidate? Score(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var id = option.Action.Id.ToLowerInvariant();
        var targetMember = snapshot.Members
            .FirstOrDefault(member => member.Person.Id == option.Target.Id);

        return id switch
        {
            "career.seek_employment" =>
                ScoreSeekEmployment(option, snapshot, targetMember),

            "career.help_seek_employment" =>
                ScoreHelpSeekEmployment(option, snapshot, targetMember),

            "career.find_another_job" or
            "career.help_find_better_job" =>
                ScoreFindAnotherJob(option, snapshot, targetMember),

            "education.help_learning" =>
                ScoreHelpLearning(option, snapshot, targetMember),

            "education.private_tutor" =>
                ScorePrivateTutor(option, snapshot, targetMember),

            "education.get_education" =>
                ScoreEducation(option, snapshot),

            "career.work_harder" =>
                ScoreWorkHarder(option, snapshot),

            "career.quit_job" =>
                ScoreQuitJob(option, snapshot, snapshot.Members.FirstOrDefault(
                    member => member.Person.Id == snapshot.Head.Id)),

            "career.ask_to_quit" =>
                ScoreQuitJob(option, snapshot, targetMember),

            _ when id.StartsWith("craft.start.", StringComparison.OrdinalIgnoreCase) =>
                ScoreStartCraft(option, snapshot, targetMember),

            _ when id.StartsWith("craft.teach.", StringComparison.OrdinalIgnoreCase) =>
                ScoreTeachCraft(option, snapshot),

            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreSeekEmployment(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        var person = target?.Person ?? snapshot.Head;
        var jobValue = GetSelectedJobValue(option, snapshot);
        var craftValue = GetBestKnownCraftIncome(person);
        var farmValue = GetMarginalFarmWorkValue(snapshot, person);
        var alternative = Math.Max(craftValue, farmValue);

        if (jobValue < craftValue
            || farmValue > 0m
                && !AutonomousWorkChoiceRules.IsMateriallyBetter(jobValue, farmValue))
        {
            return null;
        }

        var bonus = AutonomousWorkChoiceRules.GetAdvantageBonus(
            jobValue,
            alternative);

        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 100 + bonus),
            AutonomousFinancialState.Poor => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 93 + bonus),
            _ => WithScore(option,
                AutonomyCategory.CareerDevelopment,
                AutonomousPriorityBands.LongTermImprovement, 62 + bonus)
        };
    }

    private AutonomousActionCandidate? ScoreHelpSeekEmployment(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target?.Career?.IsEmployed != false)
            return null;

        var jobValue = GetSelectedJobValue(option, snapshot);
        var craftValue = GetBestKnownCraftIncome(target.Person);
        var farmValue = GetMarginalFarmWorkValue(snapshot, target.Person);
        var alternative = Math.Max(craftValue, farmValue);

        if (jobValue < craftValue
            || farmValue > 0m
                && !AutonomousWorkChoiceRules.IsMateriallyBetter(jobValue, farmValue))
        {
            return null;
        }

        var bonus = AutonomousWorkChoiceRules.GetAdvantageBonus(
            jobValue,
            alternative);

        if (snapshot.FinancialState is AutonomousFinancialState.Critical)
        {
            return WithScore(option, AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 95 + bonus);
        }

        if (snapshot.FinancialState is AutonomousFinancialState.Poor)
        {
            return WithScore(option, AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 90 + bonus);
        }

        // Employment is not merely development when it is the missing legal
        // prerequisite for an adult child's independent household. Give the
        // prerequisite the same family-formation urgency as the move itself.
        if (SupportsAdultFamilyFormation(snapshot, target))
        {
            return WithScore(option, AutonomyCategory.Continuity,
                AutonomousPriorityBands.SustainableFamilyContinuity,
                86 + bonus);
        }

        return WithScore(option, AutonomyCategory.CareerDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 55 + bonus);
    }

    private AutonomousActionCandidate? ScoreFindAnotherJob(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        var member = target ?? snapshot.Members.FirstOrDefault(
            candidate => candidate.Person.Id == snapshot.Head.Id);
        var person = member?.Person ?? snapshot.Head;
        var current = _career.GetCareer(person);
        var level = current.JobLevel;
        if (!current.IsEmployed)
            return null;

        var jobValue = GetSelectedJobValue(option, snapshot);
        var craftValue = GetBestKnownCraftIncome(person);
        var baseline = Math.Max(current.AnnualIncome, craftValue);

        if (!AutonomousWorkChoiceRules.IsMateriallyBetter(jobValue, baseline))
            return null;

        var bonus = AutonomousWorkChoiceRules.GetAdvantageBonus(
            jobValue,
            baseline);

        if ((snapshot.FinancialState is AutonomousFinancialState.Critical
                or AutonomousFinancialState.Poor)
            && level <= 2)
        {
            return WithScore(option, AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency,
                (snapshot.FinancialState == AutonomousFinancialState.Critical ? 78 : 72)
                + bonus);
        }

        if (IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            if (SupportsAdultFamilyFormation(snapshot, member))
            {
                return WithScore(option, AutonomyCategory.Continuity,
                    AutonomousPriorityBands.SustainableFamilyContinuity,
                    78 + bonus);
            }

            return WithScore(option, AutonomyCategory.CareerDevelopment,
                AutonomousPriorityBands.LongTermImprovement, 58 + bonus);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreEducation(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            || snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed
            || snapshot.NeedsFamilyContinuity)
        {
            return null;
        }

        var cost = option.Action.DisplayCost ?? 5000m;
        if ((snapshot.Finance?.Wealth ?? 0m) - cost < snapshot.ExpectedExpenses * 2m
            || !LeavesReserve(snapshot, cost, snapshot.ExpectedExpenses * 2m))
            return null;

        return WithScore(option, AutonomyCategory.PersonalDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 62);
    }

    private AutonomousActionCandidate? ScoreHelpLearning(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null || !target.IsChild
            || snapshot.FinancialState is AutonomousFinancialState.Critical
                or AutonomousFinancialState.Poor
            || snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.ChildProtection,
            AutonomousPriorityBands.FamilyStability,
            snapshot.NeedsFamilyExpansion ? 52 : 60);
    }

    private AutonomousActionCandidate? ScorePrivateTutor(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null
            || !target.IsChild
            || snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed
            || !IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return null;
        }

        var tutorCost = option.Action.DisplayCost ?? 0m;
        var reserve = (snapshot.Finance?.Wealth ?? 0m) - snapshot.ExpectedExpenses;
        if (reserve < 6000m
            || !LeavesReserve(snapshot, tutorCost, snapshot.ExpectedExpenses))
            return null;

        var intellect = _stats.GetStats(target.Person)
            .FirstOrDefault(stat => stat.Id.Equals("intellect", StringComparison.OrdinalIgnoreCase))?.Value ?? 3;
        return WithScore(
            option,
            AutonomyCategory.ChildProtection,
            AutonomousPriorityBands.FamilyStability,
            34 + intellect * 4);
    }

    private AutonomousActionCandidate? ScoreWorkHarder(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var head = snapshot.Members.First(member => member.Person.Id == snapshot.Head.Id);
        if (head.Health.Percentage < 90
            || snapshot.Status?.IsLargeFamilyStrained == true
            || snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed
            || snapshot.NeedsFamilyContinuity
            || snapshot.DependentChildCount > 0
            || snapshot.MarriageSatisfaction is < 80
            || head.Career is not { IsEmployed: true, JobSatisfaction: >= 3 }
            || !IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return null;
        }

        var stress = _context.GetService<IStressService>()?.GetStress(snapshot.Head);
        if (stress?.Total >= StressScale.FromLegacy(3))
            return null;

        var year = _context.GetService<IGameState>()?.Year;
        var events = _context.GetService<IGameEventBus>();
        if (year is { } currentYear && events is not null
            && Enumerable.Range(0, 3).Any(offset => events.GetEventsForYear(currentYear - offset)
                .Any(gameEvent => gameEvent.SubjectId == snapshot.Head.Id
                    && gameEvent.Type.Equals("career.work_harder", StringComparison.OrdinalIgnoreCase))))
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.CareerDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 48);
    }

    private AutonomousActionCandidate? ScoreQuitJob(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target?.Career is null
            || target.Career.JobSatisfaction != 1
            || !IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Secure))
        {
            return null;
        }

        var incomeWithoutTarget = snapshot.ProjectedIncome - target.Career.AnnualIncome;
        if (incomeWithoutTarget < snapshot.ExpectedExpenses)
            return null;

        return WithScore(option, AutonomyCategory.PersonalDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 48);
    }

    private AutonomousActionCandidate? ScoreStartCraft(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        var crafts = _context.GetService<ICraftService>();
        var person = target?.Person ?? snapshot.Head;
        if (crafts is null)
            return null;

        var craftId = option.Action.Id["craft.start.".Length..];
        var craft = crafts.Catalog.FirstOrDefault(candidate =>
            candidate.Id.Equals(craftId, StringComparison.OrdinalIgnoreCase));
        var progress = craft is null
            ? null
            : crafts.GetProgress(person, craft.Id);
        if (craft is null || progress is null)
            return null;

        var expectedCraftIncome = progress.ExpectedAnnualIncome;
        if (expectedCraftIncome <= 0m)
            return null;

        var current = _career.GetCareer(person);
        var currentIncome = current.IsEmployed
            ? current.AnnualIncome
            : 0m;
        var farmValue = GetMarginalFarmWorkValue(snapshot, person);
        var careerValue = GetBestCareerOpportunityValue(person, snapshot);
        var alternative = Math.Max(
            currentIncome,
            Math.Max(farmValue, careerValue));

        if (expectedCraftIncome < careerValue
            || farmValue > 0m
                && !AutonomousWorkChoiceRules.IsMateriallyBetter(
                    expectedCraftIncome,
                    farmValue)
            || current.IsEmployed
                && !AutonomousWorkChoiceRules.IsMateriallyBetter(
                    expectedCraftIncome,
                    currentIncome))
        {
            return null;
        }

        var bonus = AutonomousWorkChoiceRules.GetAdvantageBonus(
            expectedCraftIncome,
            alternative);

        if (!current.IsEmployed)
        {
            if (snapshot.FinancialState is AutonomousFinancialState.Critical)
            {
                return WithScore(option, AutonomyCategory.Solvency,
                    AutonomousPriorityBands.HouseholdSolvency, 96 + bonus);
            }

            if (snapshot.FinancialState is AutonomousFinancialState.Poor)
            {
                return WithScore(option, AutonomyCategory.Solvency,
                    AutonomousPriorityBands.HouseholdSolvency, 88 + bonus);
            }

            if (SupportsAdultFamilyFormation(snapshot, target))
            {
                return WithScore(option, AutonomyCategory.Continuity,
                    AutonomousPriorityBands.SustainableFamilyContinuity,
                    82 + bonus);
            }

            return WithScore(option, AutonomyCategory.CareerDevelopment,
                AutonomousPriorityBands.LongTermImprovement, 60 + bonus);
        }

        return WithScore(option, AutonomyCategory.CareerDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 48 + bonus);
    }

    private AutonomousActionCandidate? ScoreTeachCraft(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            || snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed
            || snapshot.Status?.IsLargeFamilyStrained == true
            || snapshot.NeedsFamilyContinuity)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.CareerDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 38);
    }


    private static bool SupportsAdultFamilyFormation(
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target) =>
        target is not null
        && target.Person.Age >= 18
        && snapshot.HasAdultFamilyFormationNeed
        && snapshot.ExistingChildren.Any(child => child.Id == target.Person.Id);

    private decimal GetSelectedJobValue(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!option.Parameters.TryGetValue("jobAnnualSalary", out var salaryText)
            || !decimal.TryParse(
                salaryText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var salary)
            || !option.Parameters.TryGetValue("jobSuccessChance", out var chanceText)
            || !double.TryParse(
                chanceText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var chance))
        {
            return 0m;
        }

        var urgent = snapshot.FinancialState is
            AutonomousFinancialState.Critical or
            AutonomousFinancialState.Poor;

        return AutonomousWorkChoiceRules.GetCareerOpportunityValue(
            salary,
            chance,
            urgent);
    }

    private decimal GetBestCareerOpportunityValue(
        IPerson person,
        AutonomousHouseholdSnapshot snapshot)
    {
        var opportunities = _career.GetJobOpportunities(person);
        if (opportunities.Count == 0)
            return 0m;

        var urgent = snapshot.FinancialState is
            AutonomousFinancialState.Critical or
            AutonomousFinancialState.Poor;

        return opportunities
            .Select(opportunity =>
                AutonomousWorkChoiceRules.GetCareerOpportunityValue(
                    opportunity.AnnualSalary,
                    opportunity.SuccessChance,
                    urgent))
            .DefaultIfEmpty(0m)
            .Max();
    }

    private decimal GetBestKnownCraftIncome(IPerson person)
    {
        var crafts = _context.GetService<ICraftService>();
        if (crafts is null)
            return 0m;

        return crafts.GetKnownCrafts(person)
            .Select(craft => crafts.GetProgress(person, craft.Id)?.ExpectedAnnualIncome ?? 0m)
            .DefaultIfEmpty(0m)
            .Max();
    }

    private decimal GetMarginalFarmWorkValue(
        AutonomousHouseholdSnapshot snapshot,
        IPerson person)
    {
        var farming = _context.GetService<IFarmingService>();
        if (farming is null
            || !farming.IsWorkingFarmWorker(person, snapshot.Head))
        {
            return 0m;
        }

        var farm = farming.GetSnapshot(snapshot.Head);
        if (farm.ExpectedAnnualIncome <= 0m
            || farm.LocalParcelCount <= 0
            || farm.AvailableWorkers <= 0)
        {
            return 0m;
        }

        // A spare eligible worker replaces somebody who leaves farming, so
        // the household gives up no farm income until labour is at or below
        // the local two-workers-per-parcel capacity.
        if (farm.AvailableWorkers > farm.LocalWorkerCapacity)
            return 0m;

        var staffedParcelEquivalent = Math.Min(
            farm.LocalParcelCount,
            farm.AvailableWorkers / 2m);
        if (staffedParcelEquivalent <= 0m)
            return 0m;

        var fullParcelExpectedIncome =
            farm.ExpectedAnnualIncome / staffedParcelEquivalent;

        return fullParcelExpectedIncome * 0.5m;
    }
}
