namespace Dynastia.App.ViewModels;

public sealed class ActionFilterViewModel : ViewModelBase
{
    private bool _isActive;

    public ActionFilterViewModel(
        ActionCategory category,
        bool isActive,
        Action<ActionCategory> toggle)
    {
        Category = category;
        Label = category.ToString();
        _isActive = isActive;
        ToggleCommand = new RelayCommand(
            () => toggle(Category));
    }

    public ActionCategory Category { get; }

    public string Label { get; }

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (_isActive == value)
                return;

            _isActive = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand ToggleCommand { get; }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
