namespace Dynastia.Contracts;

/// <summary>
/// UI-independent action presentation. Empty means that the host should use its
/// legacy presentation fallback. Supplying a metadata object opts into its
/// category, grouping and visibility settings; an omitted emoji still falls back.
/// </summary>
public sealed record ActionPresentationMetadata
{
    public static ActionPresentationMetadata Empty { get; } = new();

    public string? Emoji { get; init; }

    // An explicitly empty category list is unfiltered (for example, Pass).
    public IReadOnlyList<string> Categories { get; init; } = [];

    public string? AdjacencyGroup { get; init; }
    public int GroupOrder { get; init; }
    public bool PlaceLast { get; init; }
    public bool ShowInPrimaryActionList { get; init; } = true;

    // Employment assistance follows the first job-search action, which can also
    // belong to a different adjacency group. Separate anchors preserve that
    // placement without moving the search actions out of their existing groups.
    public string? PlacementAnchor { get; init; }
    public string? PlaceAfterAnchor { get; init; }
}
