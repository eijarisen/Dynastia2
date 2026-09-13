namespace Dynastia.Mechanics.Loans;

public sealed class LoanCreditorShareState
{
    public Guid? OwnerPersonId { get; set; }
    public Guid? RecipientHouseholdId { get; set; }
    public decimal Share { get; set; } = 1m;
}
