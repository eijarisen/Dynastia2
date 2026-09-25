using Dynastia.Contracts;
using static Dynastia.Mechanics.Households.AutonomousScoringHelpers;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousHealthScorer : IAutonomousActionScorer
{
    public bool Handles(string actionId)
    {
        var id = actionId.ToLowerInvariant();
        return id switch
        {
            "wellbeing.heal_relative" => true,
            "wellbeing.therapy" => true,
            "wellbeing.recover" => true,
            "career.ask_to_recover" => true,
            "household.hire_nanny" or
            "household.ask_daughter_nanny" => true,
            "household.fire_nanny" => true,
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
            "wellbeing.heal_relative" =>
                ScoreMedicalTreatment(option, snapshot, targetMember),

            "wellbeing.therapy" =>
                ScoreTherapy(option, snapshot, targetMember),

            "wellbeing.recover" =>
                ScoreRecover(option, snapshot),

            "career.ask_to_recover" =>
                ScoreRequestedRecover(option, snapshot, targetMember),

            "household.hire_nanny" or
            "household.ask_daughter_nanny" =>
                ScoreNanny(option, snapshot),

            "household.fire_nanny" =>
                ScoreFireNanny(option, snapshot),

            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreMedicalTreatment(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null)
            return null;

        var importance = HealthTargetImportance(snapshot, target);

        if (target.IsImmediateHealthRisk)
        {
            return WithScore(
                option,
                AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival,
                100 + importance);
        }

        if (target.IsSeriousHealthRisk)
        {
            var workingAdults = snapshot.Members.Count(member =>
                member.Career?.IsEmployed == true);

            var survivalCriticalAdult =
                target.Person.Id == snapshot.Head.Id
                || target.Career?.IsEmployed == true && workingAdults <= 1
                || snapshot.Spouse?.Id == target.Person.Id
                    && snapshot.NeedsFamilyContinuity
                    && snapshot.HasRealisticReproductivePath;

            var emergency =
                target.IsChild
                || survivalCriticalAdult;

            return WithScore(
                option,
                target.IsChild
                    ? AutonomyCategory.ChildProtection
                    : AutonomyCategory.Survival,
                emergency
                    ? AutonomousPriorityBands.EmergencySurvival
                    : AutonomousPriorityBands.FamilyStability,
                82 + importance);
        }

        if (target.Health.Percentage < 75)
        {
            return WithScore(
                option,
                target.IsChild
                    ? AutonomyCategory.ChildProtection
                    : AutonomyCategory.PersonalDevelopment,
                target.IsChild
                    ? AutonomousPriorityBands.FamilyStability
                    : AutonomousPriorityBands.LongTermImprovement,
                55 + importance * 0.5);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreTherapy(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null)
            return null;

        var dangerous = target.Health.Conditions.Any(condition =>
            (condition.Id.Equals("alcoholism", StringComparison.OrdinalIgnoreCase)
             || condition.Id.Equals("drug_dependence", StringComparison.OrdinalIgnoreCase))
            && target.Health.Percentage <= 60);

        return WithScore(
            option,
            AutonomyCategory.Survival,
            dangerous
                ? AutonomousPriorityBands.EmergencySurvival
                : AutonomousPriorityBands.FamilyStability,
            dangerous ? 72 : 58);
    }

    private AutonomousActionCandidate? ScoreRecover(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var head = snapshot.Members.First(member => member.Person.Id == snapshot.Head.Id);

        if (head.IsImmediateHealthRisk)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival, 78);
        }

        if (head.IsSeriousHealthRisk)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival,
                72 + (head.Career?.JobSatisfaction == 1 ? 10 : 0));
        }

        if (head.Health.Percentage < 65)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.FamilyStability,
                66 + (head.Career?.JobSatisfaction == 1 ? 10 : 0));
        }

        if (head.Career?.JobSatisfaction == 1
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return WithScore(option, AutonomyCategory.PersonalDevelopment,
                AutonomousPriorityBands.LongTermImprovement, 52);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreRequestedRecover(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null)
            return null;

        if (target.IsImmediateHealthRisk)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival, 65 + HealthTargetImportance(snapshot, target));
        }

        if (target.IsSeriousHealthRisk || target.Health.Percentage < 65)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.FamilyStability, 62);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreNanny(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var needsCapacity = snapshot.NeedsFamilyContinuity
            && snapshot.HasRealisticReproductivePath
            && snapshot.Status is { } status
            && snapshot.DependentChildCount >= status.EffectiveChildCapacity;
        if (snapshot.Status?.IsLargeFamilyStrained != true && !needsCapacity)
            return null;

        var band = snapshot.HasSeriousMedicalDanger
            ? AutonomousPriorityBands.EmergencySurvival
            : needsCapacity
                ? snapshot.NeedsMaleLineContinuity
                    ? AutonomousPriorityBands.MaleLineContinuity
                    : AutonomousPriorityBands.BloodlineContinuity
                : AutonomousPriorityBands.FamilyStability;

        return WithScore(option, AutonomyCategory.ChildProtection, band,
            snapshot.HasSeriousMedicalDanger ? 82 : 90);
    }

    private AutonomousActionCandidate? ScoreFireNanny(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.Status is not
            {
                HasNannyReference: true,
                IsLargeFamilyStrained: false
            })
        {
            return null;
        }

        // Removing childcare must not reintroduce the strain it just solved.
        if (snapshot.Status.UnderageChildren > snapshot.Status.BaseChildCapacity)
            return null;

        if (snapshot.FinancialState is AutonomousFinancialState.Critical
            or AutonomousFinancialState.Poor)
        {
            return WithScore(option, AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 68);
        }

        return null;
    }

    private double HealthTargetImportance(
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot target)
    {
        var score = Math.Max(0, 100 - target.Health.Percentage) * 0.25;

        if (target.Person.Id == snapshot.Head.Id)
            score += 16;

        if (target.Career?.IsEmployed == true)
        {
            var workingAdults = snapshot.Members.Count(member => member.Career?.IsEmployed == true);
            if (workingAdults <= 1)
                score += 18;
        }

        if (target.IsChild)
        {
            score += 12;
            if (!snapshot.HasRealisticReproductivePath)
                score += 24;
            if (target.Person.Age <= 10)
                score += 6;
        }

        if (snapshot.Spouse?.Id == target.Person.Id
            && snapshot.NeedsFamilyContinuity
            && snapshot.HasRealisticReproductivePath)
        {
            score += 20;
        }

        if (target.Health.Conditions.Any(condition =>
            condition.Type.Equals("terminal", StringComparison.OrdinalIgnoreCase)))
        {
            score += 24;
        }

        return score;
    }
}
