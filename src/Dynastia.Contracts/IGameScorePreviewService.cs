namespace Dynastia.Contracts;

/// <summary>Read-only evaluation of hypothetical outcomes against the current score ledger.</summary>
public interface IGameScorePreviewService
{
    /// <summary>
    /// Returns the net score change of the outcomes, in order, without publishing events,
    /// changing state, or consuming randomness. Includes existing claims and one-time awards.
    /// </summary>
    long PreviewEventDelta(IReadOnlyList<GameEvent> events);
}
