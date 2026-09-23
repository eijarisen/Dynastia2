namespace Dynastia.Contracts;

public interface IVersionedYearSystemRegistry : IYearSystemRegistry
{
    long Version { get; }
}
