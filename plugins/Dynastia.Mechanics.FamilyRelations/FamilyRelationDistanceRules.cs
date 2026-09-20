namespace Dynastia.Mechanics.FamilyRelations;

public static class FamilyRelationDistanceRules
{
    public const double MinimumFamiliarity = 20.0;

    public static (double FamiliarityLoss, double SympathyDrift) GetAnnualDrift(
        double distanceKm,
        bool sameHousehold)
    {
        if (sameHousehold)
            return (0.0, 0.25);

        distanceKm = Math.Max(0, distanceKm);

        return distanceKm switch
        {
            <= 25 => (0.0, 0.35),
            <= 100 => (0.15, 0.50),
            <= 300 => (0.35, 0.75),
            _ => (0.60, 1.00)
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
