using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                .Where(
                    assembly =>
                        !assembly.IsDynamic)
                .GroupBy(
                    assembly =>
                        assembly.GetName().Name
                        ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First(),
                    StringComparer.OrdinalIgnoreCase);

        var byPerson =
            new Dictionary<
                Guid,
                IReadOnlyList<PreparedComponent>>();

        foreach (var person in
            envelope.People)
        {
            var components =
                new List<PreparedComponent>();

            foreach (var saved in
                person.Components)
            {
                if (!resolvedAssemblies.TryGetValue(
                    saved.AssemblyName,
                    out var assembly))
                {
                    throw new InvalidDataException(
                        $"Save file requires component assembly " +
                        $"'{saved.AssemblyName}', but it is not loaded. " +
                        "The required plugin may be missing.");
                }

                var type =
                    assembly.GetType(
                        saved.TypeName,
                        throwOnError: false,
                        ignoreCase: false)
                    ?? throw new InvalidDataException(
                        $"Save file requires component type " +
                        $"'{saved.TypeName}', but it is unavailable.");

                object component;

                try
                {
                    component =
                        JsonSerializer.Deserialize(
                            saved.Json,
                            type,
                            ComponentJsonOptions)
                        ?? throw new InvalidDataException(
                            $"Component '{saved.TypeName}' " +
                            "contains no state.");
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException(
                        $"Component '{saved.TypeName}' " +
                        "is corrupted or incompatible.",
                        exception);
                }

                components.Add(
                    new PreparedComponent(
                        type,
                        component));
            }

            byPerson[
                person.Id] =
                    components;
        }

        return new PreparedComponents(
            byPerson);
    }

}
