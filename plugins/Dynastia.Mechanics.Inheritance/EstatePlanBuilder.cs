namespace Dynastia.Mechanics.Inheritance;

/// <summary>Deterministic calculation only: no economy, asset, event, or RNG services.</summary>
internal sealed class EstatePlanBuilder
{
    public EstateSettlementPlan Build(EstateSnapshot input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var snapshot = input.Freeze();
        ValidateSnapshot(snapshot);
        var kind = snapshot.Wealth is null ? EstateSettlementKind.MissingFinance
            : snapshot.IsLivingAnchor ? EstateSettlementKind.LivingAnchorTransfer
            : snapshot.Recipients.Count == 0 ? EstateSettlementKind.LostEstate
            : EstateSettlementKind.Inheritance;

        if (kind is EstateSettlementKind.MissingFinance or EstateSettlementKind.LostEstate)
            return new(snapshot, kind, [], [], [], [], []);

        var cash = BuildCash(snapshot, kind);
        var houses = Assign(snapshot, snapshot.Houses.Select(asset => (asset.Id, asset.AssignedHeirId)));
        var farmland = Assign(snapshot, snapshot.Farmland.Select(asset => (asset.Id, asset.AssignedHeirId)));
        var heirlooms = Assign(snapshot, snapshot.Heirlooms.Select(asset => (asset.Id, asset.AssignedHeirId)));
        var disadvantages = kind == EstateSettlementKind.Inheritance
            ? BuildDisadvantages(snapshot, houses, farmland, heirlooms)
            : [];
        return new(snapshot, kind, cash, houses, farmland, heirlooms, disadvantages);
    }

    private static void ValidateSnapshot(EstateSnapshot snapshot)
    {
        Require(snapshot.HeadId != Guid.Empty && snapshot.SourceId != Guid.Empty,
            "Estate source identity is missing.");
        Require(snapshot.Recipients.Select(person => person.Id).Distinct().Count() == snapshot.Recipients.Count,
            "Estate recipients must be unique.");
        Require(snapshot.Recipients.All(person => person.Id != Guid.Empty), "Estate recipient identity is missing.");
        Require(snapshot.HouseValues.Count == snapshot.Houses.Count, "Estate house valuations are incomplete.");
        Require(snapshot.Houses.All(asset => asset.Town is not null)
            && snapshot.Farmland.All(asset => asset.Town is not null), "Estate asset location is missing.");
        ValidateIds(snapshot.Houses.Select(asset => asset.Id), "house");
        ValidateIds(snapshot.Farmland.Select(asset => asset.Id), "farmland");
        ValidateIds(snapshot.Heirlooms.Select(asset => asset.Id), "heirloom");
        if (snapshot.Wealth is null)
        {
            Require(snapshot.Recipients.Count == 0 && snapshot.Houses.Count == 0
                && snapshot.Farmland.Count == 0 && snapshot.Heirlooms.Count == 0,
                "An estate without finance cannot allocate assets.");
            return;
        }
        if (snapshot.IsLivingAnchor)
            Require(snapshot.Recipients.Count == 1 && snapshot.SourceId == snapshot.AnchorId
                && snapshot.Recipients[0].Id == snapshot.SourceId,
                "A residual transfer must have exactly its living anchor as recipient.");
    }

    private static void ValidateIds(IEnumerable<Guid> ids, string kind)
    {
        var seen = new HashSet<Guid>();
        foreach (var id in ids)
            Require(id != Guid.Empty && seen.Add(id), $"Estate {kind} identity is missing or duplicated.");
    }

    private static IReadOnlyList<EstateCashAllocation> BuildCash(
        EstateSnapshot snapshot, EstateSettlementKind kind)
    {
        var wealth = snapshot.Wealth!.Value;
        if (kind == EstateSettlementKind.LivingAnchorTransfer)
        {
            var anchor = snapshot.Recipients[0];
            // Unlike divided inheritance, a residual balance is passed through unchanged.
            return [new(anchor.Id, anchor.IsPending, wealth)];
        }

        // Preserve the original whole-zł rounding, Int64 range and eldest-first remainder.
        // Overflow is now detected before any estate asset is removed.
        var wholeUnits = decimal.ToInt64(Math.Round(wealth, 0, MidpointRounding.AwayFromZero));
        var sign = Math.Sign(wholeUnits);
        var units = Math.Abs(wholeUnits);
        var baseUnits = units / snapshot.Recipients.Count;
        var remainder = units % snapshot.Recipients.Count;
        return snapshot.Recipients.Select((heir, index) => new EstateCashAllocation(
            heir.Id, heir.IsPending, sign * (baseUnits + (index < remainder ? 1L : 0L)))).ToArray();
    }

    private static IReadOnlyList<EstateAssetAllocation> Assign(
        EstateSnapshot snapshot, IEnumerable<(Guid Id, Guid? AssignedHeirId)> assets)
    {
        var orderedAssets = assets.ToArray();
        var recipients = snapshot.Recipients.ToDictionary(heir => heir.Id);
        var ids = snapshot.IsLivingAnchor
            ? orderedAssets.Select(_ => snapshot.SourceId).ToArray()
            : HouseInheritanceAssignmentRules.ResolveRecipients(
                snapshot.Recipients.Select(heir => heir.Id).ToArray(),
                orderedAssets.Select(asset => asset.AssignedHeirId).ToArray());
        return orderedAssets.Select((asset, index) => new EstateAssetAllocation(
            asset.Id, ids[index], recipients[ids[index]].IsPending)).ToArray();
    }

    private static IReadOnlyList<EstateDisadvantage> BuildDisadvantages(
        EstateSnapshot snapshot,
        IReadOnlyList<EstateAssetAllocation> houses,
        IReadOnlyList<EstateAssetAllocation> farmland,
        IReadOnlyList<EstateAssetAllocation> heirlooms)
    {
        if (!HasExplicitDesignation(snapshot)) return [];
        var received = snapshot.Recipients.ToDictionary(heir => heir.Id, _ => 0m);
        for (var index = 0; index < houses.Count; index++)
            received[houses[index].RecipientId] += Math.Max(0m, snapshot.HouseValues[index]);
        foreach (var parcel in farmland)
            received[parcel.RecipientId] += Math.Max(0m, snapshot.FarmlandValue);
        for (var index = 0; index < heirlooms.Count; index++)
            received[heirlooms[index].RecipientId] += Math.Max(0m, snapshot.Heirlooms[index].AppraisedValue);

        var maximum = received.Values.DefaultIfEmpty(0m).Max();
        if (maximum <= 0m) return [];
        var favored = received.Where(pair => pair.Value == maximum).Select(pair => pair.Key).ToArray();
        var result = new List<EstateDisadvantage>();
        foreach (var heir in snapshot.Recipients)
        {
            var value = received[heir.Id];
            var severity = value == 0m ? "skipped" : value * 2m < maximum ? "heavy" : null;
            if (severity is not null)
                result.Add(new(heir.Id, severity, value, maximum,
                    favored.Where(id => id != heir.Id).ToArray()));
        }
        return result;
    }

    internal static bool HasExplicitDesignation(EstateSnapshot snapshot)
    {
        if (snapshot.Recipients.Count < 2) return false;
        var living = snapshot.Recipients.Select(heir => heir.Id).ToHashSet();
        return snapshot.Houses.Select(asset => asset.AssignedHeirId)
            .Concat(snapshot.Farmland.Select(asset => asset.AssignedHeirId))
            .Concat(snapshot.Heirlooms.Select(asset => asset.AssignedHeirId))
            .Any(id => id is Guid selected && living.Contains(selected));
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
