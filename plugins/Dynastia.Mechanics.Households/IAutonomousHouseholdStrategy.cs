using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal interface IAutonomousHouseholdStrategy
{
    AutonomousHouseholdSnapshot BuildSnapshot(
        HouseholdInfo household);

    IReadOnlyList<AutonomousActionCandidate> GetAvailableActions(
        AutonomousHouseholdSnapshot snapshot);

    AutonomousActionCandidate? ScoreAction(
        AutonomousActionCandidate action,
        AutonomousHouseholdSnapshot snapshot);

    AutonomousActionCandidate? ChooseAction(
        IReadOnlyList<AutonomousActionCandidate> scoredActions);

    bool QueueAction(
        AutonomousActionCandidate action,
        AutonomousHouseholdSnapshot snapshot);
}
