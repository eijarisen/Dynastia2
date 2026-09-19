using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed partial class StandardLocationService
{
    public void SetPersonHomeTown(
        IPerson person,
        TownInfo homeTown)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(homeTown);

        var placeId =
            RequirePlaceId(homeTown);

        var component =
            person.Components.Get<
                LocationComponent>()
            ?? new LocationComponent();

        component.BirthplaceId ??=
            placeId;

        component.HomeTownId =
            placeId;

        person.Components.Set(
            component);
    }

    private void SetLocation(
        IPerson person,
        TownInfo birthplace,
        TownInfo homeTown)
    {
        var component =
            person.Components.Get<
                LocationComponent>()
            ?? new LocationComponent();

        component.BirthplaceId =
            RequirePlaceId(
                birthplace);

        component.HomeTownId =
            RequirePlaceId(
                homeTown);

        person.Components.Set(
            component);
    }

    private string RequirePlaceId(
        TownInfo town)
    {
        if (string.IsNullOrWhiteSpace(town.Id)
            || FindTown(town.Id) is null)
        {
            throw new InvalidOperationException(
                $"Town '{town.DisplayName}' does not have a valid permanent PlaceId.");
        }

        return town.Id;
    }

    private IPerson? FindPerson(
        Guid? id)
    {
        if (id is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == id.Value);
    }

    private IPerson? FindRelatedPerson(
        GameEvent gameEvent,
        int index)
    {
        if (index < 0
            || index
                >= gameEvent
                    .RelatedPersonIds
                    .Count)
        {
            return null;
        }

        return FindPerson(
            gameEvent
                .RelatedPersonIds[
                    index]);
    }

    private static bool IsSameTown(
        TownInfo first,
        TownInfo second)
    {
        if (!string.IsNullOrWhiteSpace(first.Id)
            && !string.IsNullOrWhiteSpace(second.Id))
        {
            return first.Id.Equals(
                second.Id,
                StringComparison.OrdinalIgnoreCase);
        }

        return Math.Abs(
                   first.Longitude
                   - second.Longitude)
                < 0.000001
            && Math.Abs(
                   first.Latitude
                   - second.Latitude)
                < 0.000001
            && first.Town.Equals(
                second.Town,
                StringComparison.OrdinalIgnoreCase);
    }

    private static double DistanceKm(
        TownInfo first,
        TownInfo second)
    {
        var firstLatitude =
            DegreesToRadians(
                first.Latitude);

        var secondLatitude =
            DegreesToRadians(
                second.Latitude);

        var latitudeDelta =
            DegreesToRadians(
                second.Latitude
                - first.Latitude);

        var longitudeDelta =
            DegreesToRadians(
                second.Longitude
                - first.Longitude);

        var sinLatitude =
            Math.Sin(
                latitudeDelta / 2);

        var sinLongitude =
            Math.Sin(
                longitudeDelta / 2);

        var a =
            sinLatitude
                * sinLatitude
            + Math.Cos(
                firstLatitude)
            * Math.Cos(
                secondLatitude)
            * sinLongitude
            * sinLongitude;

        var clampedA =
            Math.Clamp(
                a,
                0,
                1);

        var c =
            2
            * Math.Atan2(
                Math.Sqrt(
                    clampedA),
                Math.Sqrt(
                    1 - clampedA));

        return EarthRadiusKm
            * c;
    }

    private static double DegreesToRadians(
        double degrees)
    {
        return degrees
            * Math.PI
            / 180.0;
    }

    private sealed record WeightedTown(
        TownInfo Town,
        double DistanceKm);
}
