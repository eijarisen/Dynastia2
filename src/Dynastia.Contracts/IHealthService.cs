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
}
