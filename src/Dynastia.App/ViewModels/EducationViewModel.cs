namespace Dynastia.App.ViewModels;

public sealed class EducationViewModel
{
    public EducationViewModel(int age, int level)
    {
        Level = level;

        EducationText =
            age < 6
                ? "Education: None"
                : level > 0
                    ? $"Education: Level {level}"
                    : "Education: N/A";
    }

    public int Level { get; }

    public string EducationText { get; }
}
