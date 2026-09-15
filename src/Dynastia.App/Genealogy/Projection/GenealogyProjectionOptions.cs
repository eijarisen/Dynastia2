namespace Dynastia.StandardUI.Genealogy.Projection;

public sealed record GenealogyProjectionOptions(
    bool ShowAllSpouses = false,
    bool IncludeDaughtersFamilies = true,
    bool MaleLineageOnly = false,
    IReadOnlySet<Guid>? CollapsedPersonIds = null)
{
    public bool IsCollapsed(
        Guid personId) =>
        CollapsedPersonIds?.Contains(
            personId) == true;
}
