using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

/// <summary>
/// Chooses the origin town of a generated partner before marriage.
/// Nearby places dominate, with a secondary pull toward nearby larger cities,
/// then smaller same-region and national pools. The seeker's own town remains
/// possible, but no longer dominates the candidate pool.
/// </summary>
public static class HusbandOriginSelector
{
    private const double NearbyRadiusKm = 65.0;
    private const double NearbyCityRadiusKm = 220.0;
    private const int BigCityPopulation = 20000;
    private const double EarthRadiusKm = 6371.0088;

    public static HusbandOriginDistribution GetDistribution(
        int year) =>
        year switch
        {
            < 1800 => new HusbandOriginDistribution(
                0.35,
                0.38,
                0.15,
                0.09,
                0.03),
            < 1900 => new HusbandOriginDistribution(
                0.30,
                0.38,
                0.17,
                0.11,
                0.04),
            < 2000 => new HusbandOriginDistribution(
                0.24,
                0.38,
                0.20,
                0.12,
                0.06),
            _ => new HusbandOriginDistribution(
                0.18,
                0.38,
                0.24,
                0.12,
                0.08)
        };

    public static TownInfo Choose(
        TownInfo homeTown,
        IReadOnlyList<TownInfo> towns,
        int year,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(homeTown);
        ArgumentNullException.ThrowIfNull(towns);
        ArgumentNullException.ThrowIfNull(random);

        if (towns.Count == 0)
        {
            throw new InvalidOperationException(
                "Generated partner origin requires at least one current destination town.");
        }

        var currentHomeTown =
            towns.FirstOrDefault(town =>
                SameTown(town, homeTown));

        var distribution =
            GetDistribution(year);
        var roll =
            random.NextDouble();

        if (roll < distribution.SameTownChance)
        {
            return currentHomeTown
                ?? ChooseNationalTown(
                    homeTown,
                    towns,
                    random);
        }

        roll -= distribution.SameTownChance;

        if (roll < distribution.NearbyTownChance)
        {
            var nearby =
                GetDistanceCandidates(
                    homeTown,
                    towns,
                    minimumDistanceExclusive: 0,
                    maximumDistanceInclusive: NearbyRadiusKm,
                    requireBigCity: false);

            if (nearby.Length > 0)
            {
                return ChooseWeighted(
                    nearby,
                    candidate =>
                        Math.Sqrt(Math.Max(1, candidate.Town.Population))
                        / Math.Pow(
                            1.0 + candidate.DistanceKm / 22.0,
                            2.0),
                    random).Town;
            }
        }
        else
        {
            roll -= distribution.NearbyTownChance;

            if (roll < distribution.NearbyCityChance)
            {
                var nearbyCities =
                    GetDistanceCandidates(
                        homeTown,
                        towns,
                        minimumDistanceExclusive: 0,
                        maximumDistanceInclusive: NearbyCityRadiusKm,
                        requireBigCity: true);

                if (nearbyCities.Length > 0)
                {
                    return ChooseWeighted(
                        nearbyCities,
                        candidate =>
                            Math.Pow(
                                Math.Max(1, candidate.Town.Population),
                                0.70)
                            / (1.0 + candidate.DistanceKm / 110.0),
                        random).Town;
                }
            }
            else
            {
                roll -= distribution.NearbyCityChance;

                if (roll < distribution.SameRegionChance)
                {
                    var regional =
                        towns
                            .Where(town =>
                                !SameTown(town, homeTown)
                                && SameRegion(town, homeTown))
                            .ToArray();

                    if (regional.Length > 0)
                    {
                        return ChooseWeighted(
                            regional,
                            town =>
                                Math.Sqrt(
                                    Math.Max(1, town.Population)),
                            random);
                    }
                }
                else
                {
                    return ChooseNationalTown(
                        homeTown,
                        towns,
                        random);
                }
            }
        }

        // Sparse areas may not have a town in the requested bucket. Prefer a
        // same-region fallback, then the full current destination pool, rather
        // than silently turning a failed geographic roll into a same-town match.
        var sameRegionFallback =
            towns
                .Where(town =>
                    !SameTown(town, homeTown)
                    && SameRegion(town, homeTown))
                .ToArray();

        if (sameRegionFallback.Length > 0)
        {
            return ChooseWeighted(
                sameRegionFallback,
                town =>
                    Math.Sqrt(
                        Math.Max(1, town.Population)),
                random);
        }

        return ChooseNationalTown(
            homeTown,
            towns,
            random);
    }

