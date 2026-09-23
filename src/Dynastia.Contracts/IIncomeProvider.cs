namespace Dynastia.Contracts;

public interface IIncomeProvider
{
    string Id { get; }

    decimal GetAnnualIncome(
        IPerson person);

    decimal GetExpectedAnnualIncome(
        IPerson person) =>
        GetAnnualIncome(person);

    decimal GetExpectedPassiveAnnualIncome(
        IPerson person) =>
        0m;
}
