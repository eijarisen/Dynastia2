namespace Dynastia.StandardUI.Genealogy.Projection;

using Dynastia.StandardUI.Genealogy.Models;
using Dynastia.StandardUI.Genealogy.Layout;

public sealed class GenealogyProjectionBuilder
{
    public GenealogyGraph Build(
        GenealogySnapshot snapshot)
    {
        var people =
            snapshot.People.ToDictionary(
                person => person.Id);

        var rootId =
            snapshot.FounderId
            ?? snapshot.People
                .Where(
                    person =>
                        person.IsBloodline)
                .OrderBy(
                    person =>
                        person.BirthYear)
                .ThenBy(
                    person =>
                        person.Id)
                .Select(
                    person =>
                        (Guid?)person.Id)
                .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Genealogy contains no bloodline founder.");

        var nodes =
            snapshot.People
                .Where(
                    person =>
                        person.IsBloodline)
                .ToDictionary(
                    person =>
                        person.Id,
                    person =>
                        new GenealogyNode
                        {
                            Person =
                                person
                        });

        var childrenByParent =
            snapshot.People
                .SelectMany(
                    child =>
                        child.ParentIds.Select(
                            parent =>
                                (parent, child)))
                .GroupBy(
                    item =>
                        item.parent)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group
                            .Select(
                                item =>
                                    item.child)
                            .ToArray());

        foreach (var node in
            nodes.Values)
        {
            var normalizedMarriages =
                node.Person.MarriageHistory
                    .ToList();

            if (node.Person.IsAlive
                && node.Person.CurrentSpouseId
                    is Guid currentSpouseId
                && people.TryGetValue(
                    currentSpouseId,
                    out var currentSpouse)
                && currentSpouse.IsAlive
                && currentSpouse.CurrentSpouseId
                    == node.Person.Id
                && normalizedMarriages.All(
                    marriage =>
                        marriage.SpouseId
                            != currentSpouseId
                        || marriage.EndYear
                            is not null))
            {
                normalizedMarriages.Add(
                    new GenealogyMarriageRecord(
                        currentSpouseId,
                        node.Person.BirthYear));
            }

            var marriages =
                normalizedMarriages
                    .GroupBy(
                        marriage =>
                            (
                                marriage.SpouseId,
                                marriage.StartYear
                            ))
                    .Select(
                        group =>
                            group
                                .OrderByDescending(
                                    marriage =>
                                        marriage.EndYear
                                        ?? int.MaxValue)
                                .First())
                    .OrderBy(
                        marriage =>
                            marriage.StartYear)
                    .ThenBy(
                        marriage =>
                            marriage.SpouseId);

            foreach (var marriage in
                marriages)
            {
                if (!people.TryGetValue(
                    marriage.SpouseId,
                    out var spouse))
                {
                    continue;
                }

                // Preserve uploaded design: recursive expansion is bloodline-only.
                if (spouse.IsBloodline)
                    continue;

                var union =
                    new GenealogyUnion
                    {
                        BloodlineParentId =
                            node.Person.Id,

                        SpouseAnchorId =
                            spouse.Id,

                        StartYear =
                            marriage.StartYear,

                        EndYear =
                            marriage.EndYear,

                        EndReason =
                            marriage.EndReason
                    };

                if (childrenByParent.TryGetValue(
                    node.Person.Id,
                    out var candidateChildren))
                {
                    foreach (var child in
                        candidateChildren
                            .Where(
                                child =>
                                    child.IsBloodline)
                            .Where(
                                child =>
                                    child.ParentIds.Contains(
                                        spouse.Id))
                            .OrderBy(
                                child =>
                                    child.BirthYear)
                            .ThenBy(
                                child =>
                                    child.Id))
                    {
                        union.Children.Add(
                            child.Id);
                    }
                }

                node.Unions.Add(
                    union);
            }
        }

        AssignDepths(
            nodes,
            rootId);

        return new GenealogyGraphWithPeople
        {
            RootId =
                rootId,

            Nodes =
                nodes,

            AllPeople =
                people
        };
    }

    private static void AssignDepths(
        IReadOnlyDictionary<Guid, GenealogyNode> nodes,
        Guid rootId)
    {
        var queue =
            new Queue<(Guid Id, int Depth)>();

        var visited =
            new HashSet<Guid>();

        queue.Enqueue(
            (rootId, 0));

        while (queue.Count > 0)
        {
            var (id, depth) =
                queue.Dequeue();

            if (!visited.Add(id)
                || !nodes.TryGetValue(
                    id,
                    out var node))
            {
                continue;
            }

            node.TreeDepth =
                depth;

            foreach (var childId in
                node.Unions.SelectMany(
                    union =>
                        union.Children))
            {
                if (nodes.ContainsKey(
                    childId))
                {
                    queue.Enqueue(
                        (childId, depth + 1));
                }
            }
        }
    }
}
