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
            && snapshot.NeedsFamilyExpansion
            && satisfaction < 45)
        {
            return WithScore(option, AutonomyCategory.RelationshipStability,
                AutonomousPriorityBands.SustainableFamilyContinuity,
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
            || !snapshot.NeedsFamilyExpansion
            || snapshot.HasImmediateMedicalDanger
            || snapshot.HasMaterialUnmetDependentNeed)
            return null;

        // A reproductively useful marriage that is nearing the automatic
        // divorce threshold must be repaired before another deliberate
        // conception attempt.
        if (snapshot.MarriageSatisfaction is < 45)
            return null;

        var score = snapshot.ExistingChildCount == 0 ? 96.0 : 68.0;
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

        if (snapshot.NeedsFamilyExpansion
            && _reproductiveEligibility.CanSearchForReproductiveSpouse(
                snapshot.Head, GetStat(stats, "fertility"))
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return WithScore(option, AutonomyCategory.Continuity,
                AutonomousPriorityBands.SustainableFamilyContinuity,
                snapshot.ExistingChildCount == 0 ? 92 : 62);
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
            return WithScore(option, AutonomyCategory.ChildProtection,
                AutonomousPriorityBands.FamilyStability,
                happiness == 1 ? 82 : 68);
        }

        return WithScore(option, AutonomyCategory.ChildProtection,
            AutonomousPriorityBands.FamilyStability,
            snapshot.NeedsFamilyExpansion ? 34 : 44);
    }

    private AutonomousActionCandidate? ScoreArrangeMarriage(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var person = option.Target;
        if (!AutonomousReproductiveEligibility.CanParticipateInFamilyLife(person)
            || person.Age < 18
            || _family.GetSpouse(person) is { } spouse && spouse.Tags.Has("state.alive"))
        {
            return null;
        }

        var isExistingChild = snapshot.ExistingChildren.Any(child => child.Id == person.Id);
        if (!isExistingChild)
        {
            return WithScore(option, AutonomyCategory.FamilyRelations,
                AutonomousPriorityBands.LongTermImprovement, 42);
        }

        // A resident son's spouse joins his current household. If that would
        // immediately overcrowd the home, establish the son first and let his
        // own autonomous household perform the ordinary spouse search later.
        if (_family.GetSex(person) == Sex.Male
            && snapshot.Status is { } status
            && status.ResidentCount + 1 > status.OvercrowdingThreshold)
        {
            return null;
        }

        var score = 82.0;
        if (_family.GetSex(person) == Sex.Female && person.Age >= 35)
            score += 12;
        if (snapshot.HasAdultFamilyFormationNeed)
            score += 8;

        return WithScore(option, AutonomyCategory.Continuity,
            AutonomousPriorityBands.SustainableFamilyContinuity, score);
    }

    private static int ContinuityBand(AutonomousHouseholdSnapshot snapshot) =>
        AutonomousPriorityBands.SustainableFamilyContinuity;

    private IReadOnlyDictionary<string, int> GetStats(IPerson person) =>
        _stats.GetStats(person)
            .ToDictionary(stat => stat.Id, stat => stat.Value,
                StringComparer.OrdinalIgnoreCase);
}
