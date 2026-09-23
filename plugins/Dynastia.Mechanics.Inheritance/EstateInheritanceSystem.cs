using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

public sealed class EstateInheritanceSystem : IYearSystem
{
    private readonly IEconomyService _economy;
    private readonly EstateSnapshotReader _snapshots;
    private readonly EstatePlanBuilder _planner = new();
    private readonly EstateSettlementApplier _applier;

    public EstateInheritanceSystem(
        IFamilyService family,
        IEconomyService economy,
        IHeirloomService heirlooms,
        IGameEventBus events)
        : this(family, economy, heirlooms, () => null, events)
    {
    }

    public EstateInheritanceSystem(
        IFamilyService family,
        IEconomyService economy,
        IHeirloomService heirlooms,
        Func<IFarmingService?> farmingResolver,
        IGameEventBus events)
    {
        _economy = economy;
        _snapshots = new EstateSnapshotReader(family, economy, heirlooms, farmingResolver);
        _applier = new EstateSettlementApplier(family, economy, heirlooms, events);
    }

    public string Id => "inheritance.estate_settlement";
    public YearPhase Phase => YearPhase.Inheritance;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["households.inheritance_reconcile"];

    public void Execute(IGameState gameState)
    {
        var readyHouseholds = gameState.People
            .Where(person => _economy.HasHousehold(person) && _economy.IsEstateReady(person))
            .ToList();
        foreach (var head in readyHouseholds)
        {
            var snapshot = _snapshots.Capture(gameState, head);
            var plan = _planner.Build(snapshot);
            _applier.Apply(gameState, plan);
        }
    }
}
