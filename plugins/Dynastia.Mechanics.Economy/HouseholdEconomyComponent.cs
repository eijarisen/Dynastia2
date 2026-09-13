namespace Dynastia.Mechanics.Economy;

public sealed class HouseholdEconomyComponent
{
    public Guid HouseholdId { get; set; }

    public Guid HeadId { get; set; }

    public Guid DynastyAnchorId { get; set; }

    public int? DynastyGeneration { get; set; }

    public bool EstateReady { get; set; }

    // New households set this immediately. Legacy saves deserialize it as
    // false, allowing the Household service to reconstruct membership once
    // without repeatedly re-importing adult children on every reconciliation.
    public bool LegacyMembershipSeeded { get; set; }

    public decimal Wealth { get; set; }

    public string ResidenceTownId { get; set; } = string.Empty;

    // Retained for backward save compatibility. The Houses collection
    // becomes authoritative once it exists.
    public int HousesOwned { get; set; }

    // Retained for backward save compatibility. This is derived from the
    // number of properties that are not the current-town residence.
    public int RentedHouses { get; set; }

    public List<HousePropertyState> Houses { get; } = [];

    public Guid? NannyId { get; set; }

    public decimal LastIncome { get; set; }

    public decimal LastExpenses { get; set; }

    public List<LedgerLineState> LastIncomeBreakdown { get; } = [];

    public List<LedgerLineState> LastExpenseBreakdown { get; } = [];

    // Kept for compatibility with Adoption. Hosted wards are also included
    // in MemberIds while they actually live in this household.
    public List<Guid> HostedDependentIds { get; } = [];

    // Stable household membership. The component itself may move from one
    // head person to another while this ID and all assets remain unchanged.
    public List<Guid> MemberIds { get; } = [];
}
