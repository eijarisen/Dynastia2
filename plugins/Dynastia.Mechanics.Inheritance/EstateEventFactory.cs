using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

internal sealed record EstateSettlementEvents(
    IReadOnlyList<GameEvent> RecipientEvents,
    IReadOnlyList<GameEvent> DisadvantageEvents,
    GameEvent? Summary);

/// <summary>Legacy event IDs, data keys, wording, culture and publication order.</summary>
internal static class EstateEventFactory
{
    public static EstateSettlementEvents Create(EstateSettlementPlan plan)
    {
        var snapshot = plan.Snapshot;
        var receipts = new List<GameEvent>();
        var disadvantages = new List<GameEvent>();
        if (plan.Kind == EstateSettlementKind.MissingFinance)
            return new(receipts, disadvantages, null);

        if (plan.Kind == EstateSettlementKind.Inheritance)
        {
            // Preserve the old event stream even though all mutations now precede it:
            // houses by heir, farmland by heir, heirlooms by asset, cash by heir.
            var housesByHeir = Enumerable.Range(0, plan.Houses.Count).ToLookup(
                index => plan.Houses[index].RecipientId, index => snapshot.Houses[index]);
            var farmlandByHeir = Enumerable.Range(0, plan.Farmland.Count).ToLookup(
                index => plan.Farmland[index].RecipientId, index => snapshot.Farmland[index]);
            foreach (var heir in snapshot.Recipients)
            {
                var inherited = housesByHeir[heir.Id].ToArray();
                if (inherited.Length == 0) continue;
                receipts.Add(Event(snapshot, heir.IsPending ? "inheritance.pending_houses" : "inheritance.houses",
                    heir.Id, [snapshot.SourceId], new()
                    {
                        ["count"] = inherited.Length.ToString(),
                        ["towns"] = string.Join(", ", inherited.Select(house => house.Town.Town)),
                        ["text"] = $"{heir.DisplayName} " +
                            $"inherited {inherited.Length} " +
                            $"house{(inherited.Length == 1 ? "" : "s")}."
                    }));
            }
            foreach (var heir in snapshot.Recipients)
            {
                var inherited = farmlandByHeir[heir.Id].ToArray();
                if (inherited.Length == 0) continue;
                receipts.Add(Event(snapshot, "farmland.inherited", heir.Id, [snapshot.SourceId], new()
                {
                    ["count"] = inherited.Length.ToString(),
                    ["towns"] = string.Join(", ", inherited.Select(asset => asset.Town.Town)),
                    ["text"] = $"{heir.DisplayName} inherited " +
                        $"{inherited.Length} parcel" +
                        $"{(inherited.Length == 1 ? "" : "s")} of farmland."
                }));
            }
            var heirsById = snapshot.Recipients.ToDictionary(heir => heir.Id);
            for (var index = 0; index < plan.Heirlooms.Count; index++)
            {
                var allocation = plan.Heirlooms[index];
                var heir = heirsById[allocation.RecipientId];
                var item = snapshot.Heirlooms[index];
                receipts.Add(Event(snapshot, heir.IsPending ? "heirloom.pending" : "heirloom.inherited",
                    heir.Id, [snapshot.SourceId], new()
                    {
                        ["heirloomId"] = item.Id.ToString(),
                        ["item"] = item.DisplayName,
                        ["familyNews"] = "true",
                        ["text"] = !heir.IsPending
                            ? $"{heir.DisplayName} inherited {item.DisplayName} from the family estate."
                            : $"{item.DisplayName} was set aside for {heir.DisplayName} until the inheritance can be received."
                    }));
            }
            foreach (var allocation in plan.Cash)
            {
                var amount = allocation.Amount;
                if (amount == 0m) continue;
                var heir = heirsById[allocation.RecipientId];
                var type = !heir.IsPending
                    ? amount > 0 ? "inheritance.received" : "inheritance.debt_received"
                    : amount > 0 ? "inheritance.pending" : "inheritance.debt_pending";
                var text = !heir.IsPending
                    ? amount > 0
                        ? $"{heir.DisplayName} received an inheritance of {amount:N0} zł."
                        : $"{heir.DisplayName} inherited {Math.Abs(amount):N0} zł of household debt."
                    : amount > 0
                        ? $"{heir.DisplayName} has an inheritance of {amount:N0} zł waiting until they establish a household."
                        : $"{heir.DisplayName} has {Math.Abs(amount):N0} zł of inherited debt waiting until they establish a household.";
                receipts.Add(Event(snapshot, type, heir.Id, [snapshot.SourceId], new()
                {
                    ["amount"] = amount.ToString(),
                    ["text"] = text
                }));
            }
            foreach (var item in plan.Disadvantages)
            {
                var heir = heirsById[item.RecipientId];
                disadvantages.Add(Event(snapshot, "inheritance.disadvantaged", heir.Id,
                    [snapshot.SourceId, .. item.FavoredHeirIds], new()
                    {
                        ["sourceId"] = snapshot.SourceId.ToString(),
                        ["severity"] = item.Severity,
                        ["receivedAssetValue"] = item.ReceivedValue.ToString(CultureInfo.InvariantCulture),
                        ["favoredAssetValue"] = item.FavoredValue.ToString(CultureInfo.InvariantCulture),
                        ["favoredHeirIds"] = string.Join(";", item.FavoredHeirIds),
                        ["suppressChronicle"] = "true",
                        ["text"] = item.Severity == "skipped"
                            ? $"{heir.DisplayName} was passed over for designated property in {snapshot.SourceName}'s estate."
                            : $"{heir.DisplayName} received substantially less designated property than favored heirs in {snapshot.SourceName}'s estate."
                    }));
            }
        }

        return new(receipts, disadvantages, Summary(plan));
    }

