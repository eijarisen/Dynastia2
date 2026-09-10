using System.Reflection;
using System.Runtime.Loader;
using Dynastia.Contracts;

namespace Dynastia.PluginHost;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _contractsAssemblyName;

    public PluginLoadContext(string pluginAssemblyPath)
        : base(
            $"DynastiaPlugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}",
            isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);

        _contractsAssemblyName =
            typeof(IGamePlugin).Assembly.GetName().Name
            ?? throw new InvalidOperationException(
                "Could not determine Dynastia.Contracts assembly name.");
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Dynastia.Contracts MUST come from the host/default context.
        // If the plugin loads a second copy, IGamePlugin in the plugin
        // becomes a different .NET type from IGamePlugin in the host.
        if (string.Equals(
            assemblyName.Name,
            _contractsAssemblyName,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        return assemblyPath is null
            ? null
            : LoadFromAssemblyPath(assemblyPath);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath =
            _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        return libraryPath is null
            ? IntPtr.Zero
            : LoadUnmanagedDllFromPath(libraryPath);
    }
}