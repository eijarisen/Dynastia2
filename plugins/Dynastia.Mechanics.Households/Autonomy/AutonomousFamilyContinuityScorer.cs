using Dynastia.Contracts;
using static Dynastia.Mechanics.Households.AutonomousScoringHelpers;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousFamilyContinuityScorer : IAutonomousActionScorer
{
    private readonly IGamePluginContext _context;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly AutonomousReproductiveEligibility _reproductiveEligibility;

    public AutonomousFamilyContinuityScorer(
        IGamePluginContext context,
        IFamilyService family,
        IStatsService stats,
        AutonomousReproductiveEligibility reproductiveEligibility)
    {
        _context = context;
        _family = family;
        _stats = stats;
        _reproductiveEligibility = reproductiveEligibility;
    }

    public bool Handles(string actionId)
    {
        var id = actionId.ToLowerInvariant();
        return id switch
        {
            "relationship.repair_marriage" => true,
            "reproduction.try_for_baby" => true,
            "relationship.find_spouse" => true,
            "childhood.raise_child" => true,
            "relationship.marry_off_daughter" => true,
            "relationship.marry_off_son" => true,
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
            "relationship.repair_marriage" =>
                ScoreRepairMarriage(option, snapshot),

            "reproduction.try_for_baby" =>
                ScoreTryForBaby(option, snapshot),

            "relationship.find_spouse" =>
                ScoreFindSpouse(option, snapshot),

            "childhood.raise_child" =>
                ScoreRaiseChild(option, snapshot, targetMember),

            "relationship.marry_off_daughter" =>
                ScoreArrangeMarriage(option, snapshot),

            "relationship.marry_off_son" =>
                ScoreArrangeMarriage(option, snapshot),

            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreRepairMarriage(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var satisfaction = snapshot.MarriageSatisfaction;
        if (satisfaction is null)
            return null;

        if (snapshot.HasRealisticReproductivePath
            && snapshot.NeedsFamilyContinuity
            && satisfaction < 45)
        {
            return WithScore(option, AutonomyCategory.RelationshipStability,
                ContinuityBand(snapshot),
                satisfaction < 25 ? 110 : 100);
        }

        if (satisfaction < 35)
        {
            return WithScore(option, AutonomyCategory.RelationshipStability,
                AutonomousPriorityBands.FamilyStability, 90);
        }

        if (satisfaction < 55)
        {
            return WithScore(option, AutonomyCategory.RelationshipStability,
                AutonomousPriorityBands.FamilyStability, 62);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreTryForBaby(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!snapshot.CanActivelyTryForChild
            || !snapshot.NeedsFamilyContinuity
            || snapshot.HasSeriousMedicalDanger)
            return null;

        // A reproductively useful marriage that is nearing the automatic
        // divorce threshold must be repaired before another deliberate
        // conception attempt.
        if (snapshot.MarriageSatisfaction is < 45)
            return null;

        var viableDescendants = snapshot.NeedsMaleLineContinuity
            ? snapshot.ViableMaleLineDescendantCount
            : snapshot.ViableBloodlineDescendantCount;
        var score = viableDescendants == 0 ? 96.0 : 68.0;
        score += snapshot.ReproductiveUrgency * 18;

        return WithScore(option, AutonomyCategory.Continuity,
            ContinuityBand(snapshot), score);
    }

    private AutonomousActionCandidate? ScoreFindSpouse(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (_family.GetSex(snapshot.Head) != Sex.Male
            || !AutonomousReproductiveEligibility.CanParticipateInFamilyLife(snapshot.Head)
            || snapshot.Spouse is not null)
            return null;

        var stats = GetStats(snapshot.Head);
        var appeal = GetStat(stats, "appeal");
        var strength = GetStat(stats, "strength");
        var intellect = GetStat(stats, "intellect");
        var weakCareerProspects = Math.Max(strength, intellect) <= 2;

        if ((snapshot.FinancialState is AutonomousFinancialState.Critical
                or AutonomousFinancialState.Poor)
            && snapshot.Head.Age < 60
            && appeal >= 4
            && weakCareerProspects)
        {
            return WithScore(option, AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency,
                snapshot.FinancialState == AutonomousFinancialState.Critical ? 92 : 84);
        }

        if (snapshot.NeedsFamilyContinuity
            && _reproductiveEligibility.CanSearchForReproductiveSpouse(
                snapshot.Head, GetStat(stats, "fertility"))
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return WithScore(option, AutonomyCategory.Continuity,
                ContinuityBand(snapshot),
                (snapshot.NeedsMaleLineContinuity
                    ? snapshot.ViableMaleLineDescendantCount
                    : snapshot.ViableBloodlineDescendantCount) == 0 ? 92 : 62);
        }

        if (snapshot.LivingBloodlineDescendants.Count > 0
            && snapshot.Head.Age >= 60
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return WithScore(option, AutonomyCategory.Optional,
                AutonomousPriorityBands.OptionalDevelopment, 8);
        }

        return WithScore(option, AutonomyCategory.Optional,
            AutonomousPriorityBands.OptionalDevelopment,
            snapshot.Head.Age < 60 ? 28 : 10);
    }

    private AutonomousActionCandidate? ScoreRaiseChild(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null || !target.IsChild
            || snapshot.FinancialState is AutonomousFinancialState.Critical
                or AutonomousFinancialState.Poor)
        {
            return null;
        }

        var happiness =
            _context.GetService<IChildHappinessService>()?
                .GetHappiness(target.Person)?
                .Value
            ?? 3;

        if (happiness <= 2)
        {
            var priority = _family.IsMaleLineage(target.Person)
                && _family.GetSex(target.Person) == Sex.Male
                    ? AutonomousPriorityBands.MaleLineContinuity
                    : _family.IsBloodline(target.Person)
                        ? AutonomousPriorityBands.BloodlineContinuity
                        : AutonomousPriorityBands.FamilyStability;
            return WithScore(option, AutonomyCategory.ChildProtection,
                priority,
                happiness == 1 ? 82 : 68);
        }

        return WithScore(option, AutonomyCategory.ChildProtection,
            AutonomousPriorityBands.LongTermImprovement,
            snapshot.HasRealisticReproductivePath ? 34 : 44);
    }

    private AutonomousActionCandidate? ScoreArrangeMarriage(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            || snapshot.HasSeriousMedicalDanger)
        {
            return null;
        }

        var person = option.Target;
        if (!AutonomousReproductiveEligibility.CanParticipateInFamilyLife(person)
            || person.Age < 18
            || _family.GetSpouse(person) is { } spouse && spouse.Tags.Has("state.alive"))
        {
            return null;
        }

        var fertility = GetStat(GetStats(person), "fertility");
        var canContinue = _family.GetSex(person) == Sex.Male
            ? _reproductiveEligibility.CanSearchForReproductiveSpouse(person, fertility)
            : person.Age <= 45 && fertility > 0 && !person.Tags.Has("sexuality.homosexual");
        if (!canContinue)
            return null;

        if (_family.IsMaleLineage(person) && _family.GetSex(person) == Sex.Male)
        {
            return WithScore(option, AutonomyCategory.Continuity,
                AutonomousPriorityBands.MaleLineContinuity, 94);
        }

        if (_family.IsBloodline(person))
        {
            return WithScore(option, AutonomyCategory.Continuity,
                AutonomousPriorityBands.BloodlineContinuity,
                person.Age >= 35 && _family.GetSex(person) == Sex.Female ? 94 : 82);
        }

        return WithScore(option, AutonomyCategory.FamilyRelations,
            AutonomousPriorityBands.LongTermImprovement, 42);
    }

    private static int ContinuityBand(AutonomousHouseholdSnapshot snapshot) =>
        snapshot.NeedsMaleLineContinuity
            ? AutonomousPriorityBands.MaleLineContinuity
            : AutonomousPriorityBands.BloodlineContinuity;

    private IReadOnlyDictionary<string, int> GetStats(IPerson person) =>
        _stats.GetStats(person)
            .ToDictionary(stat => stat.Id, stat => stat.Value,
                StringComparer.OrdinalIgnoreCase);
}
