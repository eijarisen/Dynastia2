using Dynastia.Core.Simulation;

namespace Dynastia.Core.Tests;

public sealed class GameCalendarTests
{
    private readonly GameCalendar _calendar = new();

    [Theory]
    [InlineData(1900, 1, 31)]
    [InlineData(1900, 2, 28)]
    [InlineData(1904, 2, 28)]
    [InlineData(1900, 4, 30)]
    [InlineData(1900, 6, 30)]
    [InlineData(1900, 9, 30)]
    [InlineData(1900, 11, 30)]
    [InlineData(1900, 12, 31)]
    public void UsesMonthLengthButNeverFebruary29(
        int year,
        int month,
        int expected)
    {
        Assert.Equal(
            expected,
            _calendar.GetDaysInMonth(year, month));
    }
}
