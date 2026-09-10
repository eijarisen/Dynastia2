using System.Collections.ObjectModel;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IGameState _gameState;
    private readonly YearProcessor _yearProcessor;

    public MainWindowViewModel(
        IGameState gameState,
        YearProcessor yearProcessor)
    {
        _gameState = gameState;
        _yearProcessor = yearProcessor;

        NextYearCommand = new RelayCommand(AdvanceYear);

        RefreshPeople();
    }

    public string DynastyTitle =>
        $"The {_gameState.DynastySurname} Dynasty";

    public int Year =>
        _gameState.Year;

    public ObservableCollection<PersonRowViewModel> People { get; } = [];

    public RelayCommand NextYearCommand { get; }

    private void AdvanceYear()
    {
        _yearProcessor.AdvanceYear();

        RefreshPeople();

        OnPropertyChanged(nameof(Year));
        OnPropertyChanged(nameof(DynastyTitle));
    }

    private void RefreshPeople()
    {
        People.Clear();

        foreach (var person in _gameState.People)
        {
            People.Add(new PersonRowViewModel(person));
        }
    }
}
