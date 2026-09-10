namespace Dynastia.App.Persistence;

public sealed record GameUiSaveState(
    Guid? SelectedPersonId,
    Guid? ActiveControllerId,
    int AlbumYear,
    bool IsLivingFamilyView,
    int DetailsTabIndex);
