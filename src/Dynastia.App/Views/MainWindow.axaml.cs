using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Dynastia.App.ViewModels;

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
            || e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext
            is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (!viewModel.NextYearCommand.CanExecute(null))
            return;

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
                                "Save Dynastia Game",

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
                $"Saved {file.Name}.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                exception);

            viewModel.ReportPersistenceStatus(
                $"Save failed: {exception.Message}");
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
}
