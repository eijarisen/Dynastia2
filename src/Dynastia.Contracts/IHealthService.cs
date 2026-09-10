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

    // Used only when the source intentionally allows health
    // to temporarily exceed max health before the annual
    // health pass performs its final cap.
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
