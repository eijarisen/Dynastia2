using Dynastia.App.ViewModels;

namespace Dynastia.App.Tests;

public sealed class Development14CraftEducationLabelsTests
{
    [Theory]
    [InlineData("musician", "Musician", "Music")]
    [InlineData("painter", "Painter", "Painting")]
    [InlineData("writer", "Writer", "Writing")]
    [InlineData("sculptor", "Sculptor", "Sculpture")]
    [InlineData("smith", "Smith", "Smith")]
    public void ArtisticCraftLearningUsesCraftNameRatherThanOccupation(
        string craftId,
        string fallbackName,
        string expected)
    {
        Assert.Equal(
            expected,
            MainWindowViewModel.GetCraftLearningName(craftId, fallbackName));
    }
}
