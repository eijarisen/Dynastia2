namespace Dynastia.StandardUI.Map.Projection;

public readonly record struct ProjectedPoint(
    double X,
    double Y);

/// <summary>
/// WGS84 longitude/latitude to ETRF2000-PL / CS92 (EPSG:2180).
/// Kept deliberately isolated from the map renderer so geographic data
/// remains authoritative and presentation can change independently.
/// </summary>
public sealed class PolandCs92Projection
{
    private const double SemiMajorAxis = 6378137.0;
    private const double InverseFlattening = 298.257222101;
    private const double CentralMeridianDegrees = 19.0;
    private const double ScaleFactor = 0.9993;
    private const double FalseEasting = 500000.0;
    private const double FalseNorthing = -5300000.0;

    private static readonly double Flattening =
        1.0 / InverseFlattening;

    private static readonly double EccentricitySquared =
        Flattening * (2.0 - Flattening);

    private static readonly double SecondEccentricitySquared =
        EccentricitySquared / (1.0 - EccentricitySquared);

    private static readonly double CentralMeridian =
        DegreesToRadians(CentralMeridianDegrees);

    public ProjectedPoint Project(
        double longitude,
        double latitude)
    {
        if (!double.IsFinite(longitude)
            || !double.IsFinite(latitude))
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                "Longitude and latitude must be finite numbers.");
        }

        var phi =
            DegreesToRadians(latitude);

        var lambda =
            DegreesToRadians(longitude);

        var sinPhi =
            Math.Sin(phi);

        var cosPhi =
            Math.Cos(phi);

        var tanPhi =
            Math.Tan(phi);

        var eccentricityFourth =
            EccentricitySquared
            * EccentricitySquared;

        var eccentricitySixth =
            eccentricityFourth
            * EccentricitySquared;

        var radius =
            SemiMajorAxis
            / Math.Sqrt(
                1.0
                - EccentricitySquared
                * sinPhi
                * sinPhi);

        var t =
            tanPhi * tanPhi;

        var c =
            SecondEccentricitySquared
            * cosPhi
            * cosPhi;

        var a =
            cosPhi
            * (lambda - CentralMeridian);

        var meridionalArc =
            SemiMajorAxis
            * (
                (
                    1.0
                    - EccentricitySquared / 4.0
                    - 3.0 * eccentricityFourth / 64.0
                    - 5.0 * eccentricitySixth / 256.0
                )
                * phi
                - (
                    3.0 * EccentricitySquared / 8.0
                    + 3.0 * eccentricityFourth / 32.0
                    + 45.0 * eccentricitySixth / 1024.0
                )
                * Math.Sin(2.0 * phi)
                + (
                    15.0 * eccentricityFourth / 256.0
                    + 45.0 * eccentricitySixth / 1024.0
                )
                * Math.Sin(4.0 * phi)
                - 35.0 * eccentricitySixth / 3072.0
                * Math.Sin(6.0 * phi)
            );

        var a2 = a * a;
        var a3 = a2 * a;
        var a4 = a2 * a2;
        var a5 = a4 * a;
        var a6 = a3 * a3;

        var x =
            FalseEasting
            + ScaleFactor
            * radius
            * (
                a
                + (1.0 - t + c) * a3 / 6.0
                + (
                    5.0
                    - 18.0 * t
                    + t * t
                    + 72.0 * c
                    - 58.0 * SecondEccentricitySquared
                )
                * a5 / 120.0
            );

        var y =
            FalseNorthing
            + ScaleFactor
            * (
                meridionalArc
                + radius
                * tanPhi
                * (
                    a2 / 2.0
                    + (
                        5.0
                        - t
                        + 9.0 * c
                        + 4.0 * c * c
                    )
                    * a4 / 24.0
                    + (
                        61.0
                        - 58.0 * t
                        + t * t
                        + 600.0 * c
                        - 330.0 * SecondEccentricitySquared
                    )
                    * a6 / 720.0
                )
            );

        return new ProjectedPoint(
            x,
            y);
    }

    private static double DegreesToRadians(
        double degrees) =>
            degrees
            * Math.PI
            / 180.0;
}
