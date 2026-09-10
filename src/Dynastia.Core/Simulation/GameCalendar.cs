using Dynastia.Contracts;

namespace Dynastia.Core.Simulation;

public sealed class GameCalendar : IGameCalendar
{
    public int GetDaysInMonth(int year, int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(month),
                "Month must be between 1 and 12.");
        }

        // Dynastia deliberately excludes February 29.
        return month switch
        {
            2 => 28,
            4 or 6 or 9 or 11 => 30,
            _ => 31
        };
    }
}
