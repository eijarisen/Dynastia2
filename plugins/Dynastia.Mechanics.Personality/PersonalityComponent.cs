using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

[PersistedComponentId("personality.person")]
public sealed class PersonalityComponent
{
    public string? Temperament { get; set; }
    public string? Morals { get; set; }

    public bool HasMoralsProtection { get; set; }

    public int? LastMoralsDeclineYear { get; set; }
}
