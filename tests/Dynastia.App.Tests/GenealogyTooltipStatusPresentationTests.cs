using System.Reflection;
using Avalonia.Media;
using Dynastia.StandardUI.Genealogy.Rendering;

namespace Dynastia.App.Tests;

public sealed class GenealogyTooltipStatusPresentationTests
{
    [Theory]
    [InlineData("Health: 90/100", 67, 160, 71)]
    [InlineData("Health: 35/100 • Influenza", 230, 81, 0)]
    [InlineData("Stress: 0/100", 67, 160, 71)]
    [InlineData("Stress: 2/100", 214, 183, 106)]
    [InlineData("Education: Level 2", 230, 126, 34)]
    [InlineData("Happiness: Unhappy", 230, 81, 0)]
    [InlineData("Career: Satisfied", 154, 205, 50)]
    [InlineData("Marriage: Thriving", 67, 160, 71)]
    public void TreeTooltipStatusLinesUseHouseholdStatusPalette(
        string line,
        byte red,
        byte green,
        byte blue)
    {
        var method = typeof(GenealogyCanvas).GetMethod(
            "ResolveTooltipLineBrush",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var brush = Assert.IsType<SolidColorBrush>(
            method.Invoke(null, [line]));

        Assert.Equal(Color.FromRgb(red, green, blue), brush.Color);
    }
}
