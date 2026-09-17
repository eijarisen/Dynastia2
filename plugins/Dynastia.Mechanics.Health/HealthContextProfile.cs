using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

internal static class HealthContextProfile
{
    public static ContextWeightContext Build(
        IPerson person,
        int year,
        IFamilyService family,
        IExistingLocationService locations)
    {
        return new ContextWeightContext(
            year,
            person.Age,
            family.GetSex(person),
            ResolveTagValue(person, "personality.", ["Melancholic", "Phlegmatic", "Sanguine", "Choleric"]),
            ResolveTagValue(person, "morals.", ["Good", "Neutral", "Evil"]),
            locations.GetExistingLocation(person)?.HomeTown.SettlementClass);
    }

    private static string? ResolveTagValue(
        IPerson person,
        string prefix,
        IReadOnlyList<string> values)
    {
        return values.FirstOrDefault(value =>
            person.Tags.Has(prefix + value.ToLowerInvariant()));
    }
}
