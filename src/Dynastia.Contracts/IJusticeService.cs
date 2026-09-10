namespace Dynastia.Contracts;

public interface IJusticeService
{
    void EnsureJustice(
        IPerson person);

    JusticeSnapshot GetStatus(
        IPerson person);

    bool IsImprisoned(
        IPerson person);
}
