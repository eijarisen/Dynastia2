namespace Dynastia.Mechanics.Justice;

public sealed class CrimeDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double Weight { get; init; }
    public int SentenceMin { get; init; }
    public int SentenceMax { get; init; }
    public int ProfitMin { get; init; }
    public int ProfitMax { get; init; }
    public double SuccessBase { get; init; }
    public double DetectionBase { get; init; }
    public bool RequiresEmployment { get; init; }
    public string Description { get; init; } = string.Empty;

    public bool IsProfitCrime => ProfitMax > 0;
    public bool IsPlanned => Category.Contains("financial", StringComparison.OrdinalIgnoreCase)
        || Category.Contains("property", StringComparison.OrdinalIgnoreCase);
    public bool IsViolentOrImpulsive => Category.Contains("violent", StringComparison.OrdinalIgnoreCase)
        || Category.Equals("impulsive", StringComparison.OrdinalIgnoreCase)
        || Category.Equals("extreme", StringComparison.OrdinalIgnoreCase);
}
