using Dynastia.Contracts;

namespace Dynastia.Core.Entities;

public sealed class ComponentContainer :
    IComponentContainer
{
    private readonly Dictionary<Type, object>
        _components = [];

    public IReadOnlyCollection<object> All =>
        _components.Values.ToList();

    public bool Has<T>()
        where T : class
    {
        return _components.ContainsKey(
            typeof(T));
    }

    public T? Get<T>()
        where T : class
    {
        return _components.TryGetValue(
            typeof(T),
            out var component)
                ? (T)component
                : null;
    }

    public void Set<T>(
        T component)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(
            component);

        _components[
            typeof(T)] =
                component;
    }

    public void Set(
        Type componentType,
        object component)
    {
        ArgumentNullException.ThrowIfNull(
            componentType);

        ArgumentNullException.ThrowIfNull(
            component);

        if (!componentType.IsInstanceOfType(
            component))
        {
            throw new ArgumentException(
                $"Component instance is not of type " +
                $"'{componentType.FullName}'.",
                nameof(component));
        }

        _components[
            componentType] =
                component;
    }

    public bool Remove<T>()
        where T : class
    {
        return _components.Remove(
            typeof(T));
    }
}
