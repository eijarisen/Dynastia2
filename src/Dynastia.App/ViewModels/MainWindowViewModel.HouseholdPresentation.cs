using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public string HouseholdBudgetText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            return finance is null
                ? "No active adult household."
                : $"Family Budget: " +
                  $"{finance.Wealth:N0} zł";
        }
    }

    public string HouseholdIncomeText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            if (finance is null)
                return string.Empty;

            var head =
                GetDisplayedHouseholdHead();

            var projected =
                head is null
                    ? finance.LastIncome
                    : (_economyService?.GetProjectedAnnualIncome(head)
                        ?? finance.LastIncome)
                      + GetProjectedLoanIncome(head);

            return $"Income: {projected:N0} zł";
        }
    }

    public string HouseholdIncomeDetailsText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            if (finance is null)
                return string.Empty;

            var head =
                GetDisplayedHouseholdHead();

            var projected =
                (head is null
                    ? finance.LastIncomeBreakdown
                    : _economyService?.GetProjectedIncomeBreakdown(head)
                        ?? finance.LastIncomeBreakdown)
                .ToList();

            if (head is not null)
            {
                var loanIncome =
                    GetProjectedLoanIncome(head);

                if (loanIncome > 0)
                {
                    projected.Add(
                        new FinanceBreakdownItem(
                            "loan repayments",
                            loanIncome));
                }
            }

            return FormatFinanceBreakdown(
                projected,
                "No current recurring income.");
        }
    }

    public string HouseholdExpensesText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            return finance is null
                ? string.Empty
                : $"Expenses: " +
                  $"{finance.LastExpenses:N0} zł";
        }
    }

    public string HouseholdExpenseDetailsText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            if (finance is null)
                return string.Empty;

            return FormatFinanceBreakdown(
                finance.LastExpenseBreakdown,
                "No expenses were recorded in the last annual finance pass.");
        }
    }

    public string HouseholdHousesText
    {
        get
        {
            var head =
                GetDisplayedHouseholdHead();

            var finance =
                GetDisplayedHouseholdFinance();

            if (head is null || finance is null)
                return string.Empty;

            var farmlandCount =
                _farmingService?.GetSnapshot(head).TotalParcelCount
                ?? 0;

            return $"Houses: {finance.Houses.Count}   •   Farmland: {farmlandCount}";
        }
    }

    public string HouseholdHousesDetailsText
    {
        get
        {
            var head =
                GetDisplayedHouseholdHead();

            var finance =
                GetDisplayedHouseholdFinance();

            if (head is null
                || finance is null)
            {
                return string.Empty;
            }

            var lines = new List<string>();

            if (finance.Houses.Count == 0)
            {
                var homeTown =
                    _locationService?
                        .GetLocation(
                            head)
                        .HomeTown
                        .Town;

                lines.Add(
                    string.IsNullOrWhiteSpace(homeTown)
                        ? "No owned houses. The household rents its residence."
                        : $"{homeTown} — Renting");
            }
            else
            {
                lines.AddRange(
                    finance.Houses.Select(
                        house =>
                            $"{house.Town.Town} — " +
                            $"{house.Status}"));

                if (!finance.Houses.Any(house => house.IsResidence))
                {
                    var homeTown =
                        _locationService?
                            .GetLocation(head)
                            .HomeTown
                            .Town;

                    if (!string.IsNullOrWhiteSpace(homeTown))
                    {
                        lines.Insert(
                            0,
                            $"{homeTown} — Renting");
                    }
                }
            }

            var farming =
                _farmingService?.GetSnapshot(head);

            if (farming is { TotalParcelCount: > 0 })
            {
                lines.Add(string.Empty);
                lines.Add($"Farmland: {farming.TotalParcelCount} parcel" +
                    $"{(farming.TotalParcelCount == 1 ? string.Empty : "s")}");

                foreach (var group in farming.Farmland
                    .GroupBy(asset => asset.Town.Id, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.First().Town.Town, StringComparer.CurrentCultureIgnoreCase))
                {
                    var first = group.First();
                    var local = group.Key.Equals(
                        farming.ResidenceTownId,
                        StringComparison.OrdinalIgnoreCase);
                    lines.Add(
                        $"{first.Town.Town} ×{group.Count()} — " +
                        (local ? "Local" : "Remote / idle"));
                }
            }

            return string.Join(
                Environment.NewLine,
                lines);
        }
    }

    // Retained for compatibility with older bindings/packages.
    public string HouseholdIncomeExpensesText =>
        string.Join(
            " | ",
            new[]
            {
                HouseholdIncomeText,
                HouseholdExpensesText
            }
            .Where(
                value =>
                    !string.IsNullOrWhiteSpace(
                        value)));

    private decimal GetProjectedLoanIncome(
        IPerson householdRepresentative)
    {
        return _loanService?
            .GetLoansGiven(householdRepresentative)
            .Sum(loan => loan.AnnualPayment)
            ?? 0m;
    }

    private HouseholdFinanceSnapshot?
        GetDisplayedHouseholdFinance()
    {
        var head =
            GetDisplayedHouseholdHead();

        return head is null
            ? null
            : _economyService?.GetHousehold(
                head);
    }

    private static string FormatFinanceBreakdown(
        IReadOnlyList<FinanceBreakdownItem> items,
        string emptyText)
    {
        if (items.Count == 0)
            return emptyText;

        return string.Join(
            Environment.NewLine,
            items.Select(
                item =>
                    $"{item.Amount:N0} zł — " +
                    $"{item.Label}"));
    }

    public string HouseholdWarningText
    {
        get
        {
            var head =
                GetDisplayedHouseholdHead();

            var status =
                head is null
                    ? null
                    : _householdService?.GetStatus(
                        head);

            return status is null
                ? string.Empty
                : string.Join(
                    Environment.NewLine,
                    status.Warnings);
        }
    }

    public string ExtendedFamilyEmptyText =>
        ExtendedFamilyMembers.Count == 0
            ? "No extended family members in this household."
            : string.Empty;

    public string DeceasedFamilyEmptyText =>
        DeceasedFamilyMembers.Count == 0
            ? "No deceased family members."
            : string.Empty;

}
