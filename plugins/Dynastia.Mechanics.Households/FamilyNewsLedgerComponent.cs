using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

[PersistedComponentId("households.news_ledger")]
public sealed class FamilyNewsLedgerComponent
{
    public Dictionary<int, bool> VisibilityByEventIndex { get; } =
        [];
}
