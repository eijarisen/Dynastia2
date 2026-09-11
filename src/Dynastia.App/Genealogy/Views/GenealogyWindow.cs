namespace Dynastia.StandardUI.Genealogy.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Dynastia.StandardUI.Genealogy.Contracts;

public sealed class GenealogyWindow :
    Window
{
    private static readonly IBrush WindowBackground =
        new SolidColorBrush(
            Color.FromRgb(
                2,
                8,
                9));

    private static readonly IBrush Gold =
        new SolidColorBrush(
            Color.FromRgb(
                222,
                183,
                78));

    private readonly GenealogyPanel
        _panel;

    public GenealogyWindow(
        IGenealogyDataSource data,
        IGlobalSelectionService selection)
    {
        Title =
            "Family Tree";

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

        Background =
            WindowBackground;

        _panel =
            new GenealogyPanel(
                data,
                selection);

        var title =
            new TextBlock
            {
                Text =
                    "Family Tree",

                FontSize =
                    25,

                FontWeight =
                    FontWeight.Bold,

                Foreground =
                    Gold,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var close =
            new Button
            {
                Content =
                    new TextBlock
                    {
                        Text =
                            "Close",

                        Foreground =
                            new SolidColorBrush(
                                Color.FromRgb(
                                    244,
                                    230,
                                    195)),

                        FontWeight =
                            FontWeight.SemiBold
                    },

                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            9,
                            31,
                            27)),

                BorderBrush =
                    new SolidColorBrush(
                        Color.FromRgb(
                            143,
                            105,
                            37)),

                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(
                        15,
                        7),

                CornerRadius =
                    new CornerRadius(4)
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
                        9,
                        12,
                        7),

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
            new Grid
            {
                Background =
                    WindowBackground
            };

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
