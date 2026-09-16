namespace Dynastia.Contracts;

public sealed record CraftInfo(
    string Id,
    string Name,
    int StartYear,
    string Emoji,
    string SelfEmploymentTitle,
    string PrimaryCareerId,
    IReadOnlyList<string> RelatedCareerIds);
