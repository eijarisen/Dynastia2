namespace Dynastia.Contracts;

public interface IYearSystemRegistry
{
    void Register(IYearSystem system);

    IReadOnlyCollection<IYearSystem> Systems { get; }
}