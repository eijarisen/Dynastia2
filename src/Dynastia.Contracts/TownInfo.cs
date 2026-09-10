namespace Dynastia.Contracts;

public sealed record TownInfo(
    string Town,
    string County,
    double Longitude,
    double Latitude,
    int Population)
{
    public string DisplayName =>
        string.Equals(
            Town,
            County,
            StringComparison.OrdinalIgnoreCase)
                ? Town
                : $"{Town}, {County}";
}
