namespace Dynastia.Core.Tests;

public sealed class TestSupportTests
{
    [Fact]
    public void RepositoryFilesResolveTheSameCachedSourceRoot()
    {
        Assert.True(File.Exists(Path.Combine(RepositoryFiles.Root, "Dynastia.slnx")));
        Assert.Same(RepositoryFiles.Root, RepositoryFiles.Root);
        Assert.Equal(Path.Combine(RepositoryFiles.Root, "tests", "Dynastia.Core.Tests"),
            RepositoryFiles.Path("tests", "Dynastia.Core.Tests"));
        Assert.Equal(File.ReadAllText(RepositoryFiles.Path("Dynastia.slnx")), RepositoryFiles.ReadText("Dynastia.slnx"));
        Assert.Equal(File.ReadAllLines(RepositoryFiles.Path("Dynastia.slnx")), RepositoryFiles.ReadLines("Dynastia.slnx"));
    }

    [Fact]
    public void ScriptChecksMixedCallOrderAndInclusiveIntegerBounds()
    {
        using var random = new SequenceGameRandom(
            SequenceGameRandom.Int(2, 5, 5), SequenceGameRandom.Double(0.25), SequenceGameRandom.Chance(0.4, true));
        Assert.Equal(5, random.NextInt(2, 5));
        Assert.Equal(0.25, random.NextDouble());
        Assert.True(random.Chance(0.4));
        Assert.Equal(3, random.ConsumedCount);
        Assert.Equal(0, random.RemainingCount);
    }

    [Fact]
    public void WrongMethodBoundsAndProbabilityFailWithoutConsumingExpectedCall()
    {
        using var random = new SequenceGameRandom(SequenceGameRandom.Int(1, 3, 2), SequenceGameRandom.Chance(0.25, false));
        Assert.Contains("expected", Assert.Throws<InvalidOperationException>(() => random.NextDouble()).Message);
        Assert.Throws<InvalidOperationException>(() => random.NextInt(1, 4));
        Assert.Equal(0, random.ConsumedCount);
        Assert.Equal(2, random.NextInt(1, 3));
        Assert.Throws<InvalidOperationException>(() => random.Chance(0.5));
        Assert.False(random.Chance(0.25));
    }

    [Fact]
    public void ExtraAndMissingCallsFailClearly()
    {
        var extra = new SequenceGameRandom();
        Assert.Contains("exhausted", Assert.Throws<InvalidOperationException>(() => extra.NextDouble()).Message);
        using var missing = new SequenceGameRandom(SequenceGameRandom.Double(0.5));
        Assert.Contains("unconsumed", Assert.Throws<InvalidOperationException>(missing.AssertComplete).Message);
        Assert.Throws<InvalidOperationException>(missing.Dispose);
        Assert.Equal(0.5, missing.NextDouble());
    }

    [Fact]
    public void InvalidScriptValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SequenceGameRandom.Int(1, 3, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SequenceGameRandom.Double(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SequenceGameRandom.Double(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => SequenceGameRandom.Chance(-0.1, false));
    }
}
