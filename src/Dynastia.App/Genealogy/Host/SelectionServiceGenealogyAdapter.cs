namespace Dynastia.App.Genealogy.Host;

using Dynastia.Contracts;
using Dynastia.StandardUI.Genealogy.Contracts;

public sealed class SelectionServiceGenealogyAdapter :
    IGlobalSelectionService
{
    private readonly ISelectionService
        _selection;

    public SelectionServiceGenealogyAdapter(
        ISelectionService selection)
    {
        _selection =
            selection;

        _selection.SelectionChanged +=
            OnSelectionChanged;
    }

    public Guid? SelectedPersonId =>
        _selection.SelectedPersonId;

    public event EventHandler?
        SelectedPersonChanged;

    public void SelectPerson(
        Guid personId)
    {
        _selection.SelectedPersonId =
            personId;
    }

    private void OnSelectionChanged(
        object? sender,
        EventArgs e)
    {
        SelectedPersonChanged?.Invoke(
            this,
            EventArgs.Empty);
    }
}
