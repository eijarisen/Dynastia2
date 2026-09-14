using System.Collections.ObjectModel;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class FamilyInventoryWindowViewModel :
    ViewModelBase
{
    private readonly MainWindowViewModel _main;

    private string _householdName = string.Empty;
    private string _budgetText = string.Empty;
    private string _incomeTotalText = string.Empty;
    private string _expenseTotalText = string.Empty;
    private string _loanEmptyText = string.Empty;
    private string _houseEmptyText = string.Empty;
    private bool _canTakeLoan;
    private bool _canGiveLoan;
    private bool _canBuyHouse;
    private bool _canSellHouse;

    public FamilyInventoryWindowViewModel(
        MainWindowViewModel main)
    {
        _main = main;
        Refresh();
    }

    public ObservableCollection<InventoryFinanceLineViewModel>
        Incomes { get; } = [];

    public ObservableCollection<InventoryFinanceLineViewModel>
        Expenses { get; } = [];

    public ObservableCollection<InventoryLoanLineViewModel>
        Loans { get; } = [];

    public ObservableCollection<InventoryHouseViewModel>
        Houses { get; } = [];

    public string HouseholdName
    {
        get => _householdName;
        private set
        {
            if (_householdName == value)
                return;

            _householdName = value;
            OnPropertyChanged();
        }
    }

    public string BudgetText
    {
        get => _budgetText;
        private set
        {
            if (_budgetText == value)
                return;

            _budgetText = value;
            OnPropertyChanged();
        }
    }

    public string IncomeTotalText
    {
        get => _incomeTotalText;
        private set
        {
            if (_incomeTotalText == value)
                return;

            _incomeTotalText = value;
            OnPropertyChanged();
        }
    }

    public string ExpenseTotalText
    {
        get => _expenseTotalText;
        private set
        {
            if (_expenseTotalText == value)
                return;

            _expenseTotalText = value;
            OnPropertyChanged();
        }
    }

    public string LoanEmptyText
    {
        get => _loanEmptyText;
        private set
        {
            if (_loanEmptyText == value)
                return;

            _loanEmptyText = value;
            OnPropertyChanged();
        }
    }

    public string HouseEmptyText
    {
        get => _houseEmptyText;
        private set
        {
            if (_houseEmptyText == value)
                return;

            _houseEmptyText = value;
            OnPropertyChanged();
        }
    }

    public bool CanTakeLoan
    {
        get => _canTakeLoan;
        private set
        {
            if (_canTakeLoan == value)
                return;

            _canTakeLoan = value;
            OnPropertyChanged();
        }
    }

    public bool CanGiveLoan
    {
        get => _canGiveLoan;
        private set
        {
            if (_canGiveLoan == value)
                return;

            _canGiveLoan = value;
            OnPropertyChanged();
        }
    }

    public bool CanBuyHouse
    {
        get => _canBuyHouse;
        private set
        {
            if (_canBuyHouse == value)
                return;

            _canBuyHouse = value;
            OnPropertyChanged();
        }
    }

    public bool CanSellHouse
    {
        get => _canSellHouse;
        private set
        {
            if (_canSellHouse == value)
                return;

            _canSellHouse = value;
            OnPropertyChanged();
        }
    }

    public void Refresh()
    {
        var data =
            _main.GetFamilyInventoryData();

        Incomes.Clear();
        Expenses.Clear();
        Loans.Clear();
        Houses.Clear();

        if (data is null)
        {
            HouseholdName = "No active household";
            BudgetText = "Budget: —";
            IncomeTotalText = "Income: —";
            ExpenseTotalText = "Expenses: —";
            LoanEmptyText = "No loans.";
            HouseEmptyText = "No owned houses.";
            CanTakeLoan = false;
            CanGiveLoan = false;
            CanBuyHouse = false;
            CanSellHouse = false;
            return;
        }

        HouseholdName = data.HouseholdName;
        BudgetText = $"Total Budget: {data.Budget:N0} zł";
        IncomeTotalText = $"Income: {data.IncomeTotal:N0} zł";
        ExpenseTotalText = $"Expenses: {data.ExpenseTotal:N0} zł";

        foreach (var line in data.IncomeLines)
        {
            Incomes.Add(
                new InventoryFinanceLineViewModel(
                    line.Label,
                    line.Amount));
        }

        foreach (var line in data.ExpenseLines)
        {
            Expenses.Add(
                new InventoryFinanceLineViewModel(
                    line.Label,
                    line.Amount));
        }

        foreach (var debt in data.Debts)
        {
            Loans.Add(
                new InventoryLoanLineViewModel(
                    $"Debt — {debt.CreditorName}",
                    debt.RemainingAmount,
                    debt.AnnualPayment,
                    debt.YearsRemaining));
        }

        foreach (var loan in data.LoansGiven)
        {
            var borrowerName =
                string.IsNullOrWhiteSpace(
                    loan.BorrowerName)
                    ? "Outside Borrower"
                    : loan.BorrowerName;

            Loans.Add(
                new InventoryLoanLineViewModel(
                    $"Loan Given — {borrowerName}",
                    loan.RemainingAmount,
                    loan.AnnualPayment,
                    loan.YearsRemaining));
        }

        var heirOptions =
            new List<HouseHeirOptionViewModel>
            {
                new(
                    null,
                    "Standard inheritance")
            };

        heirOptions.AddRange(
            data.Children.Select(child =>
                new HouseHeirOptionViewModel(
                    child.Id,
                    child.Name)));

        foreach (var house in data.Houses)
        {
            Houses.Add(
                new InventoryHouseViewModel(
                    house,
                    heirOptions,
                    _main.SetHouseInheritanceHeir));
        }

        LoanEmptyText =
            Loans.Count == 0
                ? "No active loans or debts."
                : string.Empty;

        HouseEmptyText =
            Houses.Count == 0
                ? "No owned houses."
                : string.Empty;

        CanTakeLoan = data.CanTakeLoan;
        CanGiveLoan = data.CanGiveLoan;
        CanBuyHouse = data.CanBuyHouse;
        CanSellHouse = data.CanSellHouse;
    }
}

