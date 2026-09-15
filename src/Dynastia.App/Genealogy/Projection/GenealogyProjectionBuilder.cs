namespace Dynastia.StandardUI.Genealogy.Projection;

using Dynastia.StandardUI.Genealogy.Models;
using Dynastia.StandardUI.Genealogy.Layout;

public sealed class GenealogyProjectionBuilder
{
    public GenealogyGraph Build(
        GenealogySnapshot snapshot,
        GenealogyProjectionOptions? options = null)
    {
        options ??=
            new GenealogyProjectionOptions();

        var people =
            snapshot.People.ToDictionary(
                person => person.Id);

        var nodePeople =
            snapshot.People
                .Where(
                    person =>
                        person.IsBloodline)
                .Where(
                    person =>
                        !options.MaleLineageOnly
                        || (
                            person.IsMaleLineage
                            && !person.IsFemale
                        ))
                .ToArray();

        var rootId =
            ResolveRootId(
                snapshot,
                nodePeople);

        var nodes =
            nodePeople
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
            if (options.IsCollapsed(
                    node.Person.Id))
            {
                continue;
            }

            if (!options.MaleLineageOnly
                && !options.IncludeDaughtersFamilies
                && node.Person.IsFemale)
            {
                continue;
            }

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

                // Bloodline spouses normally own their own recursive branch.
                // The founding parents are the exception: both are bloodline,
                // but the mother is rendered as the root father's spouse so
                // their children can form the first visible sibling row.
                if (spouse.IsBloodline
                    && node.Person.Id != rootId)
                {
                    continue;
                }

                var bloodlineChildren =
                    childrenByParent.TryGetValue(
                        node.Person.Id,
                        out var candidateChildren)
                        ? candidateChildren
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
                                    child.Id)
                            .ToArray()
                        : [];

                if (!options.ShowAllSpouses
                    && IsExPartnerWithoutBloodlineChildren(
                        marriage,
                        bloodlineChildren.Length))
                {
                    continue;
                }

                IEnumerable<GenealogyPersonRecord>
                    visibleChildren =
                        bloodlineChildren;

                if (options.MaleLineageOnly)
                {
                    visibleChildren =
                        visibleChildren.Where(
                            child =>
                                child.IsMaleLineage
                                && !child.IsFemale);

                    // In this view spouses are shown only when they are the
                    // mother of a visible male-lineage child.
                    if (!spouse.IsFemale
                        || !visibleChildren.Any())
                    {
                        continue;
                    }
                }

                if (options.IsCollapsed(
                    spouse.Id))
                {
                    visibleChildren =
                        [];
                }

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

                foreach (var child in
                    visibleChildren)
                {
                    if (nodes.ContainsKey(
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

    private static Guid ResolveRootId(
        GenealogySnapshot snapshot,
        IReadOnlyCollection<GenealogyPersonRecord> nodePeople)
    {
        if (snapshot.FounderId
                is Guid preferredRoot
            && nodePeople.Any(
                person =>
                    person.Id == preferredRoot))
        {
            return preferredRoot;
        }

        return nodePeople
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
                "Genealogy contains no visible bloodline founder.");
    }

    private static bool
        IsExPartnerWithoutBloodlineChildren(
            GenealogyMarriageRecord marriage,
            int bloodlineChildCount)
    {
        if (bloodlineChildCount > 0
            || marriage.EndYear is null)
        {
            return false;
        }

        return !string.Equals(
            marriage.EndReason,
            "death",
            StringComparison.OrdinalIgnoreCase);
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
