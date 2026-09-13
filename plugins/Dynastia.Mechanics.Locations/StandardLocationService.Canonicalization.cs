using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed partial class StandardLocationService
{
    private bool CanonicalizeComponent(
        LocationComponent component)
    {
        var changed = false;

        if (component.Birthplace is not null)
        {
            var canonical =
                CanonicalizeTown(
                    component.Birthplace);

            if (!ReferenceEquals(
                    canonical,
                    component.Birthplace))
            {
                component.Birthplace =
                    canonical;

                changed = true;
            }
        }

        if (component.HomeTown is not null)
        {
            var canonical =
                CanonicalizeTown(
                    component.HomeTown);

            if (!ReferenceEquals(
                    canonical,
                    component.HomeTown))
            {
                component.HomeTown =
                    canonical;

                changed = true;
            }
        }

        if (component.DeathTown is not null)
        {
            var canonical =
                CanonicalizeTown(
                    component.DeathTown);

            if (!ReferenceEquals(
                    canonical,
                    component.DeathTown))
            {
                component.DeathTown =
                    canonical;

                changed = true;
            }
        }

        return changed;
    }

    private TownInfo CanonicalizeTown(
        TownInfo town)
    {
        if (!string.IsNullOrWhiteSpace(
                town.Id)
            && _townsById.TryGetValue(
                town.Id,
                out var byId))
        {
            return byId;
        }

        var key =
            LegacyTownKey(
                town.Town,
                town.County);

        return _townsByLegacyKey.TryGetValue(
            key,
            out var legacyMatch)
                ? legacyMatch
                : town;
    }

    private static string LegacyTownKey(
        string town,
        string county)
    {
        return $"{town.Trim()}|{county.Trim()}";
    }

    public void SetPersonHomeTown(
        IPerson person,
        TownInfo homeTown)
    {
        var component =
            person.Components.Get<
                LocationComponent>()
            ?? new LocationComponent();

        component.Birthplace ??=
            homeTown;

        component.HomeTown =
            homeTown;

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

        component.Birthplace =
            birthplace;

        component.HomeTown =
            homeTown;

        person.Components.Set(
            component);
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
