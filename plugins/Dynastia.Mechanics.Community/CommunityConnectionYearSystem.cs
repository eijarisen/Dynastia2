using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CommunityConnectionYearSystem : IYearSystem
{
    private readonly CommunityConnectionService _connections;

    public CommunityConnectionYearSystem(CommunityConnectionService connections) =>
        _connections = connections;

    public string Id => "community.connections.annual";
    public YearPhase Phase => YearPhase.Thoughts;
    public IReadOnlyCollection<string> Before => [];
    public IReadOnlyCollection<string> After => ["actions.queued.family_relations"];

    public void Execute(IGameState gameState) => _connections.AdvanceYear();
}
