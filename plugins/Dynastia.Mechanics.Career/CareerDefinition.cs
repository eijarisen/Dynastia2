using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed record CareerDefinition(
    string Id,
    string Name,
    string Emoji,
    int StartYear,
    int? EndYear,
    int MaleEarly,
    int FemaleEarly,
    int MaleLate,
    int FemaleLate,
    decimal BaseSalary,
    string Level1Title,
    string Level2Title,
    string Level3Title,
    string Level4Title,
    string Level5Title,
    CareerLocationType LocationType,
    SettlementClass MinimumSettlementClass,
    IReadOnlyList<string> RequiredOpportunityTags,
    string PrimaryStat,
    string? SecondaryStat,
    string EducationProfile,
    string CareerFamily)
{
    public const int TechnologyFreezeYear =
        GameCalendarConfiguration.TechnologyFreezeYear;

    public CareerLocationRequirement LocationRequirement =>
        new(
            LocationType,
            MinimumSettlementClass,
            RequiredOpportunityTags);

    public bool IsOpenForEntry(int gameYear)
    {
        var year = EffectiveTechnologyYear(gameYear);
        return year >= StartYear
            && (EndYear is null || year <= EndYear.Value);
    }

    public double GetEntryWeight(Sex sex, int gameYear)
    {
        var year = EffectiveTechnologyYear(gameYear);
        var early = sex == Sex.Male ? MaleEarly : FemaleEarly;
        var late = sex == Sex.Male ? MaleLate : FemaleLate;

        if (year <= StartYear)
            return early;

        var weightingTargetYear = Math.Min(
            EndYear ?? TechnologyFreezeYear,
            TechnologyFreezeYear);

        if (StartYear >= weightingTargetYear || year >= weightingTargetYear)
            return late;

        var progress = Math.Clamp(
            (year - StartYear) / (double)(weightingTargetYear - StartYear),
            0,
            1);

        return early + (late - early) * progress;
    }

    public string GetTitle(int jobLevel) => jobLevel switch
    {
        1 => Level1Title,
        2 => Level2Title,
        3 => Level3Title,
        4 => Level4Title,
        5 => Level5Title,
        _ => string.Empty
    };

    public CareerObsolescencePressure GetObsolescencePressure(int gameYear)
    {
        if (EndYear is null)
            return CareerObsolescencePressure.None;

        var yearsAfterEnd = gameYear - EndYear.Value;
        if (yearsAfterEnd <= 0)
            return CareerObsolescencePressure.None;
        if (yearsAfterEnd <= 5)
            return new CareerObsolescencePressure(yearsAfterEnd, 0.50, 0.05);
        if (yearsAfterEnd <= 15)
            return new CareerObsolescencePressure(yearsAfterEnd, 0.20, 0.15);
        return new CareerObsolescencePressure(yearsAfterEnd, 0, 0.35);
    }

    private static int EffectiveTechnologyYear(int gameYear) =>
        Math.Min(gameYear, TechnologyFreezeYear);
}

internal sealed record CareerObsolescencePressure(
    int YearsAfterEnd,
    double PromotionMultiplier,
    double AdditionalJobLossChance)
{
    public static CareerObsolescencePressure None { get; } =
        new(0, 1, 0);
}
