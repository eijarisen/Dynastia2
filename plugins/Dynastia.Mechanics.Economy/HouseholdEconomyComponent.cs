namespace Dynastia.Mechanics.Economy;

public sealed class HouseholdEconomyComponent
{
    public decimal Wealth { get; set; }

    public int HousesOwned { get; set; }

    public int RentedHouses { get; set; }

    public Guid? NannyId { get; set; }

    public decimal LastIncome { get; set; }

    public decimal LastExpenses { get; set; }

    /// <summary>
    /// Minor wards/adopted children whose biological parents are no
    /// longer their active household. This does not alter genealogy.
    /// </summary>
    public List<Guid> HostedDependentIds { get; } = [];
}
