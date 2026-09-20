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



    public IReadOnlyList<HouseholdBudgetHistoryPoint> HouseholdBudgetHistory =>
        GetDisplayedHouseholdFinance()?.History
        ?? Array.Empty<HouseholdBudgetHistoryPoint>();

    public string HouseholdBudgetHistoryRangeText
    {
        get
        {
            var history = HouseholdBudgetHistory;
            if (history.Count == 0)
                return string.Empty;
            return history.Count == 1
                ? history[0].Year.ToString()
                : $"{history[0].Year}–{history[^1].Year}";
        }
    }

    public string HouseholdLastYearText
    {
        get
        {
            var finance = GetDisplayedHouseholdFinance();
            return finance is null
                ? string.Empty
                : $"Last Year: Income: {finance.LastIncome:N0} zł   Expenses: {finance.LastExpenses:N0} zł";
        }
    }

    public string HouseholdLastYearDetailsText
    {
        get
        {
            var finance = GetDisplayedHouseholdFinance();
            if (finance is null)
                return string.Empty;

            var income = FormatFinanceBreakdown(
                finance.LastIncomeBreakdown,
                "No income was recorded last year.");
            var expenses = FormatFinanceBreakdown(
                finance.LastExpenseBreakdown,
                "No expenses were recorded last year.");

            return string.Join(
                Environment.NewLine,
                "Income",
                income,
                string.Empty,
                "Expenses",
                expenses);
        }
    }

    public string HouseholdIncomeText
    {
        get
        {
            var finance = GetDisplayedHouseholdFinance();
            return finance is null
                ? string.Empty
                : $"Income (last year): {finance.LastIncome:N0} zł";
        }
    }

    public string HouseholdIncomeDetailsText
    {
        get
        {
            var finance = GetDisplayedHouseholdFinance();
            return finance is null
                ? string.Empty
                : FormatFinanceBreakdown(
                    finance.LastIncomeBreakdown,
                    "No income was recorded last year.");
        }
    }

    public string HouseholdExpensesText
    {
        get
        {
            var finance =
                GetDisplayedHouseholdFinance();

            if (finance is null)
                return string.Empty;

            var head = GetDisplayedHouseholdHead();
            var projected = head is null
                ? finance.LastExpenses
                : _economyService?.GetAnnualForecast(head)?.ProjectedExpenses
                    ?? finance.LastExpenses;

            return $"Expenses: {projected:N0} zł";
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

            var head = GetDisplayedHouseholdHead();
            var projected = head is null
                ? finance.LastExpenseBreakdown
                : _economyService?.GetAnnualForecast(head)?.ExpenseBreakdown
                    ?? finance.LastExpenseBreakdown;

            return FormatFinanceBreakdown(
                projected,
                "No projected annual expenses.");
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
        var visible = items
            .Where(item => item.Amount != 0m)
            .ToList();

        if (visible.Count == 0)
            return emptyText;

        return string.Join(
            Environment.NewLine,
            visible.Select(
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
