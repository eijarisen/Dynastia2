namespace Dynastia.Contracts;

public sealed class ThoughtContext
{
    public required int Year { get; init; }

    public required IReadOnlyList<GameEvent> Events { get; init; }

    public required IGameState GameState { get; init; }

    public required IFamilyService Family { get; init; }

    public required IStatsService Stats { get; init; }

    public required IHealthService Health { get; init; }

    public required ICareerService Career { get; init; }

    public required IHouseholdService Households { get; init; }

    public required IJusticeService Justice { get; init; }

    public required IEducationService Education { get; init; }

    public required IEconomyService Economy { get; init; }

    public required IAdoptionService Adoption { get; init; }

    public required IMarriageSatisfactionService MarriageSatisfaction { get; init; }
}
