using System.Reflection;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.PluginHost;

public sealed class PluginLoader
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public IReadOnlyList<LoadedPlugin> LoadPlugins(
        string pluginsRoot,
        IGamePluginContext context)
    {
        var loadedPlugins = new List<LoadedPlugin>();

        context.Log($"Scanning plugin directory: {pluginsRoot}");

        if (!Directory.Exists(pluginsRoot))
        {
            context.Log("Plugin directory does not exist.");
            return loadedPlugins;
        }

        var manifestPaths =
            Directory.GetFiles(
                    pluginsRoot,
                    "plugin.json",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

        context.Log(
            $"Found {manifestPaths.Count} plugin manifest(s).");

        var candidates =
            manifestPaths
                .Select(ReadCandidate)
                .ToList();

        ValidateUniqueIds(candidates);
        ValidateDependencies(candidates);

        var orderedCandidates =
            OrderByDependencies(candidates);

        foreach (var candidate in orderedCandidates)
        {
            context.Log(
                $"Reading plugin manifest: {candidate.ManifestPath}");

            var manifest = candidate.Manifest;
            var assemblyPath = ResolveAssemblyPath(candidate);

            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException(
                    $"Plugin assembly was not found for '{manifest.Id}'.",
                    assemblyPath);
            }

            var loadContext =
                new PluginLoadContext(assemblyPath);

            var assembly =
                loadContext.LoadFromAssemblyPath(assemblyPath);

            var entryType =
                assembly.GetType(
                    manifest.EntryType,
                    throwOnError: true,
                    ignoreCase: false)
                ?? throw new InvalidOperationException(
                    $"Plugin entry type '{manifest.EntryType}' was not found.");

            if (!typeof(IGamePlugin).IsAssignableFrom(entryType))
            {
                throw new InvalidOperationException(
                    $"Plugin entry type '{manifest.EntryType}' " +
                    $"does not implement {nameof(IGamePlugin)}.");
            }

            var instance =
                Activator.CreateInstance(entryType) as IGamePlugin
                ?? throw new InvalidOperationException(
                    $"Could not create plugin '{manifest.Id}'.");

            instance.Initialize(context);

            loadedPlugins.Add(
                new LoadedPlugin(
                    manifest,
                    instance));

            context.Log(
                $"Loaded {manifest.Name} {manifest.Version} ({manifest.Id}).");
        }

        return loadedPlugins;
    }

    private static PluginCandidate ReadCandidate(string manifestPath)
    {
        var manifest =
            JsonSerializer.Deserialize<PluginManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions)
            ?? throw new InvalidDataException(
                $"Could not deserialize plugin manifest: {manifestPath}");

        ValidateManifest(manifest, manifestPath);

        return new PluginCandidate(
            manifest,
            manifestPath,
            Path.GetDirectoryName(manifestPath)
                ?? throw new InvalidOperationException(
                    $"Manifest has no directory: {manifestPath}"));
    }

    private static void ValidateManifest(
        PluginManifest manifest,
        string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
            throw ManifestError(manifestPath, "id is required.");

        if (string.IsNullOrWhiteSpace(manifest.Name))
            throw ManifestError(manifestPath, "name is required.");

        if (string.IsNullOrWhiteSpace(manifest.Version))
            throw ManifestError(manifestPath, "version is required.");

        if (manifest.ApiVersion != PluginApi.Version)
        {
            throw ManifestError(
                manifestPath,
                $"API version {manifest.ApiVersion} is not supported. " +
                $"Host API version is {PluginApi.Version}.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Assembly))
            throw ManifestError(manifestPath, "assembly is required.");

        if (string.IsNullOrWhiteSpace(manifest.EntryType))
            throw ManifestError(manifestPath, "entryType is required.");

        if (manifest.Dependencies.Any(string.IsNullOrWhiteSpace))
        {
            throw ManifestError(
                manifestPath,
                "dependencies cannot contain empty IDs.");
        }
    }

    private static Exception ManifestError(
        string manifestPath,
        string message)
    {
        return new InvalidDataException(
            $"{manifestPath}: {message}");
    }

    private static void ValidateUniqueIds(
        IReadOnlyList<PluginCandidate> candidates)
    {
        var duplicate =
            candidates
                .GroupBy(
                    x => x.Manifest.Id,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate plugin ID '{duplicate.Key}'.");
        }
    }

    private static void ValidateDependencies(
        IReadOnlyList<PluginCandidate> candidates)
    {
        var ids =
            candidates
                .Select(x => x.Manifest.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            foreach (var dependency in candidate.Manifest.Dependencies)
            {
                if (!ids.Contains(dependency))
                {
                    throw new InvalidOperationException(
                        $"Plugin '{candidate.Manifest.Id}' requires " +
                        $"missing plugin '{dependency}'.");
                }
            }
        }
    }

    private static IReadOnlyList<PluginCandidate> OrderByDependencies(
        IReadOnlyList<PluginCandidate> candidates)
    {
        var byId =
            candidates.ToDictionary(
                x => x.Manifest.Id,
                StringComparer.OrdinalIgnoreCase);

        var states =
            new Dictionary<string, VisitState>(
                StringComparer.OrdinalIgnoreCase);

        var result =
            new List<PluginCandidate>();

        foreach (var candidate in candidates)
        {
            Visit(candidate);
        }

        return result;

        void Visit(PluginCandidate candidate)
        {
            var id = candidate.Manifest.Id;

            if (states.TryGetValue(id, out var state))
            {
                if (state == VisitState.Visited)
                    return;

                if (state == VisitState.Visiting)
                {
                    throw new InvalidOperationException(
                        $"Plugin dependency cycle detected at '{id}'.");
                }
            }

            states[id] = VisitState.Visiting;

            foreach (var dependencyId in candidate.Manifest.Dependencies)
            {
                Visit(byId[dependencyId]);
            }

            states[id] = VisitState.Visited;
            result.Add(candidate);
        }
    }

    private static string ResolveAssemblyPath(
        PluginCandidate candidate)
    {
        var pluginDirectory =
            Path.GetFullPath(candidate.PluginDirectory);

        var assemblyPath =
            Path.GetFullPath(
                Path.Combine(
                    pluginDirectory,
                    candidate.Manifest.Assembly));

        var relative =
            Path.GetRelativePath(
                pluginDirectory,
                assemblyPath);

        if (relative == ".."
            || relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                $"Plugin '{candidate.Manifest.Id}' assembly path " +
                "escapes its plugin directory.");
        }

        return assemblyPath;
    }

    private sealed record PluginCandidate(
        PluginManifest Manifest,
        string ManifestPath,
        string PluginDirectory);

    private enum VisitState
    {
        Visiting,
        Visited
    }
}
