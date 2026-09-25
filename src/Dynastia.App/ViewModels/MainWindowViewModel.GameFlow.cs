using System.Collections.ObjectModel;
using Avalonia.Threading;
using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private static readonly Guid GlobalNewsChronicleGroupId =
        Guid.Parse("7f3bc660-359a-45dc-91d7-8e3e9af3427e");

    private void StartGame()
    {
        PersistenceStatusText =
            string.Empty;

        _newGameService.StartNewGame(
            SurnameInput,
            (int)SelectedStartYear);

        _succession.Refresh();

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

    public void DebugSimulateNextYear()
    {
        if (!IsGameStarted
            || _succession.IsGameOver
            || IsMainMenuPromptVisible)
        {
            return;
        }

        if (_autonomousHouseholdDecisionService is null)
        {
            ReportPersistenceStatus(
                "Autonomous household decisions are unavailable.");
            return;
        }

        IsYearSummaryVisible =
            false;

        PersistenceStatusText =
            string.Empty;

        _reconciliation.Reconcile(
            ReconciliationLifecycleStage.BeforeYear);

        _autonomousHouseholdDecisionService
            .QueueActionsForAllHouseholds();

        AdvanceYear();
    }

    private void AdvanceYear()
    {
        if (!IsGameStarted
            || _succession.IsGameOver)
        {
            return;
        }

        var profileEnabled =
            YearPerformanceProfiler.IsEnabled;

        var commandStartedAt =
            profileEnabled
                ? YearPerformanceProfiler.StartTimestamp()
                : 0;

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

        var peopleAtTurnStart =
            profileEnabled
                ? _gameState.People.Count
                : 0;

        var queuedAtTurnStart =
            profileEnabled
                ? _actionRegistry.GetAllQueuedActions().Count
                : 0;

        PersistenceStatusText =
            string.Empty;

        _yearProcessor.AdvanceYear();

        var refreshStartedAt =
            profileEnabled
                ? YearPerformanceProfiler.StartTimestamp()
                : 0;

        AlbumYear =
            _gameState.Year;

        RefreshPeople();

        NotifyGameStateChanged();

        ShowYearSummary(
            _gameState.Year);

        if (profileEnabled)
        {
            var refreshElapsed =
                YearPerformanceProfiler.GetElapsedTime(
                    refreshStartedAt);

            var commandElapsed =
                YearPerformanceProfiler.GetElapsedTime(
                    commandStartedAt);

            YearPerformanceProfiler.LogDuration(
                _gameState.Year,
                "app.post_year_refresh",
                refreshElapsed);

            YearPerformanceProfiler.LogDuration(
                _gameState.Year,
                "app.next_year_total",
                commandElapsed);

            LogYearProfileCounters(
                peopleAtTurnStart,
                queuedAtTurnStart);
        }
    }

    private void LogYearProfileCounters(
        int peopleAtTurnStart,
        int queuedAtTurnStart)
    {
        var totalPeople =
            _gameState.People.Count;

        var livingPeople =
            _gameState.People.Count(
                person =>
                    !person.Tags.Has(
                        "state.dead"));

        var activePeople =
            _gameState.People.Count(
                person =>
                    !person.Tags.Has(
                        "state.dead")
                    && !SimulationState.IsInactive(
                        person));

        var householdCount =
            _householdService?
                .GetActiveHouseholds()
                .Count
            ?? 0;

        var peopleCreated =
            Math.Max(
                0,
                totalPeople - peopleAtTurnStart);

        YearPerformanceProfiler.Log(
            _gameState.Year,
            "year.counters",
            $"people={totalPeople} " +
            $"living={livingPeople} " +
            $"active={activePeople} " +
            $"households={householdCount} " +
            $"events={_eventBus.AllEvents.Count} " +
            $"queued_turn_start={queuedAtTurnStart} " +
            $"created={peopleCreated}");
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
                        household.ClassOrder,
                        household.Generation,
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
                    ClassOrder = group.First().ClassOrder,
                    Generation = group.First().Generation,
                    Events = group
                        .OrderBy(item => item.Severity)
                        .Select(item => item.Event)
                        .ToList()
                })
                .OrderBy(group => group.ClassOrder)
                .ThenBy(group => group.Generation)
                .ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

        foreach (var group in grouped)
        {
            YearSummaryHouseholds.Add(
                new YearSummaryHouseholdViewModel(
                    group.Generation == int.MaxValue
                        ? group.Title
                        : $"Generation {group.Generation} — {group.Title}",
                    group.Events.Select(
                        gameEvent =>
                            new AlbumEventViewModel(
                                gameEvent,
                                _eventPresentation,
                                _gameScoreService?.GetEventDelta(gameEvent) ?? 0))));
        }

        OnPropertyChanged(
            nameof(YearSummaryTitle));

        OnPropertyChanged(
            nameof(YearSummaryEmptyText));
        OnPropertyChanged(nameof(YearSummaryScoreText));

        IsYearSummaryVisible =
            true;

        PreviousYearSummaryCommand.RaiseCanExecuteChanged();
        NextYearSummaryCommand.RaiseCanExecuteChanged();
    }

    private void PreviousYearSummary()
    {
        if (_yearSummaryEventYear <= _gameState.StartYear)
            return;

        ShowYearSummary(_yearSummaryEventYear - 1);
    }

    private void NextYearSummary()
    {
        if (_yearSummaryEventYear >= Year)
            return;

        ShowYearSummary(_yearSummaryEventYear + 1);
    }

    private void HideYearSummary()
    {
        IsYearSummaryVisible =
            false;

        PreviousYearSummaryCommand.RaiseCanExecuteChanged();
        NextYearSummaryCommand.RaiseCanExecuteChanged();

        if (_succession.IsGameOver)
        {
            IsGameOverOverlayVisible =
                true;
        }
    }

    private (Guid Key, string Title, int ClassOrder, int Generation)
        ResolveChronicleHousehold(
            GameEvent gameEvent)
    {
        if (IsGlobalNewsEvent(gameEvent))
        {
            return (
                GlobalNewsChronicleGroupId,
                "Global News",
                -1,
                int.MaxValue);
        }

        if (gameEvent.Data.TryGetValue(
                "chronicleHouseholdId",
                out var storedHouseholdIdText)
            && Guid.TryParse(
                storedHouseholdIdText,
                out var storedHouseholdId)
            && gameEvent.Data.TryGetValue(
                "chronicleHouseholdHeadId",
                out var storedHeadIdText)
            && Guid.TryParse(
                storedHeadIdText,
                out var storedHeadId))
        {
            var storedHead =
                _gameState.People.FirstOrDefault(
                    person =>
                        person.Id == storedHeadId);

            if (storedHead is not null)
            {
                IPerson? storedAnchor = null;

                if (gameEvent.Data.TryGetValue(
                        "chronicleHouseholdAnchorId",
                        out var storedAnchorIdText)
                    && Guid.TryParse(
                        storedAnchorIdText,
                        out var storedAnchorId))
                {
                    storedAnchor =
                        _gameState.People.FirstOrDefault(
                            person =>
                                person.Id == storedAnchorId);
                }

                storedAnchor ??=
                    storedHead;

                return (
                    storedHouseholdId,
                    $"{_familyService?.GetDisplayName(storedHead) ?? storedHead.Name}'s household",
                    _familyService?.IsMaleLineage(storedAnchor) == true
                        ? 0
                        : 1,
                    _familyService?.GetGeneration(storedAnchor)
                        ?? int.MaxValue);
            }
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

            return (
                household.HouseholdId,
                $"{household.HeadName}'s household",
                household.Class == HouseholdClass.Lineage
                    ? 0
                    : 1,
                household.Generation
                    ?? int.MaxValue);
        }

        return (
            Guid.Empty,
            "Extended family",
            2,
            int.MaxValue);
    }

    private static bool IsGlobalNewsEvent(GameEvent gameEvent)
    {
        if (gameEvent.Type.StartsWith(
                "historical.",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return gameEvent.Data.TryGetValue(
                "globalNews",
                out var globalNews)
            && globalNews.Equals(
                "true",
                StringComparison.OrdinalIgnoreCase);
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
            > _gameState.StartYear)
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
