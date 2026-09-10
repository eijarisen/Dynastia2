using Dynastia.Contracts;

namespace Dynastia.Mechanics.Adoption;

public sealed class StandardAdoptionService :
    IAdoptionService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;

    public StandardAdoptionService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
    }

    public AdoptionPlacementInfo GetPlacement(
        IPerson person)
    {
        var component =
            person.Components.Get<
                AdoptionPlacementComponent>();

        if (component is null)
        {
            return InferPlacement(
                person);
        }

        return new AdoptionPlacementInfo(
            component.Kind,
            component.GuardianId,
            component.HouseholdHeadId,
            HasOrphanTrait(
                person),
            component.OrphanedYear,
            BuildDescription(
                person,
                component));
    }

    public IReadOnlyList<IPerson> GetHostedChildren(
        IPerson householdHead)
    {
        return _economy
            .GetHostedDependentIds(
                householdHead)
            .Select(
                personId =>
                    FindPerson(
                        personId))
            .Where(
                person =>
                    person is not null
                    && person.Tags.Has(
                        "state.alive")
                    && !person.Tags.Has(
                        "role.nanny")
                    && person.Age < 18)
            .Cast<IPerson>()
            .ToList();
    }

    public bool HasOrphanTrait(
        IPerson person)
    {
        return person.Tags.Has(
            "trait.orphan");
    }

    internal AdoptionPlacementComponent
        GetOrCreateComponent(
            IPerson person)
    {
        var component =
            person.Components.Get<
                AdoptionPlacementComponent>();

        if (component is not null)
            return component;

        component =
            new AdoptionPlacementComponent
            {
                Kind =
                    person.Age >= 18
                        ? AdoptionPlacementKind.Independent
                        : AdoptionPlacementKind.BiologicalHousehold
            };

        person.Components.Set(
            component);

        return component;
    }

    internal IPerson? FindPerson(
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

    private AdoptionPlacementInfo InferPlacement(
        IPerson person)
    {
        var father =
            _family.GetFather(
                person);

        var mother =
            _family.GetMother(
                person);

        if (person.Age >= 18)
        {
            return new AdoptionPlacementInfo(
                AdoptionPlacementKind.Independent,
                null,
                person.Id,
                HasOrphanTrait(person),
                null,
                "Independent household");
        }

        if (father is not null
            && father.Tags.Has(
                "state.alive"))
        {
            return new AdoptionPlacementInfo(
                AdoptionPlacementKind.BiologicalHousehold,
                father.Id,
                father.Id,
                HasOrphanTrait(person),
                null,
                $"Lives with father " +
                $"{_family.GetDisplayName(father)}");
        }

        if (father is not null
            && _family.IsMaleLineage(
                father)
            && mother is not null
            && mother.Tags.Has(
                "state.alive"))
        {
            return new AdoptionPlacementInfo(
                AdoptionPlacementKind.Mother,
                mother.Id,
                father.Id,
                HasOrphanTrait(person),
                null,
                $"Lives with mother " +
                $"{_family.GetDisplayName(mother)}");
        }

        return new AdoptionPlacementInfo(
            AdoptionPlacementKind.Orphanage,
            null,
            null,
            HasOrphanTrait(person),
            null,
            "Orphanage");
    }

    private string BuildDescription(
        IPerson person,
        AdoptionPlacementComponent component)
    {
        var prefix =
            HasOrphanTrait(person)
                ? "Orphan · "
                : string.Empty;

        return component.Kind switch
        {
            AdoptionPlacementKind.Mother =>
                $"{prefix}Lives with mother " +
                $"{NameOrUnknown(component.GuardianId)}",

            AdoptionPlacementKind.AdoptiveHousehold =>
                $"{prefix}Lives with the " +
                $"{NameOrUnknown(component.HouseholdHeadId)} household",

            AdoptionPlacementKind.Orphanage =>
                $"{prefix}Orphanage",

            AdoptionPlacementKind.Independent =>
                $"{prefix}Independent household",

            _ =>
                $"{prefix}Biological household"
        };
    }

    private string NameOrUnknown(
        Guid? id)
    {
        var person =
            FindPerson(id);

        return person is null
            ? "unknown"
            : _family.GetDisplayName(
                person);
    }
}
