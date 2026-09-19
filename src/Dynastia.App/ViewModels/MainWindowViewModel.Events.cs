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
        PreserveChronicleHousehold(
            gameEvent);

        if (gameEvent.Year
            == AlbumYear)
        {
            RefreshAlbum();
        }

        RefreshNarrative();
    }

    private void PreserveChronicleHousehold(
        GameEvent gameEvent)
    {
        if (gameEvent.Data.ContainsKey(
                "chronicleHouseholdId")
            || gameEvent.Data
                is not IDictionary<string, string> data)
        {
            return;
        }

        var personIds =
            new List<Guid>();

        if (gameEvent.SubjectId is Guid subjectId)
            personIds.Add(subjectId);

        personIds.AddRange(
            gameEvent.RelatedPersonIds);

        foreach (var personId in personIds.Distinct())
        {
            var person =
                _gameState.People.FirstOrDefault(
                    candidate =>
                        candidate.Id == personId);

            if (person is null)
                continue;

            var household =
                _householdService?.GetHouseholdInfo(
                    person);

            if (household is null)
                continue;

            data["chronicleHouseholdId"] =
                household.HouseholdId.ToString();

            data["chronicleHouseholdHeadId"] =
                household.HeadId.ToString();

            data["chronicleHouseholdAnchorId"] =
                household.DynastyAnchorId.ToString();

            return;
        }
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
            nameof(GameOverTitle));

        OnPropertyChanged(
            nameof(ActiveHouseholdText));

        OnPropertyChanged(
            nameof(HasFamilyRelations));

        OnPropertyChanged(
            nameof(SelectedPersonPortrait));

        OnPropertyChanged(
            nameof(IsSelectedPersonDeceased));

        OnPropertyChanged(
            nameof(SelectedHobbiesText));

        RefreshFamilySection();
    }

    private void NotifyGameStateChanged()
    {
        OnPropertyChanged(
            nameof(Year));

        OnPropertyChanged(
            nameof(HistoricalEraName));

        OnPropertyChanged(
            nameof(DynastyTitle));

        OnPropertyChanged(
            nameof(IsGameOver));

        OnPropertyChanged(
            nameof(GameOverText));

        OnPropertyChanged(
            nameof(GameOverTitle));

        OnPropertyChanged(
            nameof(ActiveHouseholdText));

        OnPropertyChanged(
            nameof(HasFamilyRelations));

        OnPropertyChanged(
            nameof(SelectedPersonPortrait));

        OnPropertyChanged(
            nameof(IsSelectedPersonDeceased));

        OnPropertyChanged(
            nameof(SelectedHobbiesText));

        PreviousAlbumYearCommand
            .RaiseCanExecuteChanged();

        NextAlbumYearCommand
            .RaiseCanExecuteChanged();

        NextYearCommand
            .RaiseCanExecuteChanged();
    }

}
