using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class EconomyViewModel
{
    public EconomyViewModel(
        HouseholdFinanceSnapshot snapshot,
        HouseholdStatusSnapshot? status)
    {
        Wealth = snapshot.Wealth;
        HousesOwned = snapshot.HousesOwned;
        RentedHouses = snapshot.RentedHouses;
        PendingInheritance = snapshot.PendingInheritance;
        PendingHouses = snapshot.PendingHouses;
        LastIncome = snapshot.LastIncome;
        LastExpenses = snapshot.LastExpenses;

        NannyText =
            status?.HasNannyReference == true
                ? $"Nanny: {status.NannyName ?? "Unknown"}"
                : "Nanny: None";

        WarningText =
            status is null
                ? string.Empty
                : string.Join(
                    Environment.NewLine,
                    status.Warnings);
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
        HousesOwned == 0
            ? "Residence: Renting"
            : RentedHouses > 0
                ? $"Rented out: {RentedHouses}"
                : "Rented out: 0";

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

    public string NannyText { get; }

    public string WarningText { get; }
}
