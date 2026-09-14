using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynastia.Contracts;

namespace Dynastia.App.Persistence;

public sealed partial class GameSaveService
{
    private sealed record PreparedComponent(
        Type Type,
        object Value);

    private sealed record PreparedComponents(
        IReadOnlyDictionary<
            Guid,
            IReadOnlyList<PreparedComponent>>
            ByPerson);

    private sealed class DesktopSaveEnvelope
    {
        public bool IsDynastiaSave { get; set; }

        public int FormatVersion { get; set; }

        public string SaveKind { get; set; } =
            string.Empty;

        public string DynastySurname { get; set; } =
            string.Empty;

        public int Year { get; set; }

        public int StartYear { get; set; }

        public Guid? SelectedPersonId { get; set; }

        public Guid? ActiveControllerId { get; set; }

        public int AlbumYear { get; set; }

        public bool IsLivingFamilyView { get; set; } =
            true;

        public int DetailsTabIndex { get; set; }

        public List<PersonSaveData> People { get; set; } =
            [];

        public List<GameEventSaveData> Events { get; set; } =
            [];

        public List<QueuedActionSaveData> QueuedActions { get; set; } =
            [];

        public List<BiographyPersonSaveData> Biography { get; set; } =
            [];
    }

    private sealed class PersonSaveData
    {
        public Guid Id { get; set; }

        public string Name { get; set; } =
            string.Empty;

        public string Surname { get; set; } =
            string.Empty;

        public string? MaidenName { get; set; }

        public int Age { get; set; }

        public GameDate? BirthDate { get; set; }

        public GameDate? DeathDate { get; set; }

        public List<string> Tags { get; set; } =
            [];

        public List<ComponentSaveData> Components { get; set; } =
            [];
    }

    private sealed class ComponentSaveData
    {
        public string AssemblyName { get; set; } =
            string.Empty;

        public string TypeName { get; set; } =
            string.Empty;

        public string Json { get; set; } =
            string.Empty;
    }

    private sealed class GameEventSaveData
    {
        public string Type { get; set; } =
            string.Empty;

        public int Year { get; set; }

        public Guid? SubjectId { get; set; }

        public List<Guid> RelatedPersonIds { get; set; } =
            [];

        public Dictionary<string, string> Data { get; set; } =
            [];

        public static GameEventSaveData FromEvent(
            GameEvent gameEvent)
        {
            return new GameEventSaveData
            {
                Type =
                    gameEvent.Type,

                Year =
                    gameEvent.Year,

                SubjectId =
                    gameEvent.SubjectId,

                RelatedPersonIds =
                    gameEvent.RelatedPersonIds.ToList(),

                Data =
                    gameEvent.Data.ToDictionary(
                        pair =>
                            pair.Key,
                        pair =>
                            pair.Value)
            };
        }

        public GameEvent ToEvent()
        {
            return new GameEvent
            {
                Type =
                    Type,

                Year =
                    Year,

                SubjectId =
                    SubjectId,

                RelatedPersonIds =
                    RelatedPersonIds.ToList(),

                Data =
                    Data.ToDictionary(
                        pair =>
                            pair.Key,
                        pair =>
                            pair.Value)
            };
        }
    }

    private sealed class QueuedActionSaveData
    {
        public string ActionId { get; set; } =
            string.Empty;

        public string Label { get; set; } =
            string.Empty;

        public YearPhase Phase { get; set; }

        public Guid ActorId { get; set; }

        public Guid TargetId { get; set; }

        public Dictionary<string, string> Parameters { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public static QueuedActionSaveData FromAction(
            QueuedActionInfo action)
        {
            return new QueuedActionSaveData
            {
                ActionId =
                    action.ActionId,

                Label =
                    action.Label,

                Phase =
                    action.Phase,

                ActorId =
                    action.ActorId,

                TargetId =
                    action.TargetId,

                Parameters = action.Parameters is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : action.Parameters.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.OrdinalIgnoreCase)
            };
        }

        public QueuedActionInfo ToAction()
        {
            return new QueuedActionInfo(
                ActionId,
                Label,
                Phase,
                ActorId,
                TargetId,
                Parameters: Parameters);
        }
    }

    private sealed class BiographyPersonSaveData
    {
        public Guid PersonId { get; set; }

        public List<BiographyEntry> Entries { get; set; } =
            [];
    }
}
