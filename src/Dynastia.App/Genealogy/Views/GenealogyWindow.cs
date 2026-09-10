namespace Dynastia.StandardUI.Genealogy.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Dynastia.StandardUI.Genealogy.Contracts;

public sealed class GenealogyWindow :
    Window
{
    private readonly GenealogyPanel
        _panel;

    public GenealogyWindow(
        IGenealogyDataSource data,
        IGlobalSelectionService selection)
    {
        Title =
            "Genealogy Tree";

        Width =
            1200;

        Height =
            820;

        MinWidth =
            800;

        MinHeight =
            560;

        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;

        WindowState =
            WindowState.Maximized;

        _panel =
            new GenealogyPanel(
                data,
                selection);

        var title =
            new TextBlock
            {
                Text =
                    "Genealogy Tree",

                FontSize =
                    24,

                FontWeight =
                    Avalonia.Media
                        .FontWeight.Bold,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var close =
            new Button
            {
                Content =
                    "Close",

                Padding =
                    new Thickness(
                        16,
                        8)
            };

        close.Click +=
            (_, _) =>
                Close();

        var header =
            new Grid
            {
                Margin =
                    new Thickness(
                        12,
                        10,
                        12,
                        4),

                ColumnDefinitions =
                {
                    new ColumnDefinition(
                        new GridLength(
                            1,
                            GridUnitType.Star)),

                    new ColumnDefinition(
                        GridLength.Auto)
                }
            };

        Grid.SetColumn(
            title,
            0);

        Grid.SetColumn(
            close,
            1);

        header.Children.Add(
            title);

        header.Children.Add(
            close);

        var root =
            new Grid();

        root.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        root.RowDefinitions.Add(
            new RowDefinition(
                new GridLength(
                    1,
                    GridUnitType.Star)));

        Grid.SetRow(
            header,
            0);

        Grid.SetRow(
            _panel,
            1);

        root.Children.Add(
            header);

        root.Children.Add(
            _panel);

        Content =
            root;

        KeyDown +=
            OnKeyDown;

        Opened +=
            (_, _) =>
                _panel.FitTree();
    }

    protected override void OnClosed(
        EventArgs e)
    {
        _panel.Dispose();

        base.OnClosed(
            e);
    }

    private void OnKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        e.Handled =
            true;

        Close();
    }
}
