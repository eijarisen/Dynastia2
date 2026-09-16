using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

/// <summary>
/// Chooses the home town a generated husband came from before marriage.
/// The distribution broadens over time while the woman's own town remains
/// the single most likely origin in every era.
/// </summary>
public static class HusbandOriginSelector
{
    private const double NearbyRadiusKm = 65.0;
    private const double RegionalCityRadiusKm = 220.0;
    private const double EarthRadiusKm = 6371.0088;

    public static HusbandOriginDistribution GetDistribution(
        int year) =>
        year switch
        {
            < 1800 => new HusbandOriginDistribution(
                0.75,
                0.17,
                0.06,
                0.02),
            < 1900 => new HusbandOriginDistribution(
                0.70,
                0.17,
                0.09,
                0.04),
            < 2000 => new HusbandOriginDistribution(
                0.62,
                0.19,
                0.12,
                0.07),
            _ => new HusbandOriginDistribution(
                0.55,
                0.20,
                0.15,
                0.10)
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
            return homeTown;

        var distribution =
            GetDistribution(year);
        var roll =
            random.NextDouble();

        if (roll < distribution.SameTownChance)
            return homeTown;

        roll -= distribution.SameTownChance;

        if (roll < distribution.NearbyTownChance)
        {
            var nearby =
                towns
                    .Where(town =>
                        !SameTown(town, homeTown))
                    .Select(town => new CandidateTown(
                        town,
                        DistanceKm(homeTown, town)))
                    .Where(candidate =>
                        candidate.DistanceKm <= NearbyRadiusKm)
                    .ToArray();

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

            if (roll < distribution.RegionalCityChance)
            {
                var regionalCities =
                    towns
                        .Where(town =>
                            town.Population >= 20000
                            && !SameTown(town, homeTown))
                        .Select(town => new CandidateTown(
                            town,
                            DistanceKm(homeTown, town)))
                        .Where(candidate =>
                            candidate.DistanceKm > NearbyRadiusKm
                            && candidate.DistanceKm <= RegionalCityRadiusKm)
                        .ToArray();

                if (regionalCities.Length > 0)
                {
                    return ChooseWeighted(
                        regionalCities,
                        candidate =>
                            Math.Pow(
                                Math.Max(1, candidate.Town.Population),
                                0.65)
                            / (1.0 + candidate.DistanceKm / 140.0),
                        random).Town;
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

        // A sparse area may have no town in the requested geographic bucket.
        // Fall back to a national roll rather than silently converting the
        // result to another same-town match.
        return ChooseNationalTown(
            homeTown,
            towns,
            random);
    }

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
            return homeTown;

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
    double RegionalCityChance,
    double NationalChance)
{
    public double Total =>
        SameTownChance
        + NearbyTownChance
        + RegionalCityChance
        + NationalChance;
}
