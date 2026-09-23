namespace Dynastia.Mechanics.Households;

/// <summary>Scores an already-available candidate; never discovers or executes actions.</summary>
internal interface IAutonomousActionScorer
{
    bool Handles(string actionId);

    AutonomousActionCandidate? Score(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot);
}
