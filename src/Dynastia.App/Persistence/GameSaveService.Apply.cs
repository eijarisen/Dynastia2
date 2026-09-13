using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynastia.Contracts;

namespace Dynastia.App.Persistence;

public sealed partial class GameSaveService
{
    private void Apply(
        DesktopSaveEnvelope envelope,
        PreparedComponents prepared)
    {
        _actions.RestoreQueuedActions(
            []);

        _events.RestoreEvents(
            []);

        _gameState.ClearPeople();

        _gameState.DynastySurname =
            envelope.DynastySurname;

        _gameState.Year =
            envelope.Year;

        foreach (var savedPerson in
            envelope.People)
        {
            var person =
                _gameState.CreatePerson(
                    savedPerson.Name,
                    savedPerson.Surname,
                    savedPerson.Age,
                    savedPerson.Id);

            person.MaidenName =
                savedPerson.MaidenName;

            person.BirthDate =
                savedPerson.BirthDate;

            person.DeathDate =
                savedPerson.DeathDate;

            foreach (var tag in
                savedPerson.Tags)
            {
                person.Tags.Add(
                    tag);
            }

            if (prepared.ByPerson.TryGetValue(
                person.Id,
                out var components))
            {
                foreach (var component in
                    components)
                {
                    person.Components.Set(
                        component.Type,
                        component.Value);
                }
            }
        }

        var restoredEvents =
            envelope.Events
                .Select(
                    saved =>
                        saved.ToEvent())
                .ToList();

        _events.RestoreEvents(
            restoredEvents);

        if (_biography is not null)
        {
            var biography =
                envelope.Biography
                    .ToDictionary(
                        saved =>
                            saved.PersonId,
                        saved =>
                            (IReadOnlyList<BiographyEntry>)
                                saved.Entries.ToList());

            _biography.RestoreBiographyState(
                biography);
        }

        var queue =
            envelope.QueuedActions
                .Select(
                    saved =>
                        saved.ToAction())
                .ToList();

        _actions.RestoreQueuedActions(
            queue);

        _selection.SelectedPersonId =
            envelope.SelectedPersonId;

        _succession.Refresh();

        if (envelope.ActiveControllerId
            is Guid activeId)
        {
            var active =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == activeId);

            if (active is not null
                && _succession.IsControllable(
                    active))
            {
                _succession.SetActiveController(
                    active);
            }
        }

        // A selected person may legally be spouse/child/deceased.
        // Restore it after controller selection, because switching
        // controller also selects that controller.
        if (envelope.SelectedPersonId
            is Guid selectedId
            && _gameState.People.Any(
                person =>
                    person.Id == selectedId))
        {
            _selection.SelectedPersonId =
                selectedId;
        }
    }

    private static void ValidateEnvelope(
        DesktopSaveEnvelope envelope)
    {
        if (!envelope.IsDynastiaSave)
        {
            throw new InvalidDataException(
                "This is not a Dynastia save file.");
        }

        if (!string.Equals(
                envelope.SaveKind,
                "DynastiaDesktop",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "This Dynastia save belongs to an unsupported edition.");
        }

        if (envelope.FormatVersion <= 0
            || envelope.FormatVersion
                > CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported save format version " +
                $"{envelope.FormatVersion}.");
        }

        if (string.IsNullOrWhiteSpace(
            envelope.DynastySurname))
        {
            throw new InvalidDataException(
                "The save file has no dynasty surname.");
        }

        if (envelope.Year < 1)
        {
            throw new InvalidDataException(
                "The save file contains an invalid year.");
        }

        if (envelope.People.Count == 0)
        {
            throw new InvalidDataException(
                "The save file contains no people.");
        }

        var ids =
            new HashSet<Guid>();

        foreach (var person in
            envelope.People)
        {
            if (person.Id == Guid.Empty
                || !ids.Add(person.Id))
            {
                throw new InvalidDataException(
                    "The save file contains duplicate or invalid person IDs.");
            }

            if (string.IsNullOrWhiteSpace(
                    person.Name)
                || string.IsNullOrWhiteSpace(
                    person.Surname)
                || person.Age < 0)
            {
                throw new InvalidDataException(
                    $"Person {person.Id} has invalid identity data.");
            }

            foreach (var component in
                person.Components)
            {
                if (string.IsNullOrWhiteSpace(
                        component.AssemblyName)
                    || string.IsNullOrWhiteSpace(
                        component.TypeName)
                    || string.IsNullOrWhiteSpace(
                        component.Json))
                {
                    throw new InvalidDataException(
                        $"Person {person.Id} contains an invalid component record.");
                }
            }
        }

        if (envelope.SelectedPersonId
                is Guid selectedId
            && !ids.Contains(
                selectedId))
        {
            throw new InvalidDataException(
                "Selected person is missing from the save file.");
        }

        if (envelope.ActiveControllerId
                is Guid activeId
            && !ids.Contains(
                activeId))
        {
            throw new InvalidDataException(
                "Active household head is missing from the save file.");
        }

        foreach (var queued in
            envelope.QueuedActions)
        {
            if (!ids.Contains(
                    queued.ActorId)
                || !ids.Contains(
                    queued.TargetId))
            {
                throw new InvalidDataException(
                    $"Queued action '{queued.ActionId}' " +
                    "references a missing person.");
            }
        }

        foreach (var biography in
            envelope.Biography)
        {
            if (!ids.Contains(
                biography.PersonId))
            {
                throw new InvalidDataException(
                    "Biography references a missing person.");
            }
        }
    }

}
