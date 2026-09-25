namespace Dynastia.Contracts;

public sealed record ThoughtWordingDefinition
{
    public IReadOnlyList<string> Child { get; init; } = [];
    public IReadOnlyList<string> Adolescent { get; init; } = [];
    public IReadOnlyList<string> AdultRough { get; init; } = [];
    public IReadOnlyList<string> AdultNormal { get; init; } = [];
    public IReadOnlyList<string> AdultElaborate { get; init; } = [];
}
