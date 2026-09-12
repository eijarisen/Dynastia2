namespace Dynastia.App.ViewModels;

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

    public bool ShowAdultRelationships { get; init; }

    public string Bloodline { get; init; } = "";
    public string MaleLineage { get; init; } = "";
}
