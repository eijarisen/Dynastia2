using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

[PersistedComponentId("justice.person")]
public sealed class JusticeComponent
{
    public int PrisonSentence { get; set; }

    public string? CrimeId { get; set; }

    public string? CrimeName { get; set; }

    public string? CrimeDescription { get; set; }
}
