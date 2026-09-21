using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Dynastia.App.Audio;
using Dynastia.App.Genealogy.Host;
using Dynastia.App.Map.Host;
using Dynastia.App.ViewModels;
using Dynastia.Contracts;
using Dynastia.StandardUI.Genealogy.Contracts;
using Dynastia.StandardUI.Genealogy.Views;
using Dynastia.StandardUI.Map.Views;

namespace Dynastia.App.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType
        DynastiaSaveFileType =
            new("Dynastia Save")
            {
                Patterns =
                    ["*.txt"]
            };

    private bool _persistenceDialogOpen;
    private bool _genealogyDialogOpen;
    private bool _mapDialogOpen;
    private bool _townLifeDialogOpen;
    private bool _familyRelationsDialogOpen;
    private bool _familyInventoryDialogOpen;
    private bool _instructionsDialogOpen;
    private bool _actionSelectionDialogOpen;
    private MainWindowViewModel? _subscribedViewModel;

    public GameGenealogyDataSource? GenealogyDataSource
    {
        get;
        set;
    }

    public IGlobalSelectionService? GenealogySelection
    {
        get;
        set;
    }

    public GameMapDataSource? MapDataSource
    {
        get;
        set;
    }

    public ITownLifeService? TownLifeService
    {
        get;
        set;
    }

    public BackgroundMusicService? MusicService
    {
        get;
        set;
    }

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        AddHandler(
            InputElement.KeyDownEvent,
            OnWindowKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        AddHandler(
            InputElement.PointerMovedEvent,
            OnWindowPointerMoved,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        AddHandler(
            InputElement.PointerExitedEvent,
            OnWindowPointerExited,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }


    private void OnWindowPointerMoved(
        object? sender,
        PointerEventArgs e)
    {
        if (!MainMenuParallax.IsVisible)
            return;

        MainMenuParallax.SetPointerPosition(
            e.GetPosition(MainMenuParallax));
    }

    private void OnWindowPointerExited(
        object? sender,
        PointerEventArgs e)
    {
        MainMenuParallax.ResetPointer();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ActionSelectionRequested -=
                OnActionSelectionRequested;
        }

        _subscribedViewModel =
            DataContext as MainWindowViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ActionSelectionRequested +=
                OnActionSelectionRequested;
        }
    }

    private async void OnActionSelectionRequested(
        object? sender,
        ActionSelectionRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var townAffairsRequest =
            viewModel.CreateTownAffairsRequest(e.ActionId);

        if (townAffairsRequest is not null)
        {
            await OpenTownAffairsAsync(townAffairsRequest);
            return;
        }

        if (e.ActionId.Equals(
                "ui.manage_properties",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!_actionSelectionDialogOpen)
                await OpenFamilyInventoryAsync(
                    viewModel,
                    FamilyInventoryTab.Properties);

            return;
        }

        if (e.ActionId.Equals(
                "ui.manage_finances",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!_actionSelectionDialogOpen)
                await OpenFamilyInventoryAsync(
                    viewModel,
                    FamilyInventoryTab.Money);

            return;
        }

        if (_actionSelectionDialogOpen)
            return;

        _actionSelectionDialogOpen = true;
        SetPaperDialogBackdrop(true);

        try
        {
            if (e.ActionId.Equals(
                    "ui.self_improvement",
                    StringComparison.OrdinalIgnoreCase))
            {
                var selfImprovementOptions =
                    viewModel.GetSelfImprovementOptions();

                if (!selfImprovementOptions.Any(option => option.IsAvailable))
                {
                    viewModel.ReportPersistenceStatus(
                        "No self-improvement option is currently available.");
                    return;
                }

                var selfImprovementWindow =
                    new SelfImprovementWindow(
                        viewModel.GetSelfImprovementTargetName(),
                        selfImprovementOptions);

                var selectedActionId =
                    await selfImprovementWindow
                        .ShowDialog<string?>(this);

                if (!string.IsNullOrWhiteSpace(selectedActionId))
                {
                    viewModel.QueueSelfImprovementAction(
                        selectedActionId);
                }

                return;
            }

            if (e.ActionId.Equals(
                    "ui.craft_profession",
                    StringComparison.OrdinalIgnoreCase))
            {
                var craftOptions = viewModel.GetCraftProfessionOptions();
                if (craftOptions.Count == 0)
                {
                    viewModel.ReportPersistenceStatus(
                        "No known Craft is currently available for self-employment.");
                    return;
                }

                if (craftOptions.Count == 1)
                {
                    viewModel.QueueCraftProfessionAction(
                        craftOptions[0].Id);
                    return;
                }

                var craftWindow = new PropertySelectionWindow(
                    "Work in a Profession",
                    "Work",
                    craftOptions);
                var selectedActionId = await craftWindow.ShowDialog<string?>(this);
                if (!string.IsNullOrWhiteSpace(selectedActionId))
                    viewModel.QueueCraftProfessionAction(selectedActionId);
                return;
            }



            if (e.ActionId.Equals(
                    "relationship.find_spouse",
                    StringComparison.OrdinalIgnoreCase)
                || e.ActionId.Equals(
                    "relationship.marry_off_daughter",
                    StringComparison.OrdinalIgnoreCase)
                || e.ActionId.Equals(
                    "relationship.marry_off_son",
                    StringComparison.OrdinalIgnoreCase))
            {
                var model =
                    viewModel.GetPotentialPartnerDialog(
                        e.ActionId);

                if (model is null
                    || model.Candidates.Count == 0)
                {
                    viewModel.ReportPersistenceStatus(
                        "No suitable potential partners are currently available.");
                    return;
                }

                var potentialPartnersWindow =
                    new PotentialPartnersWindow(model);

                var selection =
                    await potentialPartnersWindow.ShowDialog<PartnerCandidateInfo?>(this);

                if (selection is not null)
                {
                    viewModel.QueueCourtship(
                        e.ActionId,
                        selection);
                }

                return;
            }

            if (e.ActionId.Equals(
                    "loan.take",
                    StringComparison.OrdinalIgnoreCase)
                || e.ActionId.Equals(
                    "loan.give",
                    StringComparison.OrdinalIgnoreCase))
            {
                var isGivingLoan =
                    e.ActionId.Equals(
                        "loan.give",
                        StringComparison.OrdinalIgnoreCase);

                var maximumPrincipal =
                    viewModel.GetMaximumLoanPrincipal(e.ActionId);

                var offers =
                    viewModel.GetLoanOffers(
                        isGivingLoan,
                        maximumPrincipal);

                if (offers.Count == 0)
                    return;

                var loanWindow =
                    new LoanSelectionWindow(
                        isGivingLoan,
                        offers);

                var selection =
                    await loanWindow.ShowDialog<LoanSelectionResult?>(this);

                if (selection is not null)
                {
                    viewModel.QueueLoanAction(
                        e.ActionId,
                        selection);
                }

                return;
            }

            var options =
                viewModel.GetPropertySelectionOptions(
                    e.ActionId);

            if (options.Count == 0)
            {
                viewModel.ReportPersistenceStatus(
                    "No valid property options are currently available.");
                return;
            }

            var isBuy = e.ActionId.Equals(
                "household.buy_house",
                StringComparison.OrdinalIgnoreCase);
            var isMoveOut = e.ActionId.Equals(
                "household.ask_move_out",
                StringComparison.OrdinalIgnoreCase);

            var window = new PropertySelectionWindow(
                isBuy ? "Select Town" : "Select Property",
                isBuy ? "View Housing" : isMoveOut ? "Provide House" : "Sell",
                options,
                compact: isBuy);

            var selectedId =
                await window.ShowDialog<string?>(this);

            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                if (isBuy)
                {
                    await OpenTownAffairsAsync(
                        viewModel.CreateHousingTownAffairsRequest(selectedId));
                }
                else
                {
                    viewModel.QueueActionWithSelection(
                        e.ActionId,
                        selectedId);
                }
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            viewModel.ReportPersistenceStatus(
                $"Action selection failed: {exception.Message}");
        }
        finally
        {
            SetPaperDialogBackdrop(false);
            _actionSelectionDialogOpen = false;
        }
    }

    private void OnRelationshipPersonClick(
        object? sender,
        RoutedEventArgs e)
    {
        Guid? personId = sender switch
        {
            Button { DataContext: RelationshipPersonLineViewModel line } =>
                line.PersonId,
            Button { DataContext: RelationshipHistoryLineViewModel history } =>
                history.PersonId,
            _ => null
        };

        if (personId is not Guid id
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SelectHouseholdFromTree(id);
        e.Handled = true;
    }

    private async void OnWindowKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (_persistenceDialogOpen
            || _genealogyDialogOpen
            || _mapDialogOpen
            || _townLifeDialogOpen
            || _familyRelationsDialogOpen
            || _familyInventoryDialogOpen
            || _instructionsDialogOpen
            || _actionSelectionDialogOpen)
        {
            return;
        }

        if (DataContext
            is not MainWindowViewModel viewModel)
        {
            return;
        }

#if DEBUG
        var debugModifiers =
            KeyModifiers.Control
            | KeyModifiers.Shift;

        if (e.Key == Key.Oem3
            && (e.KeyModifiers & debugModifiers)
                == debugModifiers)
        {
            e.Handled = true;
            viewModel.DebugSimulateNextYear();
            return;
        }
#endif

        if (viewModel.IsMainMenuPromptVisible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                viewModel.HideMainMenuPromptCommand.Execute(null);
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                viewModel.ReturnToMainMenuCommand.Execute(null);
            }

            return;
        }

        if (viewModel.IsYearSummaryVisible
            && (e.Key == Key.Enter
                || e.Key == Key.Escape))
        {
            e.Handled = true;
            viewModel.HideYearSummaryCommand.Execute(null);
            return;
        }

        if (viewModel.IsStatusMessageVisible
            && (e.Key == Key.Enter
                || e.Key == Key.Escape))
        {
            e.Handled = true;
            viewModel.HideStatusMessageCommand.Execute(null);
            return;
        }

        if (viewModel.IsGameStarted
            && e.Key == Key.Escape)
        {
            e.Handled = true;

            if (viewModel.HasQueuedAction)
            {
                viewModel.CancelQueuedActionCommand.Execute(null);
            }
            else if (viewModel.ShowMainMenuPromptCommand.CanExecute(null))
            {
                viewModel.ShowMainMenuPromptCommand.Execute(null);
            }

            return;
        }

        if (viewModel.IsGameStarted
            && e.Key == Key.Tab)
        {
            e.Handled = true;

            if (viewModel.IsStatusMessageVisible)
                viewModel.HideStatusMessageCommand.Execute(null);

            viewModel.CyclePlayableHousehold(
                (e.KeyModifiers & KeyModifiers.Shift) != 0);
            return;
        }

        if (viewModel.IsGameStarted
            && (e.KeyModifiers
                & (KeyModifiers.Control
                    | KeyModifiers.Alt
                    | KeyModifiers.Meta)) == 0)
        {
            switch (e.Key)
            {
                case Key.M:
                    e.Handled = true;
                    await OpenMapAsync();
                    return;

                case Key.T:
                    e.Handled = true;
                    await OpenGenealogyAsync();
                    return;

                case Key.R:
                    if (!viewModel.HasFamilyRelations)
                        return;

                    e.Handled = true;
                    await OpenRelationsAsync();
                    return;

                case Key.P:
                    e.Handled = true;
                    viewModel.TryExecuteAvailableActionShortcut(
                        "ui.manage_properties");
                    return;

                case Key.F:
                    e.Handled = true;
                    viewModel.TryExecuteAvailableActionShortcut(
                        "ui.manage_finances");
                    return;

                case Key.E:
                    e.Handled = true;
                    viewModel.TryExecuteAvailableActionShortcut(
                        "education.get_education");
                    return;

                case Key.I:
                    e.Handled = true;
                    viewModel.TryExecuteAvailableActionShortcut(
                        "ui.self_improvement");
                    return;

                case Key.S:
                    e.Handled = true;
                    viewModel.TryExecuteAvailableActionShortcut(
                        "relationship.find_spouse");
                    return;

                case Key.C:
                    if (!viewModel.ShowAlbumYearSummaryCommand.CanExecute(null))
                        return;

                    e.Handled = true;
                    viewModel.ShowAlbumYearSummaryCommand.Execute(null);
                    return;

                case Key.J:
                    e.Handled = true;
                    viewModel.TryExecuteAvailableActionShortcut(
                        "career.seek_employment",
                        "career.find_another_job");
                    return;

                case Key.H:
                    e.Handled = true;
                    viewModel.TryExecuteAvailableActionShortcut(
                        "wellbeing.heal_relative");
                    return;

                case Key.Q:
                    e.Handled = true;
                    viewModel.TryExecuteAvailableActionShortcut(
                        "career.quit_job",
                        "craft.stop_occupation");
                    return;

                case Key.Space:
                    e.Handled = true;
                    viewModel.TryExecuteAvailableActionShortcut(
                        "turn.pass");
                    return;
            }
        }

        if (e.Key != Key.Enter)
            return;

        if (!viewModel.IsGameStarted)
        {
            if (!viewModel.StartGameCommand.CanExecute(null))
                return;

            // Tunnel routing makes Enter in the surname field behave
            // exactly like Start Dynasty and prevents the focused
            // control from also handling the same key press.
            e.Handled = true;

            viewModel.StartGameCommand.Execute(null);
            await ShowInstructionsAsync();
            return;
        }

        if (viewModel.IsStatusMessageVisible
            || !viewModel.NextYearCommand.CanExecute(null))
        {
            return;
        }

        // Tunnel routing prevents a focused Button from also
        // consuming Enter and accidentally advancing twice.
        e.Handled = true;

        viewModel.NextYearCommand.Execute(null);
    }

    private void OnMusicToggleClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (MusicService is null)
            return;

        var muted =
            MusicService.ToggleMuted();

        MusicToggleButton.Content =
            muted
                ? "🔇"
                : "🔊";
    }

    private async void OnSaveGameClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext
            is not MainWindowViewModel viewModel
            || !viewModel.IsGameStarted)
        {
            return;
        }

        _persistenceDialogOpen =
            true;

        try
        {
            var file =
                await StorageProvider
                    .SaveFilePickerAsync(
                        new FilePickerSaveOptions
                        {
                            Title =
                                "Export Dynastia Game",

                            SuggestedFileName =
                                viewModel
                                    .GetSuggestedSaveFileName(),

                            DefaultExtension =
                                "txt",

                            FileTypeChoices =
                                [DynastiaSaveFileType],

                            ShowOverwritePrompt =
                                true
                        });

            if (file is null)
                return;

            await using var stream =
                await file.OpenWriteAsync();

            if (stream.CanSeek)
            {
                stream.SetLength(
                    0);
            }

            viewModel.SaveGame(
                stream);

            viewModel.ReportPersistenceStatus(
                $"Exported {file.Name}.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                exception);

            viewModel.ReportPersistenceStatus(
                $"Export failed: {exception.Message}");
        }
        finally
        {
            _persistenceDialogOpen =
                false;
        }
    }

    private async void OnLoadGameClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext
            is not MainWindowViewModel viewModel)
        {
            return;
        }

        _persistenceDialogOpen =
            true;

        try
        {
            var files =
                await StorageProvider
                    .OpenFilePickerAsync(
                        new FilePickerOpenOptions
                        {
                            Title =
                                "Load Dynastia Game",

                            AllowMultiple =
                                false,

                            FileTypeFilter =
                                [DynastiaSaveFileType]
                        });

            var file =
                files.FirstOrDefault();

            if (file is null)
                return;

            await using var stream =
                await file.OpenReadAsync();

            viewModel.LoadGame(
                stream);

            GenealogyDataSource?
                .NotifyHostReset();

            viewModel.ReportPersistenceStatus(
                $"Loaded {file.Name}.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                exception);

            viewModel.ReportPersistenceStatus(
                $"Load failed: {exception.Message}");
        }
        finally
        {
            _persistenceDialogOpen =
                false;
        }
    }

    private async void OnStartGameClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext
            is not MainWindowViewModel viewModel
            || !viewModel.StartGameCommand.CanExecute(null))
        {
            return;
        }

        viewModel.StartGameCommand.Execute(null);
        await ShowInstructionsAsync();
    }

    private async void OnInstructionsClick(
        object? sender,
        RoutedEventArgs e)
    {
        await ShowInstructionsAsync();
    }

    private async Task ShowInstructionsAsync()
    {
        if (_instructionsDialogOpen)
            return;

        _instructionsDialogOpen =
            true;

        SetPaperDialogBackdrop(true);

        try
        {
            var window =
                new InstructionsWindow();

            await window.ShowDialog(
                this);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                exception);

            if (DataContext
                is MainWindowViewModel viewModel)
            {
                viewModel.ReportPersistenceStatus(
                    $"Instructions failed: {exception.Message}");
            }
        }
        finally
        {
            SetPaperDialogBackdrop(false);
            _instructionsDialogOpen =
                false;
        }
    }

    private void SetPaperDialogBackdrop(
        bool isVisible)
    {
        if (PaperDialogBackdrop is not null)
        {
            PaperDialogBackdrop.IsVisible =
                isVisible;
        }
    }


    private async void OnFamilyInventoryClick(
        object? sender,
        PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || _familyInventoryDialogOpen
            || DataContext is not MainWindowViewModel viewModel
            || !viewModel.CanOpenFamilyInventory)
        {
            return;
        }

        e.Handled = true;
        await OpenFamilyInventoryAsync(
            viewModel,
            FamilyInventoryTab.Money);
    }

    private async Task OpenFamilyInventoryAsync(
        MainWindowViewModel viewModel,
        FamilyInventoryTab initialTab = FamilyInventoryTab.Money)
    {
        if (_familyInventoryDialogOpen
            || !viewModel.CanOpenFamilyInventory)
        {
            return;
        }

        _familyInventoryDialogOpen = true;
        SetPaperDialogBackdrop(true);

        try
        {
            var window = new FamilyInventoryWindow(
                viewModel,
                initialTab,
                TownLifeService);
            await window.ShowDialog(this);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            viewModel.ReportPersistenceStatus(
                $"Family Inventory failed: {exception.Message}");
        }
        finally
        {
            SetPaperDialogBackdrop(false);
            _familyInventoryDialogOpen = false;
        }
    }

    private async void OnRelationsClick(
        object? sender,
        RoutedEventArgs e)
    {
        await OpenRelationsAsync();
    }

    private async Task OpenRelationsAsync()
    {
        if (_familyRelationsDialogOpen
            || DataContext is not MainWindowViewModel viewModel
            || !viewModel.HasFamilyRelations)
        {
            return;
        }

        _familyRelationsDialogOpen = true;
        SetPaperDialogBackdrop(true);

        try
        {
            var window = new FamilyRelationsWindow(viewModel);
            await window.ShowDialog(this);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            viewModel.ReportPersistenceStatus(
                $"Family Relations failed: {exception.Message}");
        }
        finally
        {
            SetPaperDialogBackdrop(false);
            _familyRelationsDialogOpen = false;
        }
    }

    private async Task OpenTownAffairsAsync(
        TownAffairsRequest request)
    {
        if (_townLifeDialogOpen
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (TownLifeService is null)
        {
            viewModel.ReportPersistenceStatus(
                "Town / City Affairs is unavailable because its service did not initialize.");
            return;
        }

        _townLifeDialogOpen = true;
        SetPaperDialogBackdrop(true);

        try
        {
            var snapshot =
                TownLifeService.GetTownLife(request.TownId);
            var model =
                viewModel.CreateTownAffairsViewModel(snapshot, request);
            var window = new TownLifeWindow(model);
            await window.ShowDialog(this);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            viewModel.ReportPersistenceStatus(
                $"Town / City Affairs failed: {exception.Message}");
        }
        finally
        {
            SetPaperDialogBackdrop(false);
            _townLifeDialogOpen = false;
        }
    }

    private async void OnMapClick(
        object? sender,
        RoutedEventArgs e)
    {
        await OpenMapAsync();
    }

    private async Task OpenMapAsync()
    {
        if (_mapDialogOpen)
            return;

        var viewModel = DataContext as MainWindowViewModel;
        if (MapDataSource is null
            || GenealogySelection is null
            || viewModel is null)
        {
            viewModel?.ReportPersistenceStatus(
                "Map is unavailable because the Location service did not initialize.");

            return;
        }

        _mapDialogOpen =
            true;

        try
        {
            var window =
                new TownMapWindow(
                    MapDataSource,
                    GenealogySelection,
                    viewModel,
                    TownLifeService);

            await window.ShowDialog(
                this);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                exception);

            viewModel.ReportPersistenceStatus(
                $"Map failed: {exception.Message}");
        }
        finally
        {
            _mapDialogOpen =
                false;
        }
    }

    private async void OnViewTreeClick(
        object? sender,
        RoutedEventArgs e)
    {
        await OpenGenealogyAsync();
    }

    private async Task OpenGenealogyAsync()
    {
        if (_genealogyDialogOpen)
            return;

        if (GenealogyDataSource is null
            || GenealogySelection is null)
        {
            if (DataContext
                is MainWindowViewModel viewModel)
            {
                viewModel.ReportPersistenceStatus(
                    "Genealogy is unavailable because the Family service did not initialize.");
            }

            return;
        }

        _genealogyDialogOpen =
            true;

        try
        {
            var window =
                new GenealogyWindow(
                    GenealogyDataSource,
                    GenealogySelection,
                    personId =>
                    {
                        if (DataContext is MainWindowViewModel viewModel)
                        {
                            viewModel.SelectHouseholdFromTree(
                                personId);
                        }
                    });

            await window.ShowDialog(
                this);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                exception);

            if (DataContext
                is MainWindowViewModel viewModel)
            {
                viewModel.ReportPersistenceStatus(
                    $"Genealogy failed: {exception.Message}");
            }
        }
        finally
        {
            _genealogyDialogOpen =
                false;
        }
    }
}
