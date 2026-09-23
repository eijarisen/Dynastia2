using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

internal enum EstateSettlementKind
{
    MissingFinance,
    Inheritance,
    LivingAnchorTransfer,
    LostEstate
}

internal sealed record EstateRecipient(
    Guid Id, string DisplayName, bool IsPending, Guid? HouseholdId);

/// <summary>Read-only inputs. Build defensively copies all collections before using them.</summary>
internal sealed record EstateSnapshot(
    int Year,
    Guid HeadId,
    Guid? HouseholdId,
    Guid? AnchorId,
    Guid SourceId,
    string SourceName,
    bool IsLivingAnchor,
    decimal? Wealth,
    string HeirDescription,
    IReadOnlyList<EstateRecipient> Recipients,
    IReadOnlyList<HousePropertyInfo> Houses,
    IReadOnlyList<FarmlandAssetInfo> Farmland,
    IReadOnlyList<HeirloomAssetInfo> Heirlooms,
    IReadOnlyList<decimal> HouseValues,
    decimal FarmlandValue)
{
    public EstateSnapshot Freeze() => this with
    {
        Recipients = Array.AsReadOnly(Recipients.ToArray()),
        Houses = Array.AsReadOnly(Houses.ToArray()),
        Farmland = Array.AsReadOnly(Farmland.ToArray()),
        Heirlooms = Array.AsReadOnly(Heirlooms.Select(item => item with
        {
            OwnershipHistory = Array.AsReadOnly(item.OwnershipHistory.ToArray())
        }).ToArray()),
        HouseValues = Array.AsReadOnly(HouseValues.ToArray())
    };
}

internal sealed record EstateCashAllocation(Guid RecipientId, bool IsPending, decimal Amount);
internal sealed record EstateAssetAllocation(Guid AssetId, Guid RecipientId, bool IsPending);
internal sealed record EstateDisadvantage(
    Guid RecipientId, string Severity, decimal ReceivedValue, decimal FavoredValue,
    IReadOnlyList<Guid> FavoredHeirIds);

/// <summary>
/// Ephemeral, immutable instructions. No people, mutable components, or service references
/// are retained, and none of these types participate in save serialization.
/// </summary>
internal sealed class EstateSettlementPlan
{
    internal EstateSettlementPlan(
        EstateSnapshot snapshot,
        EstateSettlementKind kind,
        IEnumerable<EstateCashAllocation> cash,
        IEnumerable<EstateAssetAllocation> houses,
        IEnumerable<EstateAssetAllocation> farmland,
        IEnumerable<EstateAssetAllocation> heirlooms,
        IEnumerable<EstateDisadvantage> disadvantages)
    {
        Snapshot = snapshot.Freeze();
        Kind = kind;
        Cash = Array.AsReadOnly(cash.ToArray());
        Houses = Array.AsReadOnly(houses.ToArray());
        Farmland = Array.AsReadOnly(farmland.ToArray());
        Heirlooms = Array.AsReadOnly(heirlooms.ToArray());
        Disadvantages = Array.AsReadOnly(disadvantages.Select(item => item with
        {
            FavoredHeirIds = Array.AsReadOnly(item.FavoredHeirIds.ToArray())
        }).ToArray());
    }

    public EstateSnapshot Snapshot { get; }
    public EstateSettlementKind Kind { get; }
    public IReadOnlyList<EstateCashAllocation> Cash { get; }
    public IReadOnlyList<EstateAssetAllocation> Houses { get; }
    public IReadOnlyList<EstateAssetAllocation> Farmland { get; }
    public IReadOnlyList<EstateAssetAllocation> Heirlooms { get; }
    public IReadOnlyList<EstateDisadvantage> Disadvantages { get; }
}
