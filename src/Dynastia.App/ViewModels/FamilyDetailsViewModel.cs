namespace Dynastia.App.ViewModels;

public sealed record RelationshipPersonLineViewModel(
    string Label,
    Guid? PersonId,
    string DisplayText)
{
    public bool IsSelectable => PersonId.HasValue;
    public bool IsPlainText => !PersonId.HasValue;
}

public sealed record RelationshipHistoryLineViewModel(
    string PeriodText,
    Guid? PersonId,
    string PersonText,
    string ReasonText)
{
    public bool IsSelectable => PersonId.HasValue;
    public bool IsPlainText => !PersonId.HasValue;
}

public sealed class FamilyDetailsViewModel
{
    public string Sex { get; init; } = "";
    public string Generation { get; init; } = "";

    public string Father { get; init; } = "Unknown";
    public string Mother { get; init; } = "Unknown";
    public string Siblings { get; init; } = "None";

    public string Spouse { get; init; } = "None";
    public string Children { get; init; } = "None";

    public string RelationshipHistory { get; init; } = "None";

    public IReadOnlyList<RelationshipPersonLineViewModel> RelationshipPeople { get; init; } = [];
    public IReadOnlyList<RelationshipHistoryLineViewModel> RelationshipHistoryItems { get; init; } = [];
    public string RelationshipHistoryEmptyText => RelationshipHistoryItems.Count == 0 ? "None" : string.Empty;
    public bool ShowRelationshipHistoryEmptyText => RelationshipHistoryItems.Count == 0;

    public bool ShowAdultRelationships { get; init; }

    public string Bloodline { get; init; } = "";
    public string MaleLineage { get; init; } = "";
}
