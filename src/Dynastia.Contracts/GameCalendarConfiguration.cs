namespace Dynastia.Contracts;

public static class GameCalendarConfiguration
{
    public const int GameStartYear = 1700;
    public const int MinimumSelectableStartYear = 1700;
    public const int MaximumSelectableStartYear = 2000;
    public const int StartYearStep = 10;
    public const int TechnologyFreezeYear = 2026;
    public const int LegacyDesktopStartYear = 1900;

    public static int NormalizeSelectableStartYear(
        int year)
    {
        var clamped =
            Math.Clamp(
                year,
                MinimumSelectableStartYear,
                MaximumSelectableStartYear);

        var offset =
            clamped - MinimumSelectableStartYear;

        var snapped =
            (int)Math.Round(
                offset / (double)StartYearStep,
                MidpointRounding.AwayFromZero)
            * StartYearStep;

        return Math.Clamp(
            MinimumSelectableStartYear + snapped,
            MinimumSelectableStartYear,
            MaximumSelectableStartYear);
    }
}