    private static CandidateTown[] GetDistanceCandidates(
        TownInfo homeTown,
        IReadOnlyList<TownInfo> towns,
        double minimumDistanceExclusive,
        double maximumDistanceInclusive,
        bool requireBigCity) =>
        towns
            .Where(town =>
                !SameTown(town, homeTown)
                && (!requireBigCity
                    || town.Population >= BigCityPopulation))
            .Select(town => new CandidateTown(
                town,
                DistanceKm(homeTown, town)))
            .Where(candidate =>
                candidate.DistanceKm > minimumDistanceExclusive
                && candidate.DistanceKm <= maximumDistanceInclusive)
            .ToArray();

    private static TownInfo ChooseNationalTown(
        TownInfo homeTown,
        IReadOnlyList<TownInfo> towns,
        IGameRandom random)
    {
        var candidates =
            towns
                .Where(town =>
                    !SameTown(town, homeTown))
                .ToArray();

        if (candidates.Length == 0)
        {
            return towns.FirstOrDefault(town =>
                       SameTown(town, homeTown))
                   ?? towns[0];
        }

        return ChooseWeighted(
            candidates,
            town =>
                Math.Sqrt(
                    Math.Max(1, town.Population)),
            random);
    }

    private static T ChooseWeighted<T>(
        IReadOnlyList<T> items,
        Func<T, double> weightSelector,
        IGameRandom random)
    {
        var weights =
            items
                .Select(item =>
                    Math.Max(
                        0.0,
                        weightSelector(item)))
                .ToArray();

        var total =
            weights.Sum();

        if (total <= 0)
        {
            return items[
                random.NextInt(
                    0,
                    items.Count - 1)];
        }

        var roll =
            random.NextDouble() * total;

        for (var index = 0;
            index < items.Count;
            index++)
        {
            if (roll < weights[index])
                return items[index];

            roll -= weights[index];
        }

        return items[^1];
    }

    private static double DistanceKm(
        TownInfo first,
        TownInfo second)
    {
        var latitude1 =
            DegreesToRadians(first.Latitude);
        var latitude2 =
            DegreesToRadians(second.Latitude);
        var deltaLatitude =
            latitude2 - latitude1;
        var deltaLongitude =
            DegreesToRadians(
                second.Longitude - first.Longitude);

        var a =
            Math.Sin(deltaLatitude / 2.0)
            * Math.Sin(deltaLatitude / 2.0)
            + Math.Cos(latitude1)
            * Math.Cos(latitude2)
            * Math.Sin(deltaLongitude / 2.0)
            * Math.Sin(deltaLongitude / 2.0);

        return 2.0
            * EarthRadiusKm
            * Math.Asin(
                Math.Min(
                    1.0,
                    Math.Sqrt(a)));
    }

    private static double DegreesToRadians(
        double degrees) =>
        degrees * Math.PI / 180.0;

    private static bool SameRegion(
        TownInfo first,
        TownInfo second) =>
        !string.IsNullOrWhiteSpace(first.RegionId)
        && first.RegionId.Equals(
            second.RegionId,
            StringComparison.OrdinalIgnoreCase);

    private static bool SameTown(
        TownInfo first,
        TownInfo second) =>
        (!string.IsNullOrWhiteSpace(first.Id)
         && first.Id.Equals(
             second.Id,
             StringComparison.OrdinalIgnoreCase))
        || (
            Math.Abs(first.Longitude - second.Longitude) < 0.000001
            && Math.Abs(first.Latitude - second.Latitude) < 0.000001
        );

    private sealed record CandidateTown(
        TownInfo Town,
        double DistanceKm);
}

public sealed record HusbandOriginDistribution(
    double SameTownChance,
    double NearbyTownChance,
    double NearbyCityChance,
    double SameRegionChance,
    double NationalChance)
{
    public double Total =>
        SameTownChance
        + NearbyTownChance
        + NearbyCityChance
        + SameRegionChance
        + NationalChance;
}
