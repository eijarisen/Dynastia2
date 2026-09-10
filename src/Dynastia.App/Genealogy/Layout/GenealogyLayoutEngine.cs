namespace Dynastia.StandardUI.Genealogy.Layout;

using Dynastia.StandardUI.Genealogy.Models;

/// <summary>
/// Integer-grid genealogy layout from the uploaded feature.
/// </summary>
public sealed class GenealogyLayoutEngine
{
    public int MinimumBranchGap { get; init; } = 1;

    public int SpouseGapColumns { get; init; } = 1;

    public TreeLayout Layout(
        GenealogyGraph graph)
    {
        if (!graph.Nodes.TryGetValue(
            graph.RootId,
            out _))
        {
            throw new InvalidOperationException(
                "Genealogy root is missing.");
        }

        var branch =
            LayoutBloodlinePerson(
                graph,
                graph.RootId,
                new HashSet<Guid>());

        var normalized =
            Normalize(
                branch);

        var allColumns =
            normalized.People
                .Select(
                    person =>
                        person.Grid.Column)
                .ToArray();

        var allRows =
            normalized.People
                .Select(
                    person =>
                        person.Grid.Row)
                .ToArray();

        var bounds =
            allColumns.Length == 0
                ? new GridRect(
                    0,
                    0,
                    0,
                    0)
                : new GridRect(
                    allColumns.Min(),
                    allRows.Min(),
                    allColumns.Max(),
                    allRows.Max());

        return new TreeLayout
        {
            People =
                normalized.People,

            Edges =
                normalized.Edges,

            Bounds =
                bounds,

            PositionsByPersonId =
                normalized.People
                    .GroupBy(
                        person =>
                            person.Person.Id)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group.First().Grid)
        };
    }

    private BranchLayout LayoutBloodlinePerson(
        GenealogyGraph graph,
        Guid personId,
        HashSet<Guid> ancestry)
    {
        if (!graph.Nodes.TryGetValue(
            personId,
            out var node))
        {
            return new BranchLayout();
        }

        if (!ancestry.Add(
            personId))
        {
            throw new InvalidOperationException(
                $"Cycle detected in bloodline at person {personId}.");
        }

        var result =
            new BranchLayout
            {
                RootColumn = 0
            };

        result.People.Add(
            new PositionedPerson(
                node.Person,
                new GridPoint(0, 0),
                false));

        result.IncludeCell(
            0,
            0);

        var occupiedAtRoot =
            0;

        foreach (var union in
            node.Unions
                .OrderBy(
                    item =>
                        item.StartYear)
                .ThenBy(
                    item =>
                        item.SpouseAnchorId))
        {
            var block =
                LayoutUnion(
                    graph,
                    union,
                    ancestry);

            var desiredSpouseColumn =
                occupiedAtRoot
                + SpouseGapColumns;

            var shift =
                desiredSpouseColumn
                - block.RootColumn;

            var shifted =
                block.CloneShifted(
                    shift);

            var extra =
                RequiredShift(
                    result,
                    shifted,
                    MinimumBranchGap);

            if (extra > 0)
            {
                shifted =
                    shifted.CloneShifted(
                        extra);

                desiredSpouseColumn +=
                    extra;
            }

            result.Edges.Add(
                new GenealogyEdge(
                    GenealogyEdgeType.Partnership,
                    new GridPoint(
                        occupiedAtRoot,
                        0),
                    new GridPoint(
                        desiredSpouseColumn,
                        0),
                    union.SpouseAnchorId));

            result.Merge(
                shifted);

            occupiedAtRoot =
                desiredSpouseColumn;
        }

        ancestry.Remove(
            personId);

        return result;
    }

    private BranchLayout LayoutUnion(
        GenealogyGraph graph,
        GenealogyUnion union,
        HashSet<Guid> ancestry)
    {
        var spousePerson =
            FindPerson(
                graph,
                union.SpouseAnchorId);

        var result =
            new BranchLayout
            {
                RootColumn = 0
            };

        result.People.Add(
            new PositionedPerson(
                spousePerson,
                new GridPoint(0, 0),
                true));

        result.IncludeCell(
            0,
            0);

        if (union.Children.Count == 0)
            return result;

        var packedChildren =
            new BranchLayout();

        var first =
            true;

        var childRoots =
            new List<int>();

        foreach (var childId in
            union.Children)
        {
            if (!graph.Nodes.ContainsKey(
                childId))
            {
                continue;
            }

            var child =
                LayoutBloodlinePerson(
                    graph,
                    childId,
                    new HashSet<Guid>(
                        ancestry));

            child =
                ShiftRows(
                    child,
                    1);

            if (first)
            {
                packedChildren.Merge(
                    child);

                childRoots.Add(
                    child.RootColumn);

                first =
                    false;

                continue;
            }

            var shift =
                RequiredShift(
                    packedChildren,
                    child,
                    MinimumBranchGap);

            if (shift <= 0)
            {
                var packedRight =
                    packedChildren
                        .RightContour
                        .Values
                        .DefaultIfEmpty(0)
                        .Max();

                var childLeft =
                    child
                        .LeftContour
                        .Values
                        .DefaultIfEmpty(0)
                        .Min();

                shift =
                    packedRight
                    + MinimumBranchGap
                    + 1
                    - childLeft;
            }

            var shifted =
                child.CloneShifted(
                    shift);

            packedChildren.Merge(
                shifted);

            childRoots.Add(
                shifted.RootColumn);
        }

        if (childRoots.Count == 0)
            return result;

        var center =
            (int)Math.Round(
                (
                    childRoots.Min()
                    + childRoots.Max()
                ) / 2.0,
                MidpointRounding
                    .AwayFromZero);

        packedChildren =
            packedChildren.CloneShifted(
                -center);

        childRoots =
            childRoots
                .Select(
                    column =>
                        column - center)
                .ToList();

        result.Merge(
            packedChildren);

        const int connectorRow =
            1;

        var left =
            childRoots.Min();

        var right =
            childRoots.Max();

        result.Edges.Add(
            new GenealogyEdge(
                GenealogyEdgeType
                    .ParentChildVertical,
                new GridPoint(
                    0,
                    0),
                new GridPoint(
                    0,
                    connectorRow),
                union.SpouseAnchorId));

        result.Edges.Add(
            new GenealogyEdge(
                GenealogyEdgeType
                    .SiblingRail,
                new GridPoint(
                    left,
                    connectorRow),
                new GridPoint(
                    right,
                    connectorRow),
                union.SpouseAnchorId));

        foreach (var childRoot in
            childRoots)
        {
            result.Edges.Add(
                new GenealogyEdge(
                    GenealogyEdgeType
                        .ChildDrop,
                    new GridPoint(
                        childRoot,
                        connectorRow),
                    new GridPoint(
                        childRoot,
                        1),
                    union.SpouseAnchorId));
        }

        return result;
    }

    private static GenealogyPersonRecord
        FindPerson(
            GenealogyGraph graph,
            Guid id)
    {
        foreach (var node in
            graph.Nodes.Values)
        {
            if (node.Person.Id
                == id)
            {
                return node.Person;
            }
        }

        if (graph
                is GenealogyGraphWithPeople
                    graphWithPeople
            && graphWithPeople
                .AllPeople
                .TryGetValue(
                    id,
                    out var person))
        {
            return person;
        }

        throw new InvalidOperationException(
            $"Spouse/person {id} missing from genealogy projection.");
    }

    private static int RequiredShift(
        BranchLayout left,
        BranchLayout right,
        int gap)
    {
        var required =
            0;

        foreach (var row in
            left.RightContour.Keys.Intersect(
                right.LeftContour.Keys))
        {
            var candidate =
                left.RightContour[row]
                + gap
                + 1
                - right.LeftContour[row];

            required =
                Math.Max(
                    required,
                    candidate);
        }

        return required;
    }

    private static BranchLayout ShiftRows(
        BranchLayout source,
        int dy)
    {
        var result =
            new BranchLayout
            {
                RootColumn =
                    source.RootColumn
            };

        result.People.AddRange(
            source.People.Select(
                person =>
                    person with
                    {
                        Grid =
                            new GridPoint(
                                person.Grid.Column,
                                person.Grid.Row + dy)
                    }));

        result.Edges.AddRange(
            source.Edges.Select(
                edge =>
                    edge with
                    {
                        From =
                            new GridPoint(
                                edge.From.Column,
                                edge.From.Row + dy),

                        To =
                            new GridPoint(
                                edge.To.Column,
                                edge.To.Row + dy)
                    }));

        foreach (var (row, value) in
            source.LeftContour)
        {
            result.LeftContour[
                row + dy] =
                    value;
        }

        foreach (var (row, value) in
            source.RightContour)
        {
            result.RightContour[
                row + dy] =
                    value;
        }

        return result;
    }

    private static BranchLayout Normalize(
        BranchLayout source)
    {
        var minColumn =
            source.People
                .Select(
                    person =>
                        person.Grid.Column)
                .DefaultIfEmpty(0)
                .Min();

        return minColumn < 0
            ? source.CloneShifted(
                -minColumn)
            : source;
    }
}

public sealed class GenealogyGraphWithPeople :
    GenealogyGraph
{
    public required IReadOnlyDictionary<
        Guid,
        GenealogyPersonRecord>
        AllPeople { get; init; }
}
