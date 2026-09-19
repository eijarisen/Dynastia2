using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed partial class StandardLocationService
{
    private TownInfo ChooseSpouseBirthplace(
        TownInfo householdTown)
    {
        var towns =
            GetCurrentTownsRequired();

        var currentHome =
            towns.FirstOrDefault(
                candidate =>
                    candidate.Id.Equals(
                        householdTown.Id,
                        StringComparison.OrdinalIgnoreCase));

        var roll =
            _random.NextDouble();

        if (currentHome is not null
            && roll
                < SpouseSameTownChance)
        {
            return currentHome;
        }

        if (roll
            < SpouseSameTownChance
                + SpouseNearbyChance)
        {
            var nearby =
                towns
                    .Where(
                        candidate =>
                            !IsSameTown(
                                candidate,
                                householdTown))
                    .Select(
                        candidate =>
                            new WeightedTown(
                                candidate,
                                DistanceKm(
                                    householdTown,
                                    candidate)))
                    .Where(
                        candidate =>
                            candidate.DistanceKm
                            <= NearbySpouseRadiusKm)
                    .ToList();

            if (nearby.Count > 0)
            {
                return ChooseNearbyWeighted(
                    nearby);
            }
        }

        // The remaining branch is intentionally small (8%) in normal
        // circumstances, but also acts as a safe fallback when the household
        // town is no longer a selectable destination or no nearby town exists.
        return ChooseRandomTown(
            towns);
    }

    private TownInfo ChooseChildBirthplace(
        TownInfo householdTown)
    {
        if (_random.NextDouble()
            < ChildSameTownChance)
        {
            // Existing households keep their permanent PlaceId even if that
            // settlement later ceases to be a selectable destination.
            return householdTown;
        }

        var largerNearby =
            GetCurrentTownsRequired()
                .Where(
                    candidate =>
                        candidate.Population
                            > householdTown.Population
                        && !IsSameTown(
                            candidate,
                            householdTown))
                .Select(
                    candidate =>
                        new WeightedTown(
                            candidate,
                            DistanceKm(
                                householdTown,
                                candidate)))
                .Where(
                    candidate =>
                        candidate.DistanceKm
                        <= NearbyBirthRadiusKm)
                .ToList();

        if (largerNearby.Count == 0)
            return householdTown;

        return ChooseNearbyWeighted(
            largerNearby);
    }

    private TownInfo ChooseNearbyWeighted(
        IReadOnlyList<WeightedTown> candidates)
    {
        return ChooseWeighted(
            candidates,
            candidate =>
            {
                var populationFactor =
                    Math.Sqrt(
                        Math.Max(
                            1,
                            candidate.Town.Population));

                var proximityFactor =
                    1.0
                    / Math.Pow(
                        1.0
                        + candidate.DistanceKm
                            / 30.0,
                        2.0);

                return populationFactor
                    * proximityFactor;
            })
            .Town;
    }

    private TownInfo ChooseStartingTown()
    {
        var towns =
            GetCurrentTownsRequired();

        var roll =
            _random.NextDouble();

        var settlementClass =
            roll < 0.20
                ? SettlementClass.SmallTown
                : roll < 0.50
                    ? SettlementClass.Town
                    : roll < 0.80
                        ? SettlementClass.City
                        : SettlementClass.MajorCity;

        var candidates =
            towns
                .Where(
                    town =>
                        town.SettlementClass
                        == settlementClass)
                .ToList();

        if (candidates.Count == 0)
        {
            candidates =
                towns.ToList();
        }

        // Soft population weighting avoids both a uniform list roll
        // and domination by the handful of largest cities.
        return ChooseWeighted(
            candidates,
            town =>
                Math.Sqrt(
                    Math.Max(
                        1,
                        town.Population)));
    }

    private TownInfo ChooseRandomTown() =>
        ChooseRandomTown(
            GetCurrentTownsRequired());

    private TownInfo ChooseRandomTown(
        IReadOnlyList<TownInfo> towns)
    {
        return towns[
            _random.NextInt(
                0,
                towns.Count - 1)];
    }

    private TownInfo ChoosePopulationWeighted(
        IReadOnlyList<TownInfo> towns)
    {
        if (towns.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot choose a town from an empty historical destination list.");
        }

        return ChooseWeighted(
            towns,
            town =>
                Math.Max(
                    1,
                    town.Population));
    }

    private T ChooseWeighted<T>(
        IReadOnlyList<T> items,
        Func<T, double> weightSelector)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot choose from an empty weighted list.");
        }

        var weighted =
            items
                .Select(
                    item =>
                        new
                        {
                            Item =
                                item,

                            Weight =
                                Math.Max(
                                    0,
                                    weightSelector(item))
                        })
                .ToList();

        var total =
            weighted.Sum(
                item =>
                    item.Weight);

        if (total <= 0)
        {
            return items[
                _random.NextInt(
                    0,
                    items.Count - 1)];
        }

        var roll =
            _random.NextDouble()
            * total;

        foreach (var item in
            weighted)
        {
            if (roll
                < item.Weight)
            {
                return item.Item;
            }

            roll -=
                item.Weight;
        }

        return weighted[^1].Item;
    }
}
