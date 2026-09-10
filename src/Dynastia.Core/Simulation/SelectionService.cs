using Dynastia.Contracts;

namespace Dynastia.Core.Simulation;

public sealed class SelectionService : ISelectionService
{
    private Guid? _selectedPersonId;

    public Guid? SelectedPersonId
    {
        get => _selectedPersonId;
        set
        {
            if (_selectedPersonId == value)
                return;

            _selectedPersonId = value;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectionChanged;
}
