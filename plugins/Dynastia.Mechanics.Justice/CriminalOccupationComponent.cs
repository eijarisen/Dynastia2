using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

[PersistedComponentId("justice.criminal_occupation")]
public sealed class CriminalOccupationComponent
{
    public bool HasStartedLifeOfCrime { get; set; }

    public bool IsActive { get; set; }

    public int ActiveHeistYears { get; set; }

    public int LastHeistYear { get; set; }

    public int PendingHeistYear { get; set; }

    public decimal PendingHeistProceeds { get; set; }

    public decimal LastAnnualIncome { get; set; }

    public int LastIncomeYear { get; set; }
}
