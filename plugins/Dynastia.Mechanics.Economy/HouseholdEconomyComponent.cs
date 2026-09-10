namespace Dynastia.Mechanics.Economy;

public sealed class HouseholdEconomyComponent
{
    public decimal Wealth { get; set; }

    // Retained for backward save compatibility. The Houses collection
    // becomes authoritative once it exists.
    public int HousesOwned { get; set; }

    // Retained for backward save compatibility. This is always derived
    // as max(0, HousesOwned - 1).
    public int RentedHouses { get; set; }

    public List<HousePropertyState> Houses { get; } = [];

    public Guid? NannyId { get; set; }

    public decimal LastIncome { get; set; }

    public decimal LastExpenses { get; set; }

    public List<LedgerLineState> LastIncomeBreakdown { get; } = [];

    public List<LedgerLineState> LastExpenseBreakdown { get; } = [];

    public List<Guid> HostedDependentIds { get; } = [];
}
