using Dynastia.Contracts;

namespace Dynastia.TestSupport;

/// <summary>
/// A strict script of RNG calls. Method, arguments, order and completion are checked;
/// Chance deliberately does not consume a separate NextDouble entry.
/// </summary>
public sealed class SequenceGameRandom : IGameRandom, IDisposable
{
    public abstract record ExpectedCall;
    public sealed record IntCall(int Min, int Max, int Value) : ExpectedCall;
    public sealed record DoubleCall(double Value) : ExpectedCall;
    public sealed record ChanceCall(double Probability, bool Value) : ExpectedCall;

    private readonly Queue<ExpectedCall> _remaining;

    public SequenceGameRandom(params ExpectedCall[] calls)
    {
        ArgumentNullException.ThrowIfNull(calls);
        if (calls.Any(call => call is null))
            throw new ArgumentException("An RNG script cannot contain null calls.", nameof(calls));
        _remaining = new Queue<ExpectedCall>(calls);
    }

    public int ConsumedCount { get; private set; }
    public int RemainingCount => _remaining.Count;

    public static ExpectedCall Int(int minInclusive, int maxInclusive, int value)
    {
        if (minInclusive > maxInclusive || value < minInclusive || value > maxInclusive)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be inside the inclusive bounds.");
        return new IntCall(minInclusive, maxInclusive, value);
    }

    public static ExpectedCall Double(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value >= 1)
            throw new ArgumentOutOfRangeException(nameof(value), "NextDouble must return a value in [0, 1).");
        return new DoubleCall(value);
    }

    public static ExpectedCall Chance(double probability, bool value)
    {
        if (!double.IsFinite(probability) || probability < 0 || probability > 1)
            throw new ArgumentOutOfRangeException(nameof(probability));
        return new ChanceCall(probability, value);
    }

    public int NextInt(int minInclusive, int maxInclusive)
    {
        var actual = $"NextInt({minInclusive}, {maxInclusive})";
        if (Peek(actual) is not IntCall call || call.Min != minInclusive || call.Max != maxInclusive)
            throw Mismatch(actual);
        Consume();
        return call.Value;
    }

    public double NextDouble()
    {
        if (Peek("NextDouble()") is not DoubleCall call)
            throw Mismatch("NextDouble()");
        Consume();
        return call.Value;
    }

    public bool Chance(double probability)
    {
        var actual = $"Chance({probability:R})";
        if (Peek(actual) is not ChanceCall call || call.Probability != probability)
            throw Mismatch(actual);
        Consume();
        return call.Value;
    }

    public void AssertComplete()
    {
        if (_remaining.Count != 0)
            throw new InvalidOperationException(
                $"RNG script has {_remaining.Count} unconsumed call(s) after {ConsumedCount} calls. " +
                $"Next expected: {_remaining.Peek()}.");
    }

    public void Dispose() => AssertComplete();

    private ExpectedCall Peek(string actual) => _remaining.Count > 0
        ? _remaining.Peek()
        : throw new InvalidOperationException(
            $"Unexpected RNG call #{ConsumedCount + 1}: {actual}; the script is exhausted.");

    private InvalidOperationException Mismatch(string actual) => new(
        $"RNG call #{ConsumedCount + 1}: expected {_remaining.Peek()}, actual {actual}.");

    private void Consume()
    {
        _remaining.Dequeue();
        ConsumedCount++;
    }
}
