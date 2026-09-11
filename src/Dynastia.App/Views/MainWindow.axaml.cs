using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Dynastia.App.Genealogy.Host;
using Dynastia.App.ViewModels;
using Dynastia.StandardUI.Genealogy.Contracts;
using Dynastia.StandardUI.Genealogy.Views;

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
    private bool _instructionsDialogOpen;

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

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(
            InputElement.KeyDownEvent,
            OnWindowKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void OnWindowKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (_persistenceDialogOpen
            || _genealogyDialogOpen
            || _instructionsDialogOpen
            || e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext
            is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsMainMenuPromptVisible
            || !viewModel.NextYearCommand.CanExecute(null))
        {
            return;
        }

        // Tunnel routing prevents a focused Button from also
        // consuming Enter and accidentally advancing twice.
        e.Handled = true;

        viewModel.NextYearCommand.Execute(null);
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

    private async void OnInstructionsClick(
        object? sender,
        RoutedEventArgs e)
    {
        _instructionsDialogOpen =
            true;

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
            _instructionsDialogOpen =
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
