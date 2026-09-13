using System.Collections.ObjectModel;
using Avalonia.Threading;
using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RefreshAlbum()
    {
        AlbumEvents.Clear();

        foreach (var gameEvent in
            GetVisibleChronicleEvents(
                AlbumYear))
        {
            AlbumEvents.Add(
                new AlbumEventViewModel(
                    gameEvent));
        }

        OnPropertyChanged(
            nameof(AlbumEmptyText));
    }

    private IReadOnlyList<GameEvent>
        GetVisibleChronicleEvents(
            int eventYear)
    {
        return _eventBus
            .GetEventsForYear(eventYear)
            .Where(gameEvent =>
                _householdService?
                    .ShouldShowFamilyNews(
                        gameEvent)
                ?? true)
            .OrderBy(
                ChronicleEventOrdering.GetSeverity)
            .ToList();
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (gameEvent.Year
            == AlbumYear)
        {
            RefreshAlbum();
        }

        RefreshNarrative();
    }

    private void OnSelectionChanged(
        object? sender,
        EventArgs e)
    {
        var selectedId =
            _selectionService
                .SelectedPersonId;

        var row =
            selectedId is Guid id
                ? People.FirstOrDefault(
                    person =>
                        person.Id == id)
                : null;

        if (ReferenceEquals(
            row,
            SelectedPerson))
        {
            return;
        }

        SelectedPerson =
            row;
    }

    private void OnSuccessionStateChanged(
        object? sender,
        EventArgs e)
    {
        if (_succession.IsGameOver)
        {
            IsGameOverOverlayVisible =
                true;
        }

        NextYearCommand
            .RaiseCanExecuteChanged();

        OnPropertyChanged(
            nameof(IsGameOver));

        OnPropertyChanged(
            nameof(GameOverText));

        OnPropertyChanged(
            nameof(ActiveHouseholdText));

        OnPropertyChanged(
            nameof(SelectedPersonEmoji));

        RefreshFamilySection();
    }

    private void NotifyGameStateChanged()
    {
        OnPropertyChanged(
            nameof(Year));

        OnPropertyChanged(
            nameof(DynastyTitle));

        OnPropertyChanged(
            nameof(IsGameOver));

        OnPropertyChanged(
            nameof(GameOverText));

        OnPropertyChanged(
            nameof(ActiveHouseholdText));

        OnPropertyChanged(
            nameof(SelectedPersonEmoji));

        PreviousAlbumYearCommand
            .RaiseCanExecuteChanged();

        NextAlbumYearCommand
            .RaiseCanExecuteChanged();

        NextYearCommand
            .RaiseCanExecuteChanged();
    }

}
