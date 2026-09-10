using Dynastia.Contracts;

namespace Dynastia.Core.Entities;

public sealed class ComponentContainer : IComponentContainer
{
    private readonly Dictionary<Type, object> _components = [];

    public bool Has<T>() where T : class
        => _components.ContainsKey(typeof(T));

    public T? Get<T>() where T : class
        => _components.TryGetValue(typeof(T), out var component)
            ? (T)component
            : null;

    public void Set<T>(T component) where T : class
    {
        ArgumentNullException.ThrowIfNull(component);
        _components[typeof(T)] = component;
    }

    public bool Remove<T>() where T : class
        => _components.Remove(typeof(T));
}