    private static GameEvent Summary(EstateSettlementPlan plan)
    {
        var snapshot = plan.Snapshot;
        var wealth = snapshot.Wealth!.Value;
        if (plan.Kind == EstateSettlementKind.LivingAnchorTransfer)
            return Event(snapshot, "household.assets_followed_anchor", snapshot.SourceId, [snapshot.HeadId], new()
            {
                ["amount"] = wealth.ToString(),
                ["houses"] = snapshot.Houses.Count.ToString(),
                ["farmland"] = snapshot.Farmland.Count.ToString(),
                ["heirlooms"] = snapshot.Heirlooms.Count.ToString(),
                ["text"] = $"The remaining assets of " +
                    $"{snapshot.SourceName}'s former household " +
                    "followed the living bloodline member when that household ended."
            });
        if (plan.Kind == EstateSettlementKind.LostEstate)
            return Event(snapshot, "inheritance.estate_left_dynasty", snapshot.SourceId, [snapshot.HeadId], new()
            {
                ["amount"] = wealth.ToString(),
                ["houses"] = snapshot.Houses.Count.ToString(),
                ["farmland"] = snapshot.Farmland.Count.ToString(),
                ["heirlooms"] = snapshot.Heirlooms.Count.ToString(),
                ["text"] = $"The remaining estate of " +
                    $"{snapshot.SourceName} " +
                    "was lost because no eligible living relatives remained to inherit it."
            });
        return Event(snapshot, "inheritance.estate_settled", snapshot.SourceId,
            snapshot.Recipients.Select(heir => heir.Id).ToArray(), new()
            {
                ["estate"] = wealth.ToString(),
                ["houses"] = snapshot.Houses.Count.ToString(),
                ["farmland"] = snapshot.Farmland.Count.ToString(),
                ["heirlooms"] = snapshot.Heirlooms.Count.ToString(),
                ["heirs"] = snapshot.Recipients.Count.ToString(),
                ["text"] = $"The remaining household estate of " +
                    $"{snapshot.SourceName} " +
                    $"was divided among {snapshot.HeirDescription}."
            });
    }

    private static GameEvent Event(EstateSnapshot snapshot, string type, Guid subjectId,
        IReadOnlyList<Guid> relatedIds, Dictionary<string, string> data) => new()
    {
        Type = type,
        Year = snapshot.Year,
        SubjectId = subjectId,
        RelatedPersonIds = relatedIds,
        Data = data
    };
}
