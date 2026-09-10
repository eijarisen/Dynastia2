using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class EconomyViewModel
{
    public EconomyViewModel(
        HouseholdFinanceSnapshot snapshot)
    {
        Wealth =
            snapshot.Wealth;

        HousesOwned =
            snapshot.HousesOwned;

        RentedHouses =
            snapshot.RentedHouses;

        PendingInheritance =
            snapshot.PendingInheritance;

        PendingHouses =
            snapshot.PendingHouses;

        LastIncome =
            snapshot.LastIncome;

        LastExpenses =
            snapshot.LastExpenses;
    }

    public decimal Wealth { get; }

    public int HousesOwned { get; }

    public int RentedHouses { get; }

    public decimal PendingInheritance { get; }

    public int PendingHouses { get; }

    public decimal LastIncome { get; }

    public decimal LastExpenses { get; }

    public string WealthText =>
        $"Budget: ${Wealth:N0}";

    public string HousesText =>
        $"Houses: {HousesOwned}";

    public string RentedHousesText =>
        $"Rented: {RentedHouses}";

    public string IncomeText =>
        $"Income: ${LastIncome:N0}";

    public string ExpensesText =>
        $"Expenses: ${LastExpenses:N0}";

    public string NetText =>
        $"Net: ${(LastIncome - LastExpenses):N0}";

    public string PendingInheritanceText =>
        $"Pending inheritance: ${PendingInheritance:N0}";

    public string PendingHousesText =>
        $"Pending houses: {PendingHouses}";
}
