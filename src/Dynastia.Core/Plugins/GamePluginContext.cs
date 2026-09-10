using Dynastia.Contracts;

namespace Dynastia.Core.Plugins;

public sealed class GamePluginContext : IGamePluginContext
{
    private readonly Dictionary<Type, object> _services = new();

    public void AddService<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(T)] = service;
    }

    public T? GetService<T>() where T : class
    {
        return _services.TryGetValue(typeof(T), out var service)
            ? (T)service
            : null;
    }

    public void Log(string message)
    {
        var formatted = $"[PLUGIN] {message}";

        Console.WriteLine(formatted);
        System.Diagnostics.Debug.WriteLine(formatted);
    }
}