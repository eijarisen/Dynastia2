using System.Collections.ObjectModel;
using Avalonia.Threading;
using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public string GetSuggestedSaveFileName()
    {
        return _saveService
            .GetSuggestedFileName();
    }

    public void SaveGame(
        Stream stream)
    {
        if (!IsGameStarted)
        {
            throw new InvalidOperationException(
                "There is no active dynasty to save.");
        }

        _saveService.Save(
            stream,
            CaptureUiSaveState());
    }

    public void LoadGame(
        Stream stream)
    {
        var loadedUiState =
            _saveService.Load(
                stream,
                CaptureUiSaveState());

        SurnameInput =
            _gameState.DynastySurname;

        IsLivingFamilyView =
            loadedUiState.IsLivingFamilyView;

        DetailsTabIndex =
            loadedUiState.DetailsTabIndex;

        IsMainMenuPromptVisible =
            false;

        IsYearSummaryVisible =
            false;

        IsGameStarted =
            true;

        AlbumYear =
            Math.Clamp(
                loadedUiState.AlbumYear,
                GameCalendarConfiguration.GameStartYear,
                _gameState.Year);

        IsGameOverOverlayVisible =
            _succession.IsGameOver;

        _personalityService?
            .ReconcileAll();

        _householdService?
            .ReconcileHouseholds();

        _thoughtService?
            .ResetAfterLoad();

        RefreshPeople();
        RefreshAlbum();

        NotifyGameStateChanged();
    }

    public void ReportPersistenceStatus(
        string message)
    {
        PersistenceStatusText =
            message;
    }

    private GameUiSaveState CaptureUiSaveState()
    {
        return new GameUiSaveState(
            _selectionService.SelectedPersonId,
            _succession.ActiveControllerId,
            AlbumYear,
            IsLivingFamilyView,
            DetailsTabIndex);
    }

}
