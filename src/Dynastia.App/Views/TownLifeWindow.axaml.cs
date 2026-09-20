using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.Contracts;

namespace Dynastia.App.Views;

public partial class TownLifeWindow : Window
{
    public TownLifeWindow()
    {
        InitializeComponent();
    }

    public TownLifeWindow(TownLifeSnapshot snapshot)
        : this()
    {
        DataContext = snapshot;
        Title = snapshot.WindowTitle;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close();
    }
}
