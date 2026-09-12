using System.Collections.ObjectModel;

namespace Dynastia.App.ViewModels;

public sealed class YearSummaryHouseholdViewModel
{
    public YearSummaryHouseholdViewModel(
        string title,
        IEnumerable<AlbumEventViewModel> events)
    {
        Title = title;

        foreach (var item in events)
            Events.Add(item);
    }

    public string Title { get; }

    public ObservableCollection<AlbumEventViewModel>
        Events { get; } = [];
}
