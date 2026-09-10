using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class BiographyEntryViewModel
{
    public BiographyEntryViewModel(
        BiographyEntry entry)
    {
        Year = entry.Year;
        Message = entry.Message;
    }

    public int Year { get; }

    public string Message { get; }
}
