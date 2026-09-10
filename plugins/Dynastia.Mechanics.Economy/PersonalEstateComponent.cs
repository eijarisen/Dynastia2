namespace Dynastia.Mechanics.Economy;

/// <summary>
/// Money/property promised to one specific person but not yet
/// incorporated into a male-lineage household.
/// This exists for every kind of heir, including daughters and minors.
/// </summary>
public sealed class PersonalEstateComponent
{
    public decimal PendingInheritance { get; set; }

    public int PendingHouses { get; set; }
}
