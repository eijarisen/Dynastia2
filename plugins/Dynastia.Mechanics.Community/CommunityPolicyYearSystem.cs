using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CommunityPolicyYearSystem : IYearSystem
{
    private readonly CommunityPolicyService _community;
    private readonly IHouseholdService _households;
    private readonly IEconomyService _economy;
    private readonly IStatusService _status;
    private readonly ILocationService _locations;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly CivicOfficeService _civic;

    public CommunityPolicyYearSystem(
        CommunityPolicyService community,
        IHouseholdService households,
        IEconomyService economy,
        IStatusService status,
        ILocationService locations,
        IGameRandom random,
        IGameEventBus events,
        CivicOfficeService civic)
    {
        _community = community;
        _households = households;
        _economy = economy;
        _status = status;
        _locations = locations;
        _random = random;
        _events = events;
        _civic = civic;
    }

    public string Id => "community.policy_resolution";
    public YearPhase Phase => YearPhase.QueuedActionsEarly;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["actions.queued.early"];

    public void Execute(IGameState gameState) =>
        _community.ResolvePreviousYear(
            _households,
            _economy,
            _status,
            _locations,
            _random,
            _events,
            _civic);
}
