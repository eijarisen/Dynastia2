namespace Dynastia.StandardUI.Map.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Dynastia.App.Controls;
using Dynastia.App.Map.Host;
using Dynastia.App.Views;
using Dynastia.Contracts;
using Dynastia.StandardUI.Genealogy.Contracts;

public sealed class TownMapWindow :
    Window
{
    private static readonly IBrush WindowBackground =
        new SolidColorBrush(
            Color.FromRgb(2, 8, 9));

    private static readonly IBrush Gold =
        new SolidColorBrush(
            Color.FromRgb(222, 183, 78));

    private readonly TownMapPanel _panel;
    private readonly ITownLifeService? _townLife;
    private bool _townLifeDialogOpen;

    public TownMapWindow(
        GameMapDataSource data,
        IGlobalSelectionService selection,
        ITownLifeService? townLife = null)
    {
        _townLife = townLife;
        Title = "Dynastia Map";
        Width = 1200;
        Height = 820;
        MinWidth = 820;
        MinHeight = 560;
        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;
        WindowState =
            WindowState.Maximized;
        Background =
            WindowBackground;

        _panel =
            new TownMapPanel(
                data,
                selection,
                townLife);

        _panel.TownActivated +=
            OnTownActivated;

        var title =
            new TextBlock
            {
                Text = "Dynastia Map",
                FontSize = 25,
                FontWeight = FontWeight.Bold,
                Foreground = Gold,
                VerticalAlignment = VerticalAlignment.Center
            };

        var close =
            new ImageStateButton
            {
                Width = 170,
                Height = 70,
                IdleSource =
                    "avares://Dynastia.App/Assets/UI/b_close_idle.png",
                HoverSource =
                    "avares://Dynastia.App/Assets/UI/b_close_hover.png",
                FallbackText = "Close"
            };

        close.Click +=
            (_, _) =>
                Close();

        var header =
            new Grid
            {
                Margin = new Thickness(12, 9, 12, 7),
                ColumnDefinitions =
                {
                    new ColumnDefinition(
                        new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

        Grid.SetColumn(title, 0);
        Grid.SetColumn(close, 1);

        header.Children.Add(title);
        header.Children.Add(close);

        var root =
            new Grid
            {
                Background = WindowBackground
            };

        root.RowDefinitions.Add(
            new RowDefinition(GridLength.Auto));

        root.RowDefinitions.Add(
            new RowDefinition(
                new GridLength(1, GridUnitType.Star)));

        Grid.SetRow(header, 0);
        Grid.SetRow(_panel, 1);

        root.Children.Add(header);
        root.Children.Add(_panel);

        Content = root;

        KeyDown +=
            OnKeyDown;

        Opened +=
            (_, _) =>
                _panel.ResetView();
    }

    protected override void OnClosed(
        EventArgs e)
    {
        _panel.TownActivated -=
            OnTownActivated;

        _panel.Dispose();
        base.OnClosed(e);
    }


    private async void OnTownActivated(
        string townId)
    {
        if (_townLife is null
            || _townLifeDialogOpen)
        {
            return;
        }

        _townLifeDialogOpen = true;

        try
        {
            var snapshot = _townLife.GetTownLife(townId);
            var window = new TownLifeWindow(snapshot);
            await window.ShowDialog(this);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
        }
        finally
        {
            _townLifeDialogOpen = false;
        }
    }

    private void OnKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close();
    }
}
