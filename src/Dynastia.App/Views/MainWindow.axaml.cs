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
        if (_actionSelectionDialogOpen
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

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
                var options = viewModel.GetCraftProfessionOptions();
                if (options.Count == 0)
                {
                    viewModel.ReportPersistenceStatus(
                        "No known Craft is currently available for self-employment.");
                    return;
                }

                var window = new PropertySelectionWindow(
                    "Work in a Profession",
                    "Work",
                    options);
                var selectedActionId = await window.ShowDialog<string?>(this);
                if (!string.IsNullOrWhiteSpace(selectedActionId))
                    viewModel.QueueCraftProfessionAction(selectedActionId);
                return;
            }

            if (e.ActionId.Equals(
                    "education.get_education",
                    StringComparison.OrdinalIgnoreCase))
            {
                var options = viewModel.GetEducationSelectionOptions();
                if (!options.Any(option => option.IsEnabled))
                {
                    viewModel.ReportPersistenceStatus(
                        "No education option is currently available.");
                    return;
                }

                var window = new PropertySelectionWindow(
                    "Get Education",
                    "Study",
                    options);
                var selectedOptionId = await window.ShowDialog<string?>(this);
                if (!string.IsNullOrWhiteSpace(selectedOptionId))
                    viewModel.QueueEducationAction(selectedOptionId);
                return;
            }

            if (e.ActionId.Equals(
                    "career.seek_employment",
                    StringComparison.OrdinalIgnoreCase)
                || e.ActionId.Equals(
                    "career.find_another_job",
                    StringComparison.OrdinalIgnoreCase)
                || e.ActionId.Equals(
                    "career.help_seek_employment",
                    StringComparison.OrdinalIgnoreCase)
                || e.ActionId.Equals(
                    "career.help_find_better_job",
                    StringComparison.OrdinalIgnoreCase))
            {
                var model =
                    viewModel.GetJobOpportunityDialog(
                        e.ActionId);

                if (model is null
                    || model.Opportunities.Count == 0)
                {
                    viewModel.ReportPersistenceStatus(
                        "No suitable vacancies are currently available.");
                    return;
                }

                var jobOpportunitiesWindow =
                    new JobOpportunitiesWindow(model);

                var selection =
                    await jobOpportunitiesWindow.ShowDialog<JobOpportunityInfo?>(this);

                if (selection is not null)
                {
                    viewModel.QueueJobApplication(
                        e.ActionId,
                        selection);
                }

                return;
            }

            if (e.ActionId.Equals(
                    "relationship.find_spouse",
                    StringComparison.OrdinalIgnoreCase)
                || e.ActionId.Equals(
                    "relationship.marry_off_daughter",
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

                var loanWindow =
                    new LoanSelectionWindow(
                        isGivingLoan,
                        viewModel.GetLoanTerms,
                        viewModel.GetMaximumLoanPrincipal(e.ActionId));

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

            var window = new PropertySelectionWindow(
                isBuy ? "Select Town" : "Select Property",
                isBuy ? "Buy" : "Sell",
                options);

            var selectedId =
                await window.ShowDialog<string?>(this);

            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                viewModel.QueueActionWithSelection(
                    e.ActionId,
                    selectedId);
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

        if (viewModel.IsYearSummaryVisible
            && (e.Key == Key.Enter
                || e.Key == Key.Escape))
        {
            e.Handled = true;
            viewModel.HideYearSummaryCommand.Execute(null);
            return;
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

        if (viewModel.IsMainMenuPromptVisible
            || viewModel.IsStatusMessageVisible
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
        _familyInventoryDialogOpen = true;
        SetPaperDialogBackdrop(true);

        try
        {
            var window = new FamilyInventoryWindow(viewModel);
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

    private async void OnMapClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (_mapDialogOpen)
            return;

        if (MapDataSource is null
            || GenealogySelection is null)
        {
            if (DataContext
                is MainWindowViewModel viewModel)
            {
                viewModel.ReportPersistenceStatus(
                    "Map is unavailable because the Location service did not initialize.");
            }

            return;
        }

        _mapDialogOpen =
            true;

        try
        {
            var window =
                new TownMapWindow(
                    MapDataSource,
                    GenealogySelection);

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
                    $"Map failed: {exception.Message}");
            }
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
