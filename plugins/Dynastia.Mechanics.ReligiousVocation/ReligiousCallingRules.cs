using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.ReligiousVocation;

internal sealed class ReligiousCallingRules
{
    private const string Path = "LocalSociety/religious_calling_rules.json";

    private ReligiousCallingRules(
        int callingAge,
        double callingChance,
        double departureChance,
        int departureMinimumAge,
        string maleCareerId,
        string femaleCareerId,
        string checkedTag,
        string activeTag,
        string formerTag)
    {
        CallingAge = callingAge;
        CallingChance = callingChance;
        DepartureChance = departureChance;
        DepartureMinimumAge = departureMinimumAge;
        MaleCareerId = maleCareerId;
        FemaleCareerId = femaleCareerId;
        CheckedTag = checkedTag;
        ActiveTag = activeTag;
        FormerTag = formerTag;
    }

    public int CallingAge { get; }
    public double CallingChance { get; }
    public double DepartureChance { get; }
    public int DepartureMinimumAge { get; }
    public string MaleCareerId { get; }
    public string FemaleCareerId { get; }
    public string CheckedTag { get; }
    public string ActiveTag { get; }
    public string FormerTag { get; }

    public static ReligiousCallingRules Load(IGameDataService data)
    {
        using var document = JsonDocument.Parse(data.ReadText(Path));
        var root = document.RootElement;
        var careers = root.GetProperty("careerBySex");
        var tags = root.GetProperty("tags");
        var departure = root.GetProperty("selfDeparture");

        var rules = new ReligiousCallingRules(
            root.GetProperty("callingAge").GetInt32(),
            root.GetProperty("callingChance").GetDouble(),
            departure.GetProperty("annualChance").GetDouble(),
            departure.GetProperty("minimumAge").GetInt32(),
            careers.GetProperty("Male").GetString() ?? string.Empty,
            careers.GetProperty("Female").GetString() ?? string.Empty,
            tags.GetProperty("checked").GetString() ?? string.Empty,
            tags.GetProperty("active").GetString() ?? string.Empty,
            tags.GetProperty("former").GetString() ?? string.Empty);

        if (rules.CallingAge != 18
            || rules.CallingChance is <= 0 or > 1
            || rules.DepartureChance is <= 0 or > 1
            || rules.DepartureMinimumAge < rules.CallingAge + 1
            || string.IsNullOrWhiteSpace(rules.MaleCareerId)
            || string.IsNullOrWhiteSpace(rules.FemaleCareerId)
            || string.IsNullOrWhiteSpace(rules.CheckedTag)
            || string.IsNullOrWhiteSpace(rules.ActiveTag)
            || string.IsNullOrWhiteSpace(rules.FormerTag))
        {
            throw new InvalidDataException($"{Path}: invalid religious-calling rules.");
        }

        return rules;
    }

    public string CareerFor(Sex sex) =>
        sex == Sex.Female ? FemaleCareerId : MaleCareerId;
}
