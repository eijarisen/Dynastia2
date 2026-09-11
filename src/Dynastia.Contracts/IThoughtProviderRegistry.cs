namespace Dynastia.Contracts;

public interface IThoughtProviderRegistry
{
    void Register(
        IThoughtProvider provider);

    IReadOnlyCollection<IThoughtProvider> Providers { get; }
}
