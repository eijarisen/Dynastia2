namespace Dynastia.Mechanics.Economy;

public sealed class LedgerLineState
{
    public string Label { get; set; } =
        string.Empty;

    public decimal Amount { get; set; }

    // Nullable for backward save compatibility. Person-backed income lines
    // persist this so UI consumers can resolve the earner's current career
    // details instead of being left with only the old display label.
    public Guid? PersonId { get; set; }
}
