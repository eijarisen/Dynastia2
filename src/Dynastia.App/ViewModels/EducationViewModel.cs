using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class EducationViewModel
{
    public EducationViewModel(
        int age,
        int level,
        IReadOnlyList<CraftProgressSnapshot>? crafts = null)
    {
        Level = level;

        EducationText =
            age < 6
                ? string.Empty
                : level > 0
                    ? $"Education: Level {level}"
                    : "Education: N/A";

        CraftsText = age < 6
            ? string.Empty
            : crafts is null || crafts.Count == 0
                ? "Crafts: None"
                : "Crafts: " + string.Join(
                ", ",
                crafts.Select(craft =>
                    $"{craft.CraftName} ({craft.MasteryName})"));
    }

    public int Level { get; }

    public string EducationText { get; }

    public string CraftsText { get; }
}
