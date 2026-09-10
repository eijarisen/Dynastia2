namespace Dynastia.StandardUI.Genealogy.Contracts;

using Dynastia.StandardUI.Genealogy.Models;

/// <summary>
/// Read-only UI projection adapter over the authoritative Family mechanic.
/// </summary>
public interface IGenealogyDataSource
{
    GenealogySnapshot GetSnapshot();

    event EventHandler? TopologyChanged;

    event EventHandler? VisualStateChanged;
}

public interface IGlobalSelectionService
{
    Guid? SelectedPersonId { get; }

    event EventHandler? SelectedPersonChanged;

    void SelectPerson(
        Guid personId);
}