public sealed class InventoryFinanceLineViewModel
{
    public InventoryFinanceLineViewModel(
        string label,
        decimal amount)
    {
        Label = label;
        AmountText = $"{amount:N0} zł";
    }

    public string Label { get; }
    public string AmountText { get; }
}

public sealed class InventoryLoanLineViewModel
{
    public InventoryLoanLineViewModel(
        string title,
        decimal remainingAmount,
        decimal annualPayment,
        int yearsRemaining)
    {
        Title = title;
        DetailsText =
            $"{remainingAmount:N0} zł remaining  •  " +
            $"{annualPayment:N0} zł/year  •  " +
            $"{yearsRemaining} " +
            $"{(yearsRemaining == 1 ? "year" : "years")} remaining";
    }

    public string Title { get; }
    public string DetailsText { get; }
}

public sealed class HouseHeirOptionViewModel
{
    public HouseHeirOptionViewModel(
        Guid? personId,
        string label)
    {
        PersonId = personId;
        Label = label;
    }

    public Guid? PersonId { get; }
    public string Label { get; }
}

public sealed class InventoryHouseViewModel :
    ViewModelBase
{
    private readonly Func<Guid, Guid?, bool> _assign;
    private HouseHeirOptionViewModel _selectedHeir;

    public InventoryHouseViewModel(
        HousePropertyInfo house,
        IReadOnlyList<HouseHeirOptionViewModel> heirOptions,
        Func<Guid, Guid?, bool> assign)
    {
        PropertyId = house.Id;
        TownText = house.Town.DisplayName;
        StatusText = house.IsResidence
            ? "Residence"
            : "Rented property";
        HeirOptions = heirOptions;
        _assign = assign;

        _selectedHeir =
            heirOptions.FirstOrDefault(option =>
                option.PersonId == house.AssignedHeirId)
            ?? heirOptions[0];
    }

    public Guid PropertyId { get; }
    public string TownText { get; }
    public string StatusText { get; }
    public IReadOnlyList<HouseHeirOptionViewModel> HeirOptions { get; }

    public HouseHeirOptionViewModel SelectedHeir
    {
        get => _selectedHeir;
        set
        {
            if (value is null
                || ReferenceEquals(_selectedHeir, value))
            {
                return;
            }

            var previous = _selectedHeir;
            _selectedHeir = value;

            if (!_assign(
                    PropertyId,
                    value.PersonId))
            {
                _selectedHeir = previous;
            }

            OnPropertyChanged();
        }
    }
}
