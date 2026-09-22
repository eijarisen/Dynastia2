using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

internal sealed class NonmaritalBirthRules
{
    private const string Path = "LocalSociety/nonmarital_birth_rules.json";

    public int MinimumAge { get; init; }
    public int MaximumAge { get; init; }
    public double BaseAnnualChance { get; init; }
    public IReadOnlyDictionary<int, double> FertilityMultipliers { get; init; } =
        new Dictionary<int, double>();
    public IReadOnlyList<AgeMultiplierBand> AgeMultipliers { get; init; } = [];
    public IReadOnlyDictionary<string, double> TemperamentMultipliers { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, double> MoralsMultipliers { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public double UnknownFatherChance { get; init; }
    public double MarryFatherSameYearChance { get; init; }
    public int FatherMinimumAge { get; init; }
    public int FatherMaximumAgeOffsetFromMother { get; init; }

    public double CalculateChance(
        int age,
        int fertility,
        PersonalitySnapshot? personality)
    {
        if (age < MinimumAge
            || age > MaximumAge
            || fertility <= 0)
        {
            return 0;
        }

        var fertilityMultiplier = FertilityMultipliers.TryGetValue(
            fertility,
            out var storedFertility)
                ? storedFertility
                : 1.0;

        var ageMultiplier = AgeMultipliers
            .FirstOrDefault(band => band.Contains(age))
            ?.Multiplier
            ?? 1.0;

        var temperamentMultiplier = personality is not null
            && TemperamentMultipliers.TryGetValue(
                personality.Temperament,
                out var storedTemperament)
                    ? storedTemperament
                    : 1.0;

        var moralsMultiplier = personality is not null
            && MoralsMultipliers.TryGetValue(
                personality.Morals,
                out var storedMorals)
                    ? storedMorals
                    : 1.0;

        return Math.Clamp(
            BaseAnnualChance
            * fertilityMultiplier
            * ageMultiplier
            * temperamentMultiplier
            * moralsMultiplier,
            0,
            1);
    }

    public static NonmaritalBirthRules Load(IGameDataService data)
    {
        using var document = JsonDocument.Parse(data.ReadText(Path));
        var root = document.RootElement;
        var fertility = root.GetProperty("fertilityMultipliers");
        var temperament = root.GetProperty("temperamentMultipliers");
        var morals = root.GetProperty("moralsMultipliers");
        var outcomes = root.GetProperty("outcomes");
        var marryFather = root.GetProperty("marryFather");

        var rules = new NonmaritalBirthRules
        {
            MinimumAge = root.GetProperty("minimumAge").GetInt32(),
            MaximumAge = root.GetProperty("maximumAge").GetInt32(),
            BaseAnnualChance = root.GetProperty("baseAnnualChance").GetDouble(),
            FertilityMultipliers = fertility
                .EnumerateObject()
                .ToDictionary(
                    property => int.Parse(property.Name),
                    property => property.Value.GetDouble()),
            AgeMultipliers = root.GetProperty("ageMultipliers")
                .EnumerateArray()
                .Select(item => new AgeMultiplierBand(
                    item.GetProperty("min").GetInt32(),
                    item.GetProperty("max").GetInt32(),
                    item.GetProperty("multiplier").GetDouble()))
                .ToArray(),
            TemperamentMultipliers = temperament
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => property.Value.GetDouble(),
                    StringComparer.OrdinalIgnoreCase),
            MoralsMultipliers = morals
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => property.Value.GetDouble(),
                    StringComparer.OrdinalIgnoreCase),
            UnknownFatherChance = outcomes.GetProperty("unknownFatherChance").GetDouble(),
            MarryFatherSameYearChance = outcomes.GetProperty("marryFatherSameYearChance").GetDouble(),
            FatherMinimumAge = marryFather.GetProperty("fatherMinimumAge").GetInt32(),
            FatherMaximumAgeOffsetFromMother = marryFather
                .GetProperty("fatherMaximumAgeOffsetFromMother")
                .GetInt32()
        };

        if (rules.MinimumAge < 0
            || rules.MaximumAge < rules.MinimumAge
            || rules.BaseAnnualChance < 0
            || rules.BaseAnnualChance > 1
            || rules.UnknownFatherChance < 0
            || rules.UnknownFatherChance > 1
            || rules.MarryFatherSameYearChance < 0
            || rules.MarryFatherSameYearChance > 1
            || Math.Abs(
                rules.UnknownFatherChance
                + rules.MarryFatherSameYearChance
                - 1.0) > 1e-9
            || rules.FatherMinimumAge < 18
            || rules.FatherMaximumAgeOffsetFromMother < 0)
        {
            throw new InvalidDataException(
                $"Invalid non-marital birth rules in '{Path}'.");
        }

        return rules;
    }

    internal sealed record AgeMultiplierBand(
        int MinimumAge,
        int MaximumAge,
        double Multiplier)
    {
        public bool Contains(int age) =>
            age >= MinimumAge && age <= MaximumAge;
    }
}
