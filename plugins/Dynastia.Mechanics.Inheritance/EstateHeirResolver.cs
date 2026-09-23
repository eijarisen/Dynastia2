using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

/// <summary>The existing heir priority and birth-date ordering, independent of settlement.</summary>
internal sealed class EstateHeirResolver(IFamilyService family)
{
    private readonly IFamilyService _family = family;

    public EstateHeirResolution Resolve(
        IGameState gameState,
        IPerson? source)
    {
        if (source is null)
        {
            return new EstateHeirResolution(
                Array.Empty<IPerson>(),
                "no heirs");
        }

        var children =
            SortLiving(
                _family.GetChildren(source));

        if (children.Count > 0)
        {
            return new EstateHeirResolution(
                children,
                "the living children");
        }

        var spouse =
            _family.GetSpouse(source);

        if (spouse is not null
            && spouse.Tags.Has("state.alive"))
        {
            return new EstateHeirResolution(
                [spouse],
                "the surviving spouse");
        }

        var siblings =
            SortLiving(
                GetSiblings(
                    gameState,
                    source));

        if (siblings.Count > 0)
        {
            return new EstateHeirResolution(
                siblings,
                "the living siblings");
        }

        var parents =
            SortLiving(
                new[]
                {
                    _family.GetFather(source),
                    _family.GetMother(source)
                }
                .Where(person => person is not null)
                .Cast<IPerson>());

        if (parents.Count > 0)
        {
            return new EstateHeirResolution(
                parents,
                "the living parents");
        }

        var parentSiblings =
            GetParentSiblings(
                gameState,
                source);

        var cousins =
            SortLiving(
                parentSiblings
                    .SelectMany(relative => _family.GetChildren(relative))
                    .Where(relative => relative.Id != source.Id)
                    .DistinctBy(relative => relative.Id));

        if (cousins.Count > 0)
        {
            return new EstateHeirResolution(
                cousins,
                "the living first cousins");
        }

        var unclesAndAunts =
            SortLiving(
                parentSiblings);

        if (unclesAndAunts.Count > 0)
        {
            return new EstateHeirResolution(
                unclesAndAunts,
                "the living uncles and aunts");
        }

        return new EstateHeirResolution(
            Array.Empty<IPerson>(),
            "no heirs");
    }

    private IReadOnlyList<IPerson> GetSiblings(
        IGameState gameState,
        IPerson person)
    {
        var father =
            _family.GetFather(person);

        var mother =
            _family.GetMother(person);

        if (father is null
            && mother is null)
        {
            return Array.Empty<IPerson>();
        }

        return gameState.People
            .Where(candidate => candidate.Id != person.Id)
            .Where(candidate =>
                father is not null
                    && _family.GetFather(candidate)?.Id == father.Id
                || mother is not null
                    && _family.GetMother(candidate)?.Id == mother.Id)
            .DistinctBy(candidate => candidate.Id)
            .ToList();
    }

    private IReadOnlyList<IPerson> GetParentSiblings(
        IGameState gameState,
        IPerson person)
    {
        var result =
            new Dictionary<Guid, IPerson>();

        foreach (var parent in
            new[]
            {
                _family.GetFather(person),
                _family.GetMother(person)
            })
        {
            if (parent is null)
                continue;

            foreach (var sibling in
                GetSiblings(gameState, parent))
            {
                result[sibling.Id] = sibling;
            }
        }

        return result.Values.ToList();
    }

    private static IReadOnlyList<IPerson> SortLiving(
        IEnumerable<IPerson> people) =>
        people
            .Where(person => person.Tags.Has("state.alive"))
            .DistinctBy(person => person.Id)
            .OrderBy(person => person.BirthDate?.Year ?? int.MaxValue)
            .ThenBy(person => person.BirthDate?.Month ?? 1)
            .ThenBy(person => person.BirthDate?.Day ?? 1)
            .ThenBy(person => person.Id)
            .ToList();

    internal sealed record EstateHeirResolution(
        IReadOnlyList<IPerson> Heirs,
        string Description);

}
