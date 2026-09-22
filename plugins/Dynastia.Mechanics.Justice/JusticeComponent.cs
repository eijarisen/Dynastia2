using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

[PersistedComponentId("justice.person")]
public sealed class JusticeComponent
{
    public int PrisonSentence { get; set; }

    public string? CrimeId { get; set; }

    public string? CrimeName { get; set; }

    public string? CrimeDescription { get; set; }

    public Guid? CurrentImprisonmentId { get; set; }

    public bool EscapeAttemptedCurrentImprisonment { get; set; }

    public List<CriminalRecordEntryState> CriminalRecord { get; set; } = [];
}

public sealed class CriminalRecordEntryState
{
    public int Year { get; set; }
    public string CrimeId { get; set; } = string.Empty;
    public string CrimeName { get; set; } = string.Empty;
    public int OriginalSentence { get; set; }
    public int FinalSentence { get; set; }
}
