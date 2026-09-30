namespace BearAdventure.Domain.World;

/// <summary>
/// Deterministic cell geometry derived from the already-materialized structure definition.
/// No additional generation state is required in saves.
/// </summary>
public sealed class GeneratedStructureLayout
{
    public required int StructureId { get; init; }
    public required GeneratedStructureKind Kind { get; init; }
    public required int LeftCellX { get; init; }
    public required int RightCellX { get; init; }
    public required int BaseLogicalLevel { get; init; }
    public required int DoorCellX { get; init; }
    public required IReadOnlySet<UndergroundCell> SolidCells { get; init; }
    public required IReadOnlySet<UndergroundCell> BackgroundCells { get; init; }
    public required IReadOnlySet<UndergroundCell> LadderCells { get; init; }
    public required IReadOnlySet<UndergroundCell> CarvedCells { get; init; }
    public required IReadOnlySet<UndergroundCell> AccessCells { get; init; }
    public required WorldRect ReservedBounds { get; init; }

    public bool Solid(UndergroundCell cell) => SolidCells.Contains(cell);
    public bool Ladder(UndergroundCell cell) => LadderCells.Contains(cell);
    public bool Carves(UndergroundCell cell) => CarvedCells.Contains(cell);
    public bool Access(UndergroundCell cell) => AccessCells.Contains(cell);
}
