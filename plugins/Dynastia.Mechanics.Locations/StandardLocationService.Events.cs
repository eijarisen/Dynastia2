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

    internal void ReconcileAll()
    {
        var resolving = new HashSet<Guid>();

        foreach (var person in _gameState.People)
            ReconcilePersonLocation(person, resolving);
    }

    private void ReconcilePersonLocation(
        IPerson person,
        HashSet<Guid> resolving)
    {
        if (!resolving.Add(person.Id))
            return;

        try
        {
            var component =
                person.Components.Get<LocationComponent>();

            if (component is not null
                && CanonicalizeComponent(component))
            {
                person.Components.Set(component);
            }

            if (component?.Birthplace is null
                || component.HomeTown is null)
            {
                EnsureFallbackLocation(person, resolving);
                component = person.Components.Get<LocationComponent>();
            }

            if (component?.HomeTown is not null
                && person.Tags.Has("state.dead")
                && component.DeathTown is null)
            {
                component.DeathTown = component.HomeTown;
                person.Components.Set(component);
            }
        }
        finally
        {
            resolving.Remove(person.Id);
        }
    }

    private void EnsureFallbackLocation(
        IPerson person) =>
        EnsureFallbackLocation(person, new HashSet<Guid>());

    private void EnsureFallbackLocation(
        IPerson person,
        HashSet<Guid> resolving)
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
            ReconcilePersonLocation(father, resolving);

            var parentComponent =
                father.Components.Get<LocationComponent>();

            if (parentComponent?.HomeTown is not null)
            {
                SetLocation(
                    person,
                    ChooseChildBirthplace(
                        parentComponent.HomeTown),
                    parentComponent.HomeTown);

                return;
            }
        }

        var spouse =
            _family.GetSpouse(
                person);

        if (spouse is not null
            && spouse.Id != person.Id)
        {
            ReconcilePersonLocation(spouse, resolving);
        }

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
