using System.Reflection;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.PluginHost;

public sealed class PluginLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<LoadedPlugin> LoadPlugins(
        string pluginsRoot,
        IGamePluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var loadedPlugins = new List<LoadedPlugin>();

        if (!Directory.Exists(pluginsRoot))
        {
            context.Log(
                $"Plugin directory does not exist: {pluginsRoot}");

            return loadedPlugins;
        }

        var knownIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var manifestPaths = Directory
                .EnumerateFiles(
                    pluginsRoot,
                    "plugin.json",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            context.Log(
                $"Scanning plugin directory: {pluginsRoot}");

            context.Log(
                $"Found {manifestPaths.Count} plugin manifest(s).");

        foreach (var manifestPath in manifestPaths)
        {
            context.Log(
                $"Reading plugin manifest: {manifestPath}");

            var json = File.ReadAllText(manifestPath);

            var manifest =
                JsonSerializer.Deserialize<PluginManifest>(
                    json,
                    JsonOptions)
                ?? throw new InvalidDataException(
                    $"Could not deserialize plugin manifest: {manifestPath}");

            ValidateManifest(manifest, manifestPath);

            if (!knownIds.Add(manifest.Id))
            {
                throw new InvalidDataException(
                    $"Duplicate plugin ID '{manifest.Id}'.");
            }

            var pluginDirectory =
                Path.GetDirectoryName(manifestPath)
                ?? throw new InvalidDataException(
                    $"Could not determine directory for {manifestPath}");

            var assemblyPath = Path.GetFullPath(
                Path.Combine(pluginDirectory, manifest.Assembly));

            var normalizedPluginDirectory =
                Path.GetFullPath(pluginDirectory)
                + Path.DirectorySeparatorChar;

            if (!assemblyPath.StartsWith(
                normalizedPluginDirectory,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Plugin '{manifest.Id}' attempts to load an assembly " +
                    "outside its own directory.");
            }

            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException(
                    $"Plugin assembly not found for '{manifest.Id}'.",
                    assemblyPath);
            }

            var loadContext =
                new PluginLoadContext(assemblyPath);

            var assembly =
                loadContext.LoadFromAssemblyPath(assemblyPath);

            var entryType = assembly.GetType(
                manifest.EntryType,
                throwOnError: true,
                ignoreCase: false)
                ?? throw new TypeLoadException(
                    $"Could not find entry type '{manifest.EntryType}'.");

            if (!typeof(IGamePlugin).IsAssignableFrom(entryType))
            {
                throw new InvalidOperationException(
                    $"Plugin entry type '{manifest.EntryType}' " +
                    $"does not implement {nameof(IGamePlugin)}.");
            }

            if (entryType.IsAbstract || entryType.IsInterface)
            {
                throw new InvalidOperationException(
                    $"Plugin entry type '{manifest.EntryType}' " +
                    "cannot be instantiated.");
            }

            var instance =
                Activator.CreateInstance(entryType) as IGamePlugin
                ?? throw new InvalidOperationException(
                    $"Could not instantiate plugin '{manifest.Id}'.");

            instance.Initialize(context);

            loadedPlugins.Add(
                new LoadedPlugin(manifest, instance));

            context.Log(
                $"Loaded {manifest.Name} ({manifest.Id}) v{manifest.Version}");
        }

        return loadedPlugins;
    }

    private static void ValidateManifest(
        PluginManifest manifest,
        string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
            throw new InvalidDataException(
                $"Plugin ID is missing in {manifestPath}.");

        if (string.IsNullOrWhiteSpace(manifest.Name))
            throw new InvalidDataException(
                $"Plugin name is missing in {manifestPath}.");

        if (!Version.TryParse(manifest.Version, out _))
            throw new InvalidDataException(
                $"Plugin '{manifest.Id}' has invalid version " +
                $"'{manifest.Version}'.");

        if (manifest.ApiVersion != PluginApi.Version)
            throw new InvalidDataException(
                $"Plugin '{manifest.Id}' uses API version " +
                $"{manifest.ApiVersion}, but Dynastia expects " +
                $"{PluginApi.Version}.");

        if (string.IsNullOrWhiteSpace(manifest.Assembly))
            throw new InvalidDataException(
                $"Plugin '{manifest.Id}' has no assembly specified.");

        if (string.IsNullOrWhiteSpace(manifest.EntryType))
            throw new InvalidDataException(
                $"Plugin '{manifest.Id}' has no entry type specified.");
    }
}