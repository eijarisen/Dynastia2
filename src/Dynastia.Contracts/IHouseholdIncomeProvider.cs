namespace Dynastia.Contracts;

public interface IHouseholdIncomeProvider
{
    string Id { get; }
    string Label { get; }

    decimal GetAnnualIncome(
        IPerson householdRepresentative);

    decimal GetExpectedAnnualIncome(
        IPerson householdRepresentative);

    decimal GetExpectedPassiveAnnualIncome(
        IPerson householdRepresentative) =>
        0m;
}
