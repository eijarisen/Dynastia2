namespace Dynastia.Contracts;

public interface ISelectionService
{
    Guid? SelectedPersonId { get; set; }

    event EventHandler? SelectionChanged;
}
