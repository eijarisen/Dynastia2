using System.Collections.ObjectModel;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public enum FamilyInventoryTab
{
    Money = 0,
    Houses = 1,
    Properties = Houses,
    Farmland = 2,
    Heirlooms = 3
}

public sealed class FamilyInventoryWindowViewModel :
    ViewModelBase
{
    private readonly MainWindowViewModel _main;

    private string _householdName = string.Empty;
    private string _budgetText = string.Empty;
    private IReadOnlyList<HouseholdBudgetHistoryPoint> _budgetHistory = Array.Empty<HouseholdBudgetHistoryPoint>();
    private string _budgetHistoryRangeText = string.Empty;
    private string _incomeTotalText = string.Empty;
    private string _expenseTotalText = string.Empty;
    private string _loanEmptyText = string.Empty;
    private string _houseEmptyText = string.Empty;
    private string _farmlandTitle = "Farmland";
    private string _farmlandSummaryText = string.Empty;
    private string _farmlandEmptyText = string.Empty;
    private string _heirloomEmptyText = string.Empty;
    private string _buyFarmlandActionText = "Buy Farmland";
    private string _sellFarmlandActionText = "Sell Farmland";
    private int _selectedTabIndex;
    private string _lifestyleText = "Balanced";
    private string _lifestyleDescription = "Current living costs and household wellbeing are balanced.";
    private bool _canUseLavishLifestyle;
    private bool _canUseBalancedLifestyle;
    private bool _canUseThriftyLifestyle;
    private bool _canBuyHouse;
    private bool _canExtendHouse;
    private bool _canSellHouse;
    private bool _canBuyFarmland;
    private bool _canSellFarmland;
    private bool _canSellHeirloom;

    public FamilyInventoryWindowViewModel(
        MainWindowViewModel main,
        FamilyInventoryTab initialTab = FamilyInventoryTab.Money,
        bool canVisitBank = false)
    {
        _main = main;
        _selectedTabIndex = (int)initialTab;
        CanVisitBank = canVisitBank;
        Refresh();
    }


    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            var normalized = Math.Clamp(value, 0, 3);
            if (_selectedTabIndex == normalized)
                return;

            _selectedTabIndex = normalized;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<InventoryFinanceLineViewModel>
        Incomes { get; } = [];

    public ObservableCollection<InventoryFinanceLineViewModel>
        Expenses { get; } = [];

    public ObservableCollection<InventoryLoanLineViewModel>
        Loans { get; } = [];

    public ObservableCollection<InventoryHouseViewModel>
        Houses { get; } = [];

    public ObservableCollection<InventoryFarmlandViewModel>
        Farmland { get; } = [];

    public ObservableCollection<InventoryHeirloomViewModel>
        Heirlooms { get; } = [];

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

    public IReadOnlyList<HouseholdBudgetHistoryPoint> BudgetHistory
    {
        get => _budgetHistory;
        private set
        {
            if (ReferenceEquals(_budgetHistory, value))
                return;

            _budgetHistory = value;
            OnPropertyChanged();
        }
    }

    public string BudgetHistoryRangeText
    {
        get => _budgetHistoryRangeText;
        private set
        {
            if (_budgetHistoryRangeText == value)
                return;

            _budgetHistoryRangeText = value;
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

    public string FarmlandTitle
    {
        get => _farmlandTitle;
        private set
        {
            if (_farmlandTitle == value)
                return;
            _farmlandTitle = value;
            OnPropertyChanged();
        }
    }

    public string FarmlandSummaryText
    {
        get => _farmlandSummaryText;
        private set
        {
            if (_farmlandSummaryText == value)
                return;
            _farmlandSummaryText = value;
            OnPropertyChanged();
        }
    }

    public string FarmlandEmptyText
    {
        get => _farmlandEmptyText;
        private set
        {
            if (_farmlandEmptyText == value)
                return;
            _farmlandEmptyText = value;
            OnPropertyChanged();
        }
    }

    public string HeirloomEmptyText
    {
        get => _heirloomEmptyText;
        private set
        {
            if (_heirloomEmptyText == value)
                return;
            _heirloomEmptyText = value;
            OnPropertyChanged();
        }
    }

    public string BuyFarmlandActionText
    {
        get => _buyFarmlandActionText;
        private set
        {
            if (_buyFarmlandActionText == value)
                return;
            _buyFarmlandActionText = value;
            OnPropertyChanged();
        }
    }

    public string SellFarmlandActionText
    {
        get => _sellFarmlandActionText;
        private set
        {
            if (_sellFarmlandActionText == value)
                return;
            _sellFarmlandActionText = value;
            OnPropertyChanged();
        }
    }

    public bool CanVisitBank { get; }

    public string LifestyleText
    {
        get => _lifestyleText;
        private set
        {
            if (_lifestyleText == value)
                return;
            _lifestyleText = value;
            OnPropertyChanged();
        }
    }

    public string LifestyleDescription
    {
        get => _lifestyleDescription;
        private set
        {
            if (_lifestyleDescription == value)
                return;
            _lifestyleDescription = value;
            OnPropertyChanged();
        }
    }

    public bool CanUseLavishLifestyle
    {
        get => _canUseLavishLifestyle;
        private set
        {
            if (_canUseLavishLifestyle == value)
                return;
            _canUseLavishLifestyle = value;
            OnPropertyChanged();
        }
    }

    public bool CanUseBalancedLifestyle
    {
        get => _canUseBalancedLifestyle;
        private set
        {
            if (_canUseBalancedLifestyle == value)
                return;
            _canUseBalancedLifestyle = value;
            OnPropertyChanged();
        }
    }

    public bool CanUseThriftyLifestyle
    {
        get => _canUseThriftyLifestyle;
        private set
        {
            if (_canUseThriftyLifestyle == value)
                return;
            _canUseThriftyLifestyle = value;
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

    public bool CanExtendHouse
    {
        get => _canExtendHouse;
        private set
        {
            if (_canExtendHouse == value)
                return;

            _canExtendHouse = value;
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

    public bool CanBuyFarmland
    {
        get => _canBuyFarmland;
        private set
        {
            if (_canBuyFarmland == value)
                return;
            _canBuyFarmland = value;
            OnPropertyChanged();
        }
    }

    public bool CanSellFarmland
    {
        get => _canSellFarmland;
        private set
        {
            if (_canSellFarmland == value)
                return;
            _canSellFarmland = value;
            OnPropertyChanged();
        }
    }

    public bool CanSellHeirloom
    {
        get => _canSellHeirloom;
        private set
        {
            if (_canSellHeirloom == value)
                return;
            _canSellHeirloom = value;
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
        Farmland.Clear();
        Heirlooms.Clear();

        if (data is null)
        {
            HouseholdName = "No active household";
            BudgetText = "Budget: —";
            BudgetHistory = Array.Empty<HouseholdBudgetHistoryPoint>();
            BudgetHistoryRangeText = string.Empty;
            IncomeTotalText = "Income: —";
            ExpenseTotalText = "Expenses: —";
            LoanEmptyText = "No loans.";
            HouseEmptyText = "No owned houses.";
            FarmlandTitle = "Farmland";
            FarmlandSummaryText = string.Empty;
            FarmlandEmptyText = "No owned farmland.";
            HeirloomEmptyText = "No family heirlooms.";
            BuyFarmlandActionText = "Buy Farmland";
            SellFarmlandActionText = "Sell Farmland";
            LifestyleText = "Balanced";
            LifestyleDescription = "Current living costs and household wellbeing are balanced.";
            CanUseLavishLifestyle = false;
            CanUseBalancedLifestyle = false;
            CanUseThriftyLifestyle = false;
            CanBuyHouse = false;
            CanSellHouse = false;
            CanBuyFarmland = false;
            CanSellFarmland = false;
            CanSellHeirloom = false;
            return;
        }

        HouseholdName = data.HouseholdName;
        BudgetText = $"Total Budget: {data.Budget:N0} zł";
        BudgetHistory = data.BudgetHistory;
        BudgetHistoryRangeText = data.BudgetHistory.Count switch
        {
            0 => string.Empty,
            1 => data.BudgetHistory[0].Year.ToString(),
            _ => $"{data.BudgetHistory[0].Year}–{data.BudgetHistory[^1].Year}"
        };
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
                    _main.GetHouseValue(house),
                    data.CanExtendHouse
                        && house.ExtensionCost > 0m
                        && data.Budget >= house.ExtensionCost,
                    heirOptions,
                    _main.SetHouseInheritanceHeir));
        }

        if (data.Farming is { } farmlandSnapshot)
        {
            var parcelNumber = 0;
            foreach (var farmland in farmlandSnapshot.Farmland
                         .OrderBy(asset => asset.AcquiredYear)
                         .ThenBy(asset => asset.Id))
            {
                parcelNumber++;
                var status = farmland.Town.Id.Equals(
                        farmlandSnapshot.ResidenceTownId,
                        StringComparison.OrdinalIgnoreCase)
                    ? $"Parcel {parcelNumber} · Current town"
                    : $"Parcel {parcelNumber}";

                Farmland.Add(
                    new InventoryFarmlandViewModel(
                        farmland,
                        status,
                        _main.GetFarmlandSaleValue(farmland),
                        data.CanSellFarmland,
                        data.CanAddLivestock
                            && farmland.Town.Id.Equals(
                                farmlandSnapshot.ResidenceTownId,
                                StringComparison.OrdinalIgnoreCase)
                            && string.IsNullOrWhiteSpace(farmland.LivestockTypeId)
                            && data.Budget >= data.LivestockPurchasePrice,
                        data.LivestockPurchasePrice,
                        heirOptions,
                        _main.SetFarmlandInheritanceHeir));
            }
        }

        foreach (var heirloom in data.Heirlooms
                     .OrderBy(item => item.AcquiredYear)
                     .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(item => item.Id))
        {
            Heirlooms.Add(
                new InventoryHeirloomViewModel(
                    heirloom,
                    heirOptions,
                    _main.SetHeirloomInheritanceHeir,
                    data.CanSellHeirloom));
        }

        HeirloomEmptyText = Heirlooms.Count == 0
            ? "No family heirlooms."
            : string.Empty;

        LoanEmptyText =
            Loans.Count == 0
                ? "No active loans or debts."
                : string.Empty;

        HouseEmptyText =
            Houses.Count == 0
                ? "No owned houses."
                : string.Empty;

        if (data.Farming is { } farming)
        {
            FarmlandTitle = "Farmland";
            FarmlandSummaryText = $"Parcels: {farming.TotalParcelCount}";
            FarmlandEmptyText = farming.TotalParcelCount == 0
                ? "No owned farmland."
                : string.Empty;
        }
        else
        {
            FarmlandTitle = "Farmland";
            FarmlandSummaryText = "Parcels: 0";
            FarmlandEmptyText = "Farming mechanics are unavailable.";
        }

        LifestyleText = data.Lifestyle switch
        {
            HouseholdLifestyleStance.Lavish => "Lavish",
            HouseholdLifestyleStance.Thrifty => "Thrifty",
            _ => "Balanced"
        };

        LifestyleDescription = data.Lifestyle switch
        {
            HouseholdLifestyleStance.Lavish =>
                "+10% living costs. Subtle gains to family happiness, marriage, work morale, recovery, education and spouse prospects; slightly less Stress.",
            HouseholdLifestyleStance.Thrifty =>
                "-10% living costs. Subtle penalties to family happiness, marriage, work morale, recovery, education and spouse prospects; slightly more Stress.",
            _ =>
                "Baseline living costs and household wellbeing."
        };

        CanUseLavishLifestyle = data.CanUseLavishLifestyle;
        CanUseBalancedLifestyle = data.CanUseBalancedLifestyle;
        CanUseThriftyLifestyle = data.CanUseThriftyLifestyle;
        CanBuyHouse = data.CanBuyHouse;
        CanExtendHouse = data.CanExtendHouse;
        CanSellHouse = data.CanSellHouse;
        CanBuyFarmland = data.CanBuyFarmland;
        CanSellFarmland = data.CanSellFarmland;
        CanSellHeirloom = data.CanSellHeirloom;
        BuyFarmlandActionText = "Buy Farmland";
        SellFarmlandActionText = "Sell Farmland";
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

public sealed class InventoryHeirloomViewModel :
    ViewModelBase
{
    private readonly Func<Guid, Guid?, bool> _assign;
    private HouseHeirOptionViewModel _selectedHeir;

    public InventoryHeirloomViewModel(
        HeirloomAssetInfo heirloom,
        IReadOnlyList<HouseHeirOptionViewModel> heirOptions,
        Func<Guid, Guid?, bool> assign,
        bool canSell)
    {
        HeirloomId = heirloom.Id;
        Emoji = heirloom.Emoji;
        DisplayName = heirloom.DisplayName;
        var saleValue = Math.Round(
            heirloom.AppraisedValue * 0.8m,
            0,
            MidpointRounding.AwayFromZero);
        DetailsText = $"{heirloom.AcquiredYear} · Value {heirloom.AppraisedValue:N0} zł · sells for {saleValue:N0} zł";
        AcquiredText = $"Acquired {heirloom.AcquiredYear}";
        ValueText = $"Value {heirloom.AppraisedValue:N0} zł";
        SaleValueText = $"Sale value {saleValue:N0} zł";
        OriginText = string.IsNullOrWhiteSpace(heirloom.OriginDescription)
            ? "Family heirloom"
            : heirloom.OriginDescription;
        HeirOptions = heirOptions;
        _assign = assign;
        CanSell = canSell;
        _selectedHeir = heirOptions.FirstOrDefault(option =>
                option.PersonId == heirloom.AssignedHeirId)
            ?? heirOptions[0];
    }

    public Guid HeirloomId { get; }
    public string Emoji { get; }
    public string DisplayName { get; }
    public string DetailsText { get; }
    public string AcquiredText { get; }
    public string ValueText { get; }
    public string SaleValueText { get; }
    public string OriginText { get; }
    public bool CanSell { get; }
    public IReadOnlyList<HouseHeirOptionViewModel> HeirOptions { get; }

    public HouseHeirOptionViewModel SelectedHeir
    {
        get => _selectedHeir;
        set
        {
            if (value is null || ReferenceEquals(_selectedHeir, value))
                return;

            var previous = _selectedHeir;
            _selectedHeir = value;
            if (!_assign(HeirloomId, value.PersonId))
                _selectedHeir = previous;
            OnPropertyChanged();
        }
    }
}

public sealed class InventoryFarmlandViewModel :
    ViewModelBase
{
    private readonly Func<Guid, Guid?, bool> _assign;
    private HouseHeirOptionViewModel _selectedHeir;

    public InventoryFarmlandViewModel(
        FarmlandAssetInfo farmland,
        string statusText,
        decimal saleValue,
        bool canSell,
        bool canAddLivestock,
        decimal livestockPurchasePrice,
        IReadOnlyList<HouseHeirOptionViewModel> heirOptions,
        Func<Guid, Guid?, bool> assign)
    {
        FarmlandId = farmland.Id;
        var farmName = string.IsNullOrWhiteSpace(farmland.FarmTypeDisplayName)
            ? "Farm"
            : farmland.FarmTypeDisplayName;
        var farmEmoji = string.IsNullOrWhiteSpace(farmland.FarmTypeEmoji)
            ? "🌾"
            : farmland.FarmTypeEmoji;
        Emoji = farmEmoji;
        FarmTypeText = farmName;
        TownText = $"{farmEmoji} {farmName} — {farmland.Town.DisplayName}";

        var livestockText = string.IsNullOrWhiteSpace(farmland.LivestockTypeId)
            ? "No livestock"
            : $"{farmland.LivestockEmoji} {farmland.LivestockDisplayName}";
        LivestockText = livestockText;
        ValueText = $"Value {saleValue:N0} zł";
        StatusText = $"{livestockText} · Sale value {saleValue:N0} zł · {statusText}";
        CanSell = canSell;
        CanAddLivestock = canAddLivestock;
        AddLivestockActionText = $"Add Livestock ({livestockPurchasePrice:N0} zł)";
        HeirOptions = heirOptions;
        _assign = assign;
        _selectedHeir =
            heirOptions.FirstOrDefault(option =>
                option.PersonId == farmland.AssignedHeirId)
            ?? heirOptions[0];
    }

    public Guid FarmlandId { get; }
    public string Emoji { get; }
    public string FarmTypeText { get; }
    public string LivestockText { get; }
    public string ValueText { get; }
    public string TownText { get; }
    public string StatusText { get; }
    public bool CanSell { get; }
    public bool CanAddLivestock { get; }
    public string AddLivestockActionText { get; }
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
                    FarmlandId,
                    value.PersonId))
            {
                _selectedHeir = previous;
            }

            OnPropertyChanged();
        }
    }
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
        decimal currentValue,
        bool canExtend,
        IReadOnlyList<HouseHeirOptionViewModel> heirOptions,
        Func<Guid, Guid?, bool> assign)
    {
        PropertyId = house.Id;
        Emoji = "🏠";
        TownText = house.Town.DisplayName;
        CountyText = house.Town.County;
        CapacityText = $"Capacity {house.ResidentCapacity}";
        ValueText = $"Value {currentValue:N0} zł";
        CanExtend = canExtend;
        ExtendActionText = $"Extend ({house.ExtensionCost:N0} zł)";
        var status = house.IsResidence
            ? "Residence"
            : "Rented property";
        var extensionText = house.CapacityExtensions == 0
            ? string.Empty
            : $" · {house.CapacityExtensions} extension{(house.CapacityExtensions == 1 ? string.Empty : "s")}";
        StatusText =
            $"{status}{extensionText} · Capacity {house.ResidentCapacity} · Value {currentValue:N0} zł";
        HeirOptions = heirOptions;
        _assign = assign;

        _selectedHeir =
            heirOptions.FirstOrDefault(option =>
                option.PersonId == house.AssignedHeirId)
            ?? heirOptions[0];
    }

    public Guid PropertyId { get; }
    public string Emoji { get; }
    public string TownText { get; }
    public string CountyText { get; }
    public string CapacityText { get; }
    public string ValueText { get; }
    public string StatusText { get; }
    public bool CanExtend { get; }
    public string ExtendActionText { get; }
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
