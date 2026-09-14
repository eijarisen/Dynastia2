namespace Dynastia.Contracts;

public sealed record RelationshipEraRule(
    int StartYear,
    int? EndYear,
    double MarriageChanceMultiplier,
    double ArrangedMarriageMultiplier,
    double AutomaticDivorceMultiplier)
{
    public bool Covers(int year) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value);

    public double ApplyMarriageChance(double chance) =>
        chance * MarriageChanceMultiplier;

    public double ApplyArrangedMarriageChance(double chance) =>
        Math.Min(
            0.95,
            chance * ArrangedMarriageMultiplier);

    public double ApplyAutomaticDivorceChance(double chance) =>
        chance * AutomaticDivorceMultiplier;
}
