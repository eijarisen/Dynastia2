using System.Reflection;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.App.Persistence;

public sealed partial class GameSaveService
{
    private PreparedComponents PrepareComponents(
        DesktopSaveEnvelope envelope)
    {
        var resolvedAssemblies =
            AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .GroupBy(
                    assembly => assembly.GetName().Name ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

        var persistedTypes = BuildPersistedTypeRegistry(
            resolvedAssemblies.Values);

        var byPerson =
            new Dictionary<Guid, IReadOnlyList<PreparedComponent>>();

        foreach (var person in envelope.People)
        {
            var components = new List<PreparedComponent>();

            foreach (var saved in person.Components)
            {
                var type = ResolveComponentType(
                    saved,
                    persistedTypes,
                    resolvedAssemblies);

                object component;

                try
                {
                    component =
                        JsonSerializer.Deserialize(
                            saved.Json,
                            type,
                            ComponentJsonOptions)
                        ?? throw new InvalidDataException(
                            $"Component '{GetSavedComponentName(saved)}' contains no state.");
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException(
                        $"Component '{GetSavedComponentName(saved)}' is corrupted or incompatible.",
                        exception);
                }

                components.Add(
                    new PreparedComponent(
                        type,
                        component));
            }

            byPerson[person.Id] = components;
        }

        return new PreparedComponents(byPerson);
    }

    private static Type ResolveComponentType(
        ComponentSaveData saved,
        IReadOnlyDictionary<string, Type> persistedTypes,
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies)
    {
        if (!string.IsNullOrWhiteSpace(saved.ComponentId)
            && persistedTypes.TryGetValue(saved.ComponentId, out var stableType))
        {
            return stableType;
        }

        // Version-1 desktop saves used CLR assembly/type names. Keep that
        // format as a migration alias, so the first load after this upgrade
        // automatically rewrites the component with its stable ID.
        if (!string.IsNullOrWhiteSpace(saved.TypeName)
            && persistedTypes.TryGetValue(saved.TypeName, out var aliasedType))
        {
            return aliasedType;
        }

        if (!string.IsNullOrWhiteSpace(saved.AssemblyName)
            && resolvedAssemblies.TryGetValue(saved.AssemblyName, out var assembly)
            && !string.IsNullOrWhiteSpace(saved.TypeName))
        {
            var legacyType = assembly.GetType(
                saved.TypeName,
                throwOnError: false,
                ignoreCase: false);

            if (legacyType is not null)
                return legacyType;
        }

        if (!string.IsNullOrWhiteSpace(saved.ComponentId))
        {
            throw new InvalidDataException(
                $"Save file requires component '{saved.ComponentId}', but no loaded plugin provides it.");
        }

        throw new InvalidDataException(
            $"Save file requires component type '{saved.TypeName}' from assembly " +
            $"'{saved.AssemblyName}', but it is unavailable.");
    }

    private static IReadOnlyDictionary<string, Type> BuildPersistedTypeRegistry(
        IEnumerable<Assembly> assemblies)
    {
        var result = new Dictionary<string, Type>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                var metadata =
                    type.GetCustomAttribute<PersistedComponentIdAttribute>();

                if (metadata is null)
                    continue;

                RegisterPersistedAlias(result, metadata.Id, type);

                if (!string.IsNullOrWhiteSpace(type.FullName))
                    RegisterPersistedAlias(result, type.FullName, type);

                foreach (var alias in metadata.Aliases)
                    RegisterPersistedAlias(result, alias, type);
            }
        }

        return result;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null).Cast<Type>();
        }
    }

    private static void RegisterPersistedAlias(
        IDictionary<string, Type> registry,
        string alias,
        Type type)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return;

        if (registry.TryGetValue(alias, out var existing)
            && existing != type)
        {
            throw new InvalidOperationException(
                $"Persisted component identifier '{alias}' is provided by both " +
                $"'{existing.FullName}' and '{type.FullName}'.");
        }

        registry[alias] = type;
    }

    private static string GetSavedComponentName(ComponentSaveData saved) =>
        !string.IsNullOrWhiteSpace(saved.ComponentId)
            ? saved.ComponentId
            : saved.TypeName;
}
