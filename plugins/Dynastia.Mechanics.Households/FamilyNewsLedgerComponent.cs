namespace Dynastia.Mechanics.Households;

public sealed class FamilyNewsLedgerComponent
{
    public Dictionary<int, bool> VisibilityByEventIndex { get; } =
        [];
}
