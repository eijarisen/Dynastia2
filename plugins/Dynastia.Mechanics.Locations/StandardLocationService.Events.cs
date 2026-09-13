using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed partial class StandardLocationService
{
    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (gameEvent.Type.Equals(
            "game.started",
            StringComparison.OrdinalIgnoreCase))
        {
            InitializeStartingFamily(
                gameEvent);

            return;
        }

        if (gameEvent.Type.Equals(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.partnered",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase))
        {
            InitializeGeneratedSpouse(
                gameEvent);

            return;
        }

        if (gameEvent.Type.Equals(
                "life.birth",
                StringComparison.OrdinalIgnoreCase))
        {
            InitializeNewborn(
                gameEvent);

            return;
        }

        if (gameEvent.Type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase))
        {
            RecordDeathTown(
                gameEvent);
        }
    }

    private void InitializeStartingFamily(
        GameEvent gameEvent)
    {
        var founder =
            FindPerson(
                gameEvent.SubjectId);

        if (founder is null)
            return;

        var father =
            _family.GetFather(
                founder);

        var mother =
            _family.GetMother(
                founder);

        var fatherTown =
            ChooseStartingTown();

        if (father is not null)
        {
            SetLocation(
                father,
                fatherTown,
                fatherTown);
        }

        var householdTown =
            fatherTown;

        if (mother is not null)
        {
            var motherBirthplace =
                ChooseSpouseBirthplace(
                    householdTown);

            SetLocation(
                mother,
                motherBirthplace,
                householdTown);
        }

        if (father is not null)
        {
            foreach (var sibling in
                _family.GetChildren(father)
                    .Where(
                        candidate =>
                            candidate.Id != founder.Id
                            && _family.GetMother(candidate)?.Id
                                == mother?.Id))
            {
                var siblingBirthplace =
                    ChooseChildBirthplace(
                        householdTown);

                SetLocation(
                    sibling,
                    siblingBirthplace,
                    householdTown);
            }
        }

        var founderBirthplace =
            ChooseChildBirthplace(
                householdTown);

        SetLocation(
            founder,
            founderBirthplace,
            householdTown);
    }

    private void InitializeGeneratedSpouse(
        GameEvent gameEvent)
    {
        var anchor =
            FindPerson(
                gameEvent.SubjectId);

        var spouse =
            FindRelatedPerson(
                gameEvent,
                0);

        if (anchor is null
            || spouse is null)
        {
            return;
        }

        var anchorLocation =
            GetLocation(
                anchor);

        var existing =
            spouse.Components.Get<
                LocationComponent>();

        if (existing?.Birthplace is null)
        {
            existing ??=
                new LocationComponent();

            existing.Birthplace =
                ChooseSpouseBirthplace(
                    anchorLocation.HomeTown);

            spouse.Components.Set(
                existing);
        }

        existing.HomeTown =
            anchorLocation.HomeTown;
    }

    private void InitializeNewborn(
        GameEvent gameEvent)
    {
        var child =
            FindPerson(
                gameEvent.SubjectId);

        var father =
            FindRelatedPerson(
                gameEvent,
                0);

        var mother =
            FindRelatedPerson(
                gameEvent,
                1);

        if (child is null)
            return;

        TownInfo householdTown;

        if (father is not null)
        {
            householdTown =
                GetLocation(
                    father)
                .HomeTown;
        }
        else if (mother is not null)
        {
            householdTown =
                GetLocation(
                    mother)
                .HomeTown;
        }
        else
        {
            householdTown =
                ChoosePopulationWeighted(
                    _towns);
        }

        var birthplace =
            ChooseChildBirthplace(
                householdTown);

        SetLocation(
            child,
            birthplace,
            householdTown);
    }

    private void RecordDeathTown(
        GameEvent gameEvent)
    {
        var person =
            FindPerson(
                gameEvent.SubjectId);

        if (person is null)
            return;

        var location =
            person.Components.Get<
                LocationComponent>();

        if (location?.HomeTown is null)
        {
            EnsureFallbackLocation(
                person);

            location =
                person.Components.Get<
                    LocationComponent>();
        }

        if (location?.HomeTown is null)
            return;

        location.DeathTown =
            location.HomeTown;

        person.Components.Set(
            location);
    }

    private void EnsureFallbackLocation(
        IPerson person)
    {
        var existing =
            person.Components.Get<
                LocationComponent>();

        if (existing?.Birthplace is not null
            && existing.HomeTown is not null)
        {
            return;
        }

        var father =
            _family.GetFather(
                person);

        if (father is not null
            && father.Id != person.Id)
        {
            var parentLocation =
                GetLocation(
                    father);

            SetLocation(
                person,
                ChooseChildBirthplace(
                    parentLocation.HomeTown),
                parentLocation.HomeTown);

            return;
        }

        var spouse =
            _family.GetSpouse(
                person);

        var spouseComponent =
            spouse?.Components.Get<
                LocationComponent>();

        if (spouseComponent?.HomeTown is not null)
        {
            SetLocation(
                person,
                ChooseSpouseBirthplace(
                    spouseComponent.HomeTown),
                spouseComponent.HomeTown);

            return;
        }

        var town =
            ChoosePopulationWeighted(
                _towns);

        SetLocation(
            person,
            town,
            town);
    }

}
