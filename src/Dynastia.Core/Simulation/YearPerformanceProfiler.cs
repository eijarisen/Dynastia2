using System.Diagnostics;
using System.Globalization;

namespace Dynastia.Core.Simulation;

/// <summary>
/// Opt-in timing output for deterministic year-simulation profiling.
/// Enable by setting DYNASTIA_PROFILE_YEAR=1 before launching the process.
/// Profiling never reads RNG state through gameplay APIs and never mutates game state.
/// </summary>
public static class YearPerformanceProfiler
{
    public const string EnvironmentVariableName =
        "DYNASTIA_PROFILE_YEAR";

    public static bool IsEnabled
    {
        get
        {
            var value =
                Environment.GetEnvironmentVariable(
                    EnvironmentVariableName);

            return value is not null
                && (
                    value.Equals("1", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("on", StringComparison.OrdinalIgnoreCase)
                );
        }
    }

    public static long StartTimestamp() =>
        Stopwatch.GetTimestamp();

    public static TimeSpan GetElapsedTime(
        long startedAt) =>
        Stopwatch.GetElapsedTime(startedAt);

    public static void LogDuration(
        int year,
        string stage,
        long startedAt,
        string? details = null)
    {
        LogDuration(
            year,
            stage,
            Stopwatch.GetElapsedTime(startedAt),
            details);
    }

    public static void LogDuration(
        int year,
        string stage,
        TimeSpan elapsed,
        string? details = null)
    {
        var milliseconds =
            elapsed.TotalMilliseconds.ToString(
                "F3",
                CultureInfo.InvariantCulture);

        WriteLine(
            $"[PERF][{year}] {stage} ms={milliseconds}" +
            (string.IsNullOrWhiteSpace(details)
                ? string.Empty
                : $" {details}"));
    }

    public static void Log(
        int year,
        string stage,
        string details)
    {
        WriteLine(
            $"[PERF][{year}] {stage} {details}");
    }

    private static void WriteLine(
        string message)
    {
        try
        {
            Console.WriteLine(message);
        }
        catch
        {
            // Profiling must never change simulation behavior or failure handling.
        }
    }
}
