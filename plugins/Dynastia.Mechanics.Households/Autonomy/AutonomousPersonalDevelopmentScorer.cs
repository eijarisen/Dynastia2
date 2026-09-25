using Dynastia.Contracts;
using static Dynastia.Mechanics.Households.AutonomousScoringHelpers;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousPersonalDevelopmentScorer : IAutonomousActionScorer
{
    private readonly IGamePluginContext _context;

    public AutonomousPersonalDevelopmentScorer(
        IGamePluginContext context)
    {
        _context = context;
    }

    public bool Handles(string actionId)
    {
        var id = actionId.ToLowerInvariant();
        return id switch
        {
            "personality.religious_study" => true,
            "wellbeing.drink" => true,
            "turn.pass" => true,
            _ when id.StartsWith("stats.", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

    public AutonomousActionCandidate? Score(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var id = option.Action.Id.ToLowerInvariant();

        return id switch
        {
            "personality.religious_study" =>
                ScoreReligiousStudy(option, snapshot),

            "wellbeing.drink" =>
                ScoreDrink(option, snapshot),

            "turn.pass" =>
                ScorePass(option, snapshot),

            _ when id.StartsWith("stats.", StringComparison.OrdinalIgnoreCase) =>
                ScoreSelfImprovement(option, snapshot),

            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreReligiousStudy(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            || snapshot.HasSeriousMedicalDanger)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.Optional,
            AutonomousPriorityBands.OptionalDevelopment,
            snapshot.Head.Tags.Has("morals.good") ? 34 : 20);
    }

    private AutonomousActionCandidate? ScoreDrink(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var head = snapshot.Members.First(member => member.Person.Id == snapshot.Head.Id);
        if (head.Health.Percentage < 85 || snapshot.HasSeriousMedicalDanger
            || snapshot.NeedsFamilyContinuity || snapshot.DependentChildCount > 0)
            return null;

        var stress = _context.GetService<IStressService>()?
            .GetStress(snapshot.Head).Total ?? 0;

        // Alcohol is a risky coping mechanism, not a routine leisure choice.
        // Autonomous households only consider it under substantial stress.
        if (stress < StressScale.FromLegacy(4))
            return null;

        return WithScore(option, AutonomyCategory.Optional,
            AutonomousPriorityBands.OptionalDevelopment,
            8 + StressScale.ToLegacy(stress) * 2);
    }

    private AutonomousActionCandidate? ScoreSelfImprovement(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.FinancialState != AutonomousFinancialState.Secure
            || snapshot.HasSeriousMedicalDanger
            || (snapshot.Finance?.Wealth ?? 0m) - (option.Action.DisplayCost ?? 0m)
                < snapshot.ExpectedExpenses * 2m)
        {
            return null;
        }

        var member = snapshot.Members.FirstOrDefault(candidate => candidate.Person.Id == option.Target.Id);
        var fertilitySupport = option.Action.Id.Equals("stats.improve_fertility",
                StringComparison.OrdinalIgnoreCase)
            && snapshot.NeedsFamilyContinuity
            && (option.Target.Id == snapshot.Head.Id || option.Target.Id == snapshot.Spouse?.Id
                || member is { IsBloodline: true })
            && option.Target.Age >= 18
            && AutonomousReproductiveEligibility.CanParticipateInFamilyLife(option.Target);
        if (fertilitySupport)
        {
            var family = _context.GetService<IFamilyService>();
            if (family is null || member is null
                || family.GetSex(option.Target) == Sex.Female && option.Target.Age > 45
                || GetStat(member.Stats, "fertility") >= 3)
            {
                return null;
            }

            var supportsMaleLine = snapshot.NeedsMaleLineContinuity
                && (member.IsMaleLineage && family.GetSex(option.Target) == Sex.Male
                    || option.Target.Id == snapshot.Spouse?.Id
                        && snapshot.Members.Any(candidate => candidate.Person.Id == snapshot.Head.Id
                            && candidate.IsMaleLineage));
            return WithScore(option, AutonomyCategory.Continuity,
                supportsMaleLine ? AutonomousPriorityBands.MaleLineContinuity
                    : AutonomousPriorityBands.BloodlineContinuity, 58);
        }

        var survivalSupport = option.Action.Id.Equals("stats.improve_immunity", StringComparison.OrdinalIgnoreCase)
            || option.Action.Id.Equals("stats.improve_longevity", StringComparison.OrdinalIgnoreCase);
        if (survivalSupport && member is not null)
        {
            return WithScore(option, AutonomyCategory.Survival,
                member.IsMaleLineage || member.IsBloodline
                    ? AutonomousPriorityBands.FamilyStability
                    : AutonomousPriorityBands.LongTermImprovement, 48);
        }

        if (snapshot.NeedsFamilyContinuity && snapshot.HasRealisticReproductivePath)
            return null;

        return WithScore(option, AutonomyCategory.PersonalDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 38);
    }

    private AutonomousActionCandidate ScorePass(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var quietYear = !snapshot.HasSeriousMedicalDanger
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            && (!snapshot.HasRealisticReproductivePath || !snapshot.NeedsFamilyContinuity)
            && snapshot.Status?.IsLargeFamilyStrained != true;

        return WithScore(option, AutonomyCategory.Optional,
            AutonomousPriorityBands.OptionalDevelopment,
            quietYear ? 42 : 8);
    }
}
