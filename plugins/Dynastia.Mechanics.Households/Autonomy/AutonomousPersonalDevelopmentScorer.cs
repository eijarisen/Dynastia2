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
        if (head.Health.Percentage < 85 || snapshot.HasSeriousMedicalDanger)
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
            || snapshot.LivingChildCount < 2 && snapshot.HasRealisticReproductivePath)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.PersonalDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 38);
    }

    private AutonomousActionCandidate ScorePass(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var quietYear = !snapshot.HasSeriousMedicalDanger
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            && (!snapshot.HasRealisticReproductivePath || snapshot.LivingChildCount >= 2)
            && snapshot.Status?.IsLargeFamilyStrained != true;

        return WithScore(option, AutonomyCategory.Optional,
            AutonomousPriorityBands.OptionalDevelopment,
            quietYear ? 42 : 8);
    }
}
