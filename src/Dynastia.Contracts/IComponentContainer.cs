namespace Dynastia.Contracts;

public interface IComponentContainer
{
    IReadOnlyCollection<object> All { get; }

    bool Has<T>()
        where T : class;

    T? Get<T>()
        where T : class;

    void Set<T>(
        T component)
        where T : class;

    void Set(
        Type componentType,
        object component);

    bool Remove<T>()
        where T : class;
}
