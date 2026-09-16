using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class EducationViewModel
{
    public EducationViewModel(
        int age,
        int level,
        IReadOnlyList<CraftInfo>? crafts = null)
    {
        Level = level;

        EducationText =
            age < 6
                ? "Education: None"
                : level > 0
                    ? $"Education: Level {level}"
                    : "Education: N/A";

        CraftsText = crafts is null || crafts.Count == 0
            ? "Crafts: None"
            : $"Crafts: {string.Join(", ", crafts.Select(craft => craft.Name))}";
    }

    public int Level { get; }

    public string EducationText { get; }

    public string CraftsText { get; }
}
