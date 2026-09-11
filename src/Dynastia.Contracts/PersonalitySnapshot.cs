namespace Dynastia.Contracts;

public sealed record PersonalitySnapshot(
    string Temperament,
    string Morals)
{
    public string DisplayName =>
        $"{Temperament} {Morals}";
}
