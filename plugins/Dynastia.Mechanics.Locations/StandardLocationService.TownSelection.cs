using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed partial class StandardLocationService
{
    private TownInfo ChooseSpouseBirthplace(
        TownInfo householdTown)
    {
        var roll =
            _random.NextDouble();

        if (roll
            < SpouseSameTownChance)
        {
            return householdTown;
        }

        if (roll
            < SpouseSameTownChance
                + SpouseNearbyChance)
        {
            var nearby =
                _towns
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
        // circumstances, but also acts as a safe fallback when no nearby
        // town exists.
        return ChooseRandomTown();
    }

    private TownInfo ChooseChildBirthplace(
        TownInfo householdTown)
    {
        if (_random.NextDouble()
            < ChildSameTownChance)
        {
            return householdTown;
        }

        var largerNearby =
            _towns
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
            _towns
                .Where(
                    town =>
                        town.SettlementClass
                        == settlementClass)
                .ToList();

        if (candidates.Count == 0)
        {
            candidates =
                _towns.ToList();
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

    private TownInfo ChooseRandomTown()
    {
        return _towns[
            _random.NextInt(
                0,
                _towns.Count - 1)];
    }

    private TownInfo ChoosePopulationWeighted(
        IReadOnlyList<TownInfo> towns)
    {
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

    private static IReadOnlyList<TownInfo>
        ParseTowns(
            string text)
    {
        var result =
            new List<TownInfo>();

        var lines =
            text.Split(
                ['\r', '\n'],
                StringSplitOptions
                    .RemoveEmptyEntries);

        if (lines.Length < 2
            || !lines[0].Equals(
                "TownId,Town,County,Longitude,Latitude,Population,RegionId",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{TownsPath} has an unexpected header.");
        }

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields =
                lines[index]
                    .Split(',');

            if (fields.Length != 7)
            {
                throw new InvalidDataException(
                    $"Invalid towns.csv row "
                    + $"{index + 1}: expected 7 fields.");
            }

            if (!double.TryParse(
                    fields[3],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var longitude)
                || !double.TryParse(
                    fields[4],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var latitude)
                || !int.TryParse(
                    fields[5],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var population))
            {
                throw new InvalidDataException(
                    $"Invalid numeric town data on row "
                    + $"{index + 1}.");
            }

            var townId =
                fields[0].Trim();

            var regionId =
                fields[6].Trim();

            if (townId.Length == 0
                || regionId.Length == 0)
            {
                throw new InvalidDataException(
                    $"Invalid towns.csv row {index + 1}: "
                    + "TownId and RegionId are required.");
            }

            result.Add(
                new TownInfo(
                    fields[1],
                    fields[2],
                    longitude,
                    latitude,
                    population)
                {
                    Id = townId,
                    RegionId = regionId
                });
        }

        if (result
            .Select(town => town.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count()
            != result.Count)
        {
            throw new InvalidDataException(
                $"{TownsPath} contains duplicate TownIds.");
        }

        return result;
    }

}
