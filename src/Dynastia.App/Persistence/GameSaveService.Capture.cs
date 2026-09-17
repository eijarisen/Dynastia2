using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynastia.Contracts;

namespace Dynastia.App.Persistence;

public sealed partial class GameSaveService
{
    private DesktopSaveEnvelope Capture(
        GameUiSaveState uiState)
    {
        var people =
            _gameState.People
                .Select(
                    CapturePerson)
                .ToList();

        var events =
            _events.AllEvents
                .Select(
                    GameEventSaveData.FromEvent)
                .ToList();

        var queued =
            _actions.GetAllQueuedActions()
                .Select(
                    QueuedActionSaveData.FromAction)
                .ToList();

        var biography =
            _biography?
                .ExportBiographyState()
                .Select(
                    pair =>
                        new BiographyPersonSaveData
                        {
                            PersonId =
                                pair.Key,

                            Entries =
                                pair.Value.ToList()
                        })
                .ToList()
            ?? [];

        return new DesktopSaveEnvelope
        {
            IsDynastiaSave =
                true,

            FormatVersion =
                CurrentFormatVersion,

            SaveKind =
                "DynastiaDesktop",

            DynastySurname =
                _gameState.DynastySurname,

            Year =
                _gameState.Year,

            StartYear =
                _gameState.StartYear,

            RandomState =
                _random.CaptureState(),

            SelectedPersonId =
                uiState.SelectedPersonId,

            ActiveControllerId =
                uiState.ActiveControllerId,

            AlbumYear =
                uiState.AlbumYear,

            IsLivingFamilyView =
                uiState.IsLivingFamilyView,

            DetailsTabIndex =
                uiState.DetailsTabIndex,

            People =
                people,

            Events =
                events,

            QueuedActions =
                queued,

            Biography =
                biography
        };
    }

    private static PersonSaveData CapturePerson(
        IPerson person)
    {
        var components =
            new List<ComponentSaveData>();

        foreach (var component in
            person.Components.All)
        {
            var type =
                component.GetType();

            var componentId =
                type.GetCustomAttribute<PersistedComponentIdAttribute>()
                    ?.Id
                ?? throw new InvalidOperationException(
                    $"Persisted component '{type.FullName}' has no stable component ID.");

            var typeName =
                type.FullName
                ?? throw new InvalidOperationException(
                    "Cannot save a component with no type name.");

            var assemblyName =
                type.Assembly
                    .GetName()
                    .Name
                ?? throw new InvalidOperationException(
                    $"Cannot save component '{typeName}' " +
                    "because its assembly has no name.");

            string json;

            try
            {
                json =
                    JsonSerializer.Serialize(
                        component,
                        type,
                        ComponentJsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Failed to serialize component " +
                    $"'{typeName}'.",
                    exception);
            }

            components.Add(
                new ComponentSaveData
                {
                    ComponentId =
                        componentId,

                    AssemblyName =
                        assemblyName,

                    TypeName =
                        typeName,

                    Json =
                        json
                });
        }

        return new PersonSaveData
        {
            Id =
                person.Id,

            Name =
                person.Name,

            Surname =
                person.Surname,

            MaidenName =
                person.MaidenName,

            Age =
                person.Age,

            BirthDate =
                person.BirthDate,

            DeathDate =
                person.DeathDate,

            Tags =
                person.Tags.All
                    .OrderBy(
                        tag =>
                            tag,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList(),

            Components =
                components
        };
    }

}
