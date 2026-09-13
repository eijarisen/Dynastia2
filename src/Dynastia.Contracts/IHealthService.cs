namespace Dynastia.Contracts;

public interface IHealthService
{
    void EnsureHealth(IPerson person);

    HealthSnapshot GetHealth(IPerson person);

    void SetHealth(
        IPerson person,
        double value);

    void ChangeHealth(
        IPerson person,
        double amount);

    // Used only when a mechanic intentionally bypasses the upper cap.
    // The lower bound remains a hard invariant: health can never be negative.
    void ChangeHealthUnclamped(
        IPerson person,
        double amount);

    bool HasCondition(
        IPerson person,
        string conditionId);

    bool AddCondition(
        IPerson person,
        string conditionId);

    bool RemoveCondition(
        IPerson person,
        string conditionId);
}
