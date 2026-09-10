namespace Dynastia.StandardUI.Genealogy.Layout;

using Dynastia.StandardUI.Genealogy.Models;

public readonly record struct GridPoint(
    int Column,
    int Row);

public readonly record struct GridRect(
    int Left,
    int Top,
    int Right,
    int Bottom)
{
    public int Width =>
        Right - Left + 1;

    public int Height =>
        Bottom - Top + 1;
}

public enum GenealogyEdgeType
{
    Partnership,
    ParentChildVertical,
    SiblingRail,
    ChildDrop
}

public sealed record PositionedPerson(
    GenealogyPersonRecord Person,
    GridPoint Grid,
    bool IsSpouseAnchor);

public sealed record GenealogyEdge(
    GenealogyEdgeType Type,
    GridPoint From,
    GridPoint To,
    Guid? RelatedPersonId = null);

public sealed class TreeLayout
{
    public required IReadOnlyList<PositionedPerson>
        People { get; init; }

    public required IReadOnlyList<GenealogyEdge>
        Edges { get; init; }

    public required GridRect Bounds { get; init; }

    public required IReadOnlyDictionary<Guid, GridPoint>
        PositionsByPersonId { get; init; }

    public TreeLayout WithPeople(
        IReadOnlyDictionary<Guid, GenealogyPersonRecord> people)
    {
        return new TreeLayout
        {
            People =
                People
                    .Select(
                        positioned =>
                            people.TryGetValue(
                                positioned.Person.Id,
                                out var current)
                                ? positioned with
                                {
                                    Person =
                                        current
                                }
                                : positioned)
                    .ToList(),

            Edges =
                Edges,

            Bounds =
                Bounds,

            PositionsByPersonId =
                PositionsByPersonId
        };
    }
}

internal sealed class BranchLayout
{
    public List<PositionedPerson> People { get; } = [];

    public List<GenealogyEdge> Edges { get; } = [];

    public Dictionary<int, int> LeftContour { get; } = [];

    public Dictionary<int, int> RightContour { get; } = [];

    public int RootColumn { get; set; }

    public BranchLayout CloneShifted(
        int dx)
    {
        var copy =
            new BranchLayout
            {
                RootColumn =
                    RootColumn + dx
            };

        copy.People.AddRange(
            People.Select(
                person =>
                    person with
                    {
                        Grid =
                            new GridPoint(
                                person.Grid.Column + dx,
                                person.Grid.Row)
                    }));

        copy.Edges.AddRange(
            Edges.Select(
                edge =>
                    edge with
                    {
                        From =
                            new GridPoint(
                                edge.From.Column + dx,
                                edge.From.Row),

                        To =
                            new GridPoint(
                                edge.To.Column + dx,
                                edge.To.Row)
                    }));

        foreach (var (row, value) in
            LeftContour)
        {
            copy.LeftContour[
                row] =
                    value + dx;
        }

        foreach (var (row, value) in
            RightContour)
        {
            copy.RightContour[
                row] =
                    value + dx;
        }

        return copy;
    }

    public void IncludeCell(
        int column,
        int row)
    {
        if (!LeftContour.TryGetValue(
                row,
                out var left)
            || column < left)
        {
            LeftContour[row] =
                column;
        }

        if (!RightContour.TryGetValue(
                row,
                out var right)
            || column > right)
        {
            RightContour[row] =
                column;
        }
    }

    public void Merge(
        BranchLayout other)
    {
        People.AddRange(
            other.People);

        Edges.AddRange(
            other.Edges);

        foreach (var row in
            other.LeftContour.Keys.Union(
                other.RightContour.Keys))
        {
            if (other.LeftContour.TryGetValue(
                    row,
                    out var left))
            {
                if (!LeftContour.TryGetValue(
                        row,
                        out var existing)
                    || left < existing)
                {
                    LeftContour[row] =
                        left;
                }
            }

            if (other.RightContour.TryGetValue(
                    row,
                    out var right))
            {
                if (!RightContour.TryGetValue(
                        row,
                        out var existing)
                    || right > existing)
                {
                    RightContour[row] =
                        right;
                }
            }
        }
    }
}
