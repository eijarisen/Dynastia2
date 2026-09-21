namespace Dynastia.Mechanics.FamilyRelations;

public static class FamilyRelationDistanceRules
{
    public const double MinimumFamiliarity = 20.0;

    public static (double FamiliarityLoss, double SympathyDrift) GetAnnualDrift(
        double distanceKm,
        bool sameHousehold)
    {
        if (sameHousehold)
            return (0.0, 0.0);

        distanceKm = Math.Max(0, distanceKm);

        return distanceKm switch
        {
            <= 1 => (0.0, 0.0),
            <= 25 => (0.10, 0.15),
            <= 100 => (0.20, 0.35),
            <= 300 => (0.40, 0.60),
            _ => (0.65, 0.85)
        };
    }

    public static double DistanceKm(
        double firstLatitude,
        double firstLongitude,
        double secondLatitude,
        double secondLongitude)
    {
        const double earthRadiusKm = 6371.0;
        static double Radians(double degrees) => degrees * Math.PI / 180.0;

        var firstLat = Radians(firstLatitude);
        var secondLat = Radians(secondLatitude);
        var deltaLat = Radians(secondLatitude - firstLatitude);
        var deltaLon = Radians(secondLongitude - firstLongitude);

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
            + Math.Cos(firstLat) * Math.Cos(secondLat)
            * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }
}
