using System.Collections.ObjectModel;
using Avalonia.Threading;
using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void StartGame()
    {
        PersistenceStatusText =
            string.Empty;

        _newGameService.StartNewGame(
            SurnameInput);

        _succession.Refresh();

        _thoughtService?
            .EnsureCurrentThoughts();

        IsLivingFamilyView = true;

        IsGameOverOverlayVisible =
            false;

        IsMainMenuPromptVisible =
            false;

        IsYearSummaryVisible =
            false;

        IsGameStarted = true;
        AlbumYear = _gameState.Year;

        ResetActionCategoryFilters();

        RefreshPeople();
        RefreshAlbum();

        NotifyGameStateChanged();
    }

    private void AdvanceYear()
    {
        if (!IsGameStarted
            || _succession.IsGameOver)
        {
            return;
        }

        var missing =
            GetMaleHeirsWithoutAnnualAction();

        if (missing.Count > 0)
        {
            var first =
                missing[0];

            _succession.SetActiveController(
                first);

            var row =
                People.FirstOrDefault(
                    person =>
                        person.Id
                        == first.Id);

            if (row is not null)
            {
                SelectedPerson =
                    row;
            }

            ReportPersistenceStatus(
                "Choose an annual action for every adult male heir before advancing the year.");

            RefreshFamilySection();
            RefreshActions();

            return;
        }

        PersistenceStatusText =
            string.Empty;

        _yearProcessor.AdvanceYear();

        AlbumYear =
            _gameState.Year;

        RefreshPeople();

        NotifyGameStateChanged();

        ShowYearSummary(
            _gameState.Year);
    }

    private IReadOnlyList<IPerson>
        GetMaleHeirsWithoutAnnualAction()
    {
        var queuedActorIds =
            _actionRegistry
                .GetAllQueuedActions()
                .Select(
                    queued =>
                        queued.ActorId)
                .ToHashSet();

        return _gameState.People
            .Where(
                person =>
                    _succession.IsControllable(
                        person)
                    && !queuedActorIds.Contains(
                        person.Id))
            .OrderBy(
                person =>
                    _familyService?
                        .GetGeneration(
                            person)
                    ?? int.MaxValue)
            .ThenBy(
                GetBirthSortYear)
            .ThenBy(
                person =>
                    person.Id)
            .ToList();
    }

    private void CancelQueuedAction()
    {
        var actor =
            _succession.ActiveController;

        if (actor is null)
            return;

        _actionRegistry
            .CancelQueuedActions(
                actor);

        RefreshActions();
        RefreshFamilySection();
    }

    private void ShowAlbumYearSummary()
    {
        if (!IsGameStarted)
            return;

        ShowYearSummary(
            AlbumYear);
    }

    private void ShowYearSummary(
        int eventYear)
    {
        IsGameOverOverlayVisible =
            false;

        _yearSummaryEventYear =
            eventYear;

        YearSummaryHouseholds.Clear();

        var grouped =
            GetVisibleChronicleEvents(eventYear)
                .Select(gameEvent =>
                {
                    var household =
                        ResolveChronicleHousehold(
                            gameEvent);

                    return new
                    {
                        Event = gameEvent,
                        household.Key,
                        household.Title,
                        Severity =
                            ChronicleEventOrdering.GetSeverity(
                                gameEvent)
                    };
                })
                .GroupBy(item => item.Key)
                .Select(group => new
                {
                    Key = group.Key,
                    Title = group.First().Title,
                    Severity = group.Min(item => item.Severity),
                    Events = group
                        .OrderBy(item => item.Severity)
                        .Select(item => item.Event)
                        .ToList()
                })
                .OrderBy(group => group.Severity)
                .ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

        foreach (var group in grouped)
        {
            YearSummaryHouseholds.Add(
                new YearSummaryHouseholdViewModel(
                    group.Title,
                    group.Events.Select(
                        gameEvent =>
                            new AlbumEventViewModel(
                                gameEvent))));
        }

        OnPropertyChanged(
            nameof(YearSummaryTitle));

        OnPropertyChanged(
            nameof(YearSummaryEmptyText));

        IsYearSummaryVisible =
            true;
    }

    private void HideYearSummary()
    {
        IsYearSummaryVisible =
            false;

        if (_succession.IsGameOver)
        {
            IsGameOverOverlayVisible =
                true;
        }
    }

    private (Guid Key, string Title) ResolveChronicleHousehold(
        GameEvent gameEvent)
    {
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

            return (
                household.HouseholdId,
                $"{household.HeadName}'s household");
        }

        return (
            Guid.Empty,
            "Extended family");
    }

    private void HideGameOver()
    {
        IsGameOverOverlayVisible =
            false;
    }

    private void ShowMainMenuPrompt()
    {
        if (!IsGameStarted)
        {
            return;
        }

        IsMainMenuPromptVisible =
            true;
    }

    private void HideMainMenuPrompt()
    {
        IsMainMenuPromptVisible =
            false;
    }

    private void ReturnToMainMenu()
    {
        IsGameOverOverlayVisible =
            false;

        IsMainMenuPromptVisible =
            false;

        IsYearSummaryVisible =
            false;

        IsGameStarted =
            false;

        PersistenceStatusText =
            string.Empty;

        OnPropertyChanged(
            nameof(DynastyTitle));
    }

    private void ShowLivingFamily()
    {
        SetHouseholdViewMode(
            HouseholdViewMode.Lineage);

        var active =
            _succession.ActiveController;

        if (active is not null)
        {
            var row =
                People.FirstOrDefault(
                    person =>
                        person.Id
                        == active.Id);

            if (row is not null)
            {
                SelectedPerson =
                    row;
            }
        }

        RefreshFamilySection();
        RefreshActions();
    }

    private void ShowBloodlineFamily()
    {
        SetHouseholdViewMode(
            HouseholdViewMode.Bloodline);

        var first =
            _householdService?
                .GetActiveHouseholds()
                .FirstOrDefault(
                    household =>
                        household.Class
                        == HouseholdClass.Bloodline);

        _inspectedBloodlineHouseholdId ??=
            first?.HouseholdId;

        if (_inspectedBloodlineHouseholdId
            is Guid inspectedId)
        {
            var inspected =
                _householdService?
                    .GetActiveHouseholds()
                    .FirstOrDefault(
                        household =>
                            household.HouseholdId
                                == inspectedId
                            && household.Class
                                == HouseholdClass.Bloodline);

            var row =
                inspected is null
                    ? null
                    : People.FirstOrDefault(
                        person =>
                            person.Id
                            == inspected.HeadId);

            if (row is not null)
            {
                SelectedPerson =
                    row;
            }
        }

        RefreshFamilySection();
        RefreshActions();
    }

    private void ShowDeceasedFamily()
    {
        SetHouseholdViewMode(
            HouseholdViewMode.Deceased);

        RefreshFamilySection();
        RefreshActions();
    }

    private void SetHouseholdViewMode(
        HouseholdViewMode mode)
    {
        if (_householdViewMode
            == mode)
        {
            return;
        }

        _householdViewMode =
            mode;

        OnPropertyChanged(
            nameof(IsLivingFamilyView));

        OnPropertyChanged(
            nameof(IsLineageFamilyView));

        OnPropertyChanged(
            nameof(IsBloodlineFamilyView));

        OnPropertyChanged(
            nameof(IsDeceasedFamilyView));

        OnPropertyChanged(
            nameof(HasFamilyRelations));

        OnPropertyChanged(
            nameof(ActionsEmptyText));
    }

    private void PreviousAlbumYear()
    {
        if (AlbumYear
            > GameCalendarConfiguration.GameStartYear + 1)
        {
            AlbumYear--;
        }
    }

    private void NextAlbumYear()
    {
        if (AlbumYear < Year)
            AlbumYear++;
    }

}
