namespace Dynastia.Mechanics.Personality;

public sealed class PersonalityComponent
{
    public string? Temperament { get; set; }
    public string? Morals { get; set; }

    public int MoralsProtection { get; set; }

    public int LastDownwardMoralsAttemptYear { get; set; }
}
