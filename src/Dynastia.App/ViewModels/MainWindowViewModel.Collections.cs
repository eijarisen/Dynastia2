using System.Collections.ObjectModel;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<AlbumEventViewModel>
        AlbumEvents { get; } = [];

    public ObservableCollection<YearSummaryHouseholdViewModel>
        YearSummaryHouseholds { get; } = [];

    public ObservableCollection<PersonRowViewModel>
        People { get; } = [];

    public ObservableCollection<PlayablePersonTabViewModel>
        PlayableTabs { get; } = [];

    public ObservableCollection<PlayablePersonTabViewModel>
        BloodlineTabs { get; } = [];

    public ObservableCollection<FamilyMemberCardViewModel>
        HouseholdMembers { get; } = [];

    public ObservableCollection<FamilyMemberCardViewModel>
        ExtendedFamilyMembers { get; } = [];

    public ObservableCollection<FamilyMemberCardViewModel>
        DeceasedFamilyMembers { get; } = [];

    public ObservableCollection<StatValue>
        SelectedStats { get; } = [];

    public ObservableCollection<BiographyEntryViewModel>
        SelectedBiography { get; } = [];

}
