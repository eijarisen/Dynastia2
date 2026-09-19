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

        var spouseExisting =
            spouse.Components.Get<
                LocationComponent>()
            ?? new LocationComponent();

        if (string.IsNullOrWhiteSpace(
                spouseExisting.BirthplaceId))
        {
            spouseExisting.BirthplaceId =
                ChooseSpouseBirthplace(
                    anchorLocation.HomeTown)
                .Id;
        }

        if (string.IsNullOrWhiteSpace(
                spouseExisting.HomeTownId))
        {
            spouseExisting.HomeTownId =
                spouseExisting.BirthplaceId;
        }

        spouse.Components.Set(
            spouseExisting);

        var anchorSex =
            _family.GetSex(
                anchor);

        var spouseSex =
            _family.GetSex(
                spouse);

        if (anchorSex != spouseSex
            && (anchorSex == Sex.Male
                || spouseSex == Sex.Male))
        {
            var husband =
                anchorSex == Sex.Male
                    ? anchor
                    : spouse;

            var wife =
                anchorSex == Sex.Female
                    ? anchor
                    : spouse;

            var husbandHome =
                GetLocation(
                    husband)
                .HomeTown;

            SetPersonHomeTown(
                wife,
                husbandHome);

            return;
        }

        // Same-sex unions keep the event subject as the residential anchor.
        SetPersonHomeTown(
            spouse,
            anchorLocation.HomeTown);
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
                    GetCurrentTownsRequired());
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

        if (location is null
            || string.IsNullOrWhiteSpace(
                location.HomeTownId))
        {
            EnsureFallbackLocation(
                person);

            location =
                person.Components.Get<
                    LocationComponent>();
        }

        if (location is null
            || string.IsNullOrWhiteSpace(
                location.HomeTownId))
        {
            return;
        }

        location.DeathTownId =
            location.HomeTownId;

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

            var birthplaceValid =
                component is not null
                && !string.IsNullOrWhiteSpace(
                    component.BirthplaceId)
                && FindTownAtYear(
                    component.BirthplaceId,
                    GetBirthYear(person)) is not null;

            var homeTownValid =
                component is not null
                && !string.IsNullOrWhiteSpace(
                    component.HomeTownId)
                && FindTownAtYear(
                    component.HomeTownId,
                    _gameState.Year) is not null;

            if (!birthplaceValid
                || !homeTownValid)
            {
                EnsureFallbackLocation(person, resolving);
                component = person.Components.Get<LocationComponent>();
            }

            if (component is null)
                return;

            if (person.Tags.Has("state.dead")
                && string.IsNullOrWhiteSpace(
                    component.DeathTownId)
                && !string.IsNullOrWhiteSpace(
                    component.HomeTownId))
            {
                component.DeathTownId =
                    component.HomeTownId;

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

        if (existing is not null
            && !string.IsNullOrWhiteSpace(
                existing.BirthplaceId)
            && !string.IsNullOrWhiteSpace(
                existing.HomeTownId)
            && FindTownAtYear(
                existing.BirthplaceId,
                GetBirthYear(person)) is not null
            && FindTownAtYear(
                existing.HomeTownId,
                _gameState.Year) is not null)
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

            if (parentComponent is not null
                && !string.IsNullOrWhiteSpace(
                    parentComponent.HomeTownId)
                && FindTown(
                    parentComponent.HomeTownId)
                    is TownInfo parentHomeTown)
            {
                SetLocation(
                    person,
                    ChooseChildBirthplace(
                        parentHomeTown),
                    parentHomeTown);

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

        if (spouseComponent is not null
            && !string.IsNullOrWhiteSpace(
                spouseComponent.HomeTownId)
            && FindTown(
                spouseComponent.HomeTownId)
                is TownInfo spouseHomeTown)
        {
            SetLocation(
                person,
                ChooseSpouseBirthplace(
                    spouseHomeTown),
                spouseHomeTown);

            return;
        }

        var town =
            ChoosePopulationWeighted(
                GetCurrentTownsRequired());

        SetLocation(
            person,
            town,
            town);
    }
}
