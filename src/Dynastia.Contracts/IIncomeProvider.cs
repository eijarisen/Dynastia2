namespace Dynastia.Contracts;

public interface IIncomeProvider
{
    string Id { get; }

    decimal GetAnnualIncome(
        IPerson person);
}
