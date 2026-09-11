namespace Dynastia.Contracts;

public interface IMarriageSatisfactionService
{
    MarriageSatisfactionSnapshot? GetSatisfaction(
        IPerson person);

    void InitializeMarriage(
        IPerson first,
        IPerson second,
        int startYear);

    void ChangeSatisfaction(
        IPerson person,
        double amount);
}
