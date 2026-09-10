namespace Dynastia.StandardUI.Genealogy.Models;

public class GenealogyGraph
{
    public required Guid RootId { get; init; }

    public required IReadOnlyDictionary<Guid, GenealogyNode>
        Nodes { get; init; }
}

public sealed class GenealogyNode
{
    public required GenealogyPersonRecord Person { get; init; }

    public int TreeDepth { get; set; }

    public List<GenealogyUnion> Unions { get; } = [];
}

/// <summary>
/// The bloodline person is the recursive branch node. The non-bloodline spouse is
/// the spouse anchor, and the child connector is drawn from the spouse anchor.
/// </summary>
public sealed class GenealogyUnion
{
    public required Guid BloodlineParentId { get; init; }

    public required Guid SpouseAnchorId { get; init; }

    public required int StartYear { get; init; }

    public int? EndYear { get; init; }

    public string? EndReason { get; init; }

    public List<Guid> Children { get; } = [];
}
