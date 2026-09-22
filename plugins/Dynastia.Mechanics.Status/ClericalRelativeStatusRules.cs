using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Status;

internal sealed class ClericalRelativeStatusRules
{
    private const string Path = "LocalSociety/clerical_relative_status_rules.json";
    private readonly IReadOnlyList<Rule> _rules;

    private ClericalRelativeStatusRules(
        IReadOnlyList<Rule> rules,
        double renownCap,
        double reputationCap)
    {
        _rules = rules;
        RenownCap = renownCap;
        ReputationCap = reputationCap;
    }

    public double RenownCap { get; }
    public double ReputationCap { get; }

    public static ClericalRelativeStatusRules Load(IGameDataService data)
    {
        using var document = JsonDocument.Parse(data.ReadText(Path));
        var root = document.RootElement;
        var rules = new List<Rule>();

        foreach (var item in root.GetProperty("bonuses").EnumerateArray())
        {
            rules.Add(new Rule(
                item.GetProperty("relationship").GetString() ?? string.Empty,
                item.TryGetProperty("activeVocationLevelMin", out var min) ? min.GetInt32() : null,
                item.TryGetProperty("activeVocationLevelMax", out var max) ? max.GetInt32() : null,
                item.GetProperty("renown").GetDouble(),
                item.GetProperty("reputation").GetDouble()));
        }

        var caps = root.GetProperty("perPersonCaps");
        var result = new ClericalRelativeStatusRules(
            rules,
            caps.GetProperty("renown").GetDouble(),
            caps.GetProperty("reputation").GetDouble());

        if (rules.Count == 0
            || result.RenownCap <= 0
            || result.ReputationCap <= 0)
        {
            throw new InvalidDataException($"{Path}: invalid clerical-relative Status rules.");
        }

        return result;
    }

    public StatusBonus Resolve(string relationship, int activeVocationLevel)
    {
        var rule = _rules.FirstOrDefault(item =>
            item.Relationship.Equals(relationship, StringComparison.OrdinalIgnoreCase)
            && (!item.MinimumLevel.HasValue || activeVocationLevel >= item.MinimumLevel.Value)
            && (!item.MaximumLevel.HasValue || activeVocationLevel <= item.MaximumLevel.Value));

        return rule is null
            ? new StatusBonus(0, 0)
            : new StatusBonus(rule.Renown, rule.Reputation);
    }

    internal sealed record StatusBonus(double Renown, double Reputation);
    private sealed record Rule(
        string Relationship,
        int? MinimumLevel,
        int? MaximumLevel,
        double Renown,
        double Reputation);
}
