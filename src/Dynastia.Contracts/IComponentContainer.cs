namespace Dynastia.Contracts;

public interface IComponentContainer
{
    bool Has<T>() where T : class;
    T? Get<T>() where T : class;
    void Set<T>(T component) where T : class;
    bool Remove<T>() where T : class;
}
