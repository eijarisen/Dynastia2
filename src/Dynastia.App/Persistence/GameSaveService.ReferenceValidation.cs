using System.Collections;
using System.Reflection;
using Dynastia.Contracts;

namespace Dynastia.App.Persistence;

public sealed partial class GameSaveService
{
    private static void ValidatePreparedReferences(
        DesktopSaveEnvelope envelope,
        PreparedComponents prepared)
    {
        var personIds = envelope.People
            .Select(person => person.Id)
            .ToHashSet();

        var familyStates =
            new Dictionary<Guid, FamilyReferenceState>();

        foreach (var person in envelope.People)
        {
            if (!prepared.ByPerson.TryGetValue(person.Id, out var components))
            {
                throw new InvalidDataException(
                    $"Person {person.Id} has no prepared component state.");
            }

            var family = components.FirstOrDefault(component =>
                component.Type
                    .GetCustomAttribute<PersistedComponentIdAttribute>()
                    ?.Id
                    .Equals(
                        "family.person",
                        StringComparison.OrdinalIgnoreCase)
                == true);

            if (family is null)
            {
                throw new InvalidDataException(
                    $"Person {person.Id} is missing required family state.");
            }

            var state = ReadFamilyReferences(family);
            familyStates[person.Id] = state;

            ValidateOptionalReference(person.Id, "father", state.FatherId, personIds);
            ValidateOptionalReference(person.Id, "mother", state.MotherId, personIds);
            ValidateOptionalReference(person.Id, "spouse", state.SpouseId, personIds);

            foreach (var childId in state.ChildrenIds)
            {
                if (!personIds.Contains(childId))
                {
                    throw new InvalidDataException(
                        $"Person {person.Id} references missing child {childId}.");
                }
            }
        }

        foreach (var (personId, state) in familyStates)
        {
            if (state.SpouseId is Guid spouseId)
            {
                if (!familyStates.TryGetValue(spouseId, out var spouse)
                    || spouse.SpouseId != personId)
                {
                    throw new InvalidDataException(
                        $"Spouse references for {personId} and {spouseId} are inconsistent.");
                }
            }

            if (state.FatherId is Guid fatherId
                && !familyStates[fatherId].ChildrenIds.Contains(personId))
            {
                throw new InvalidDataException(
                    $"Father/child references for {personId} and {fatherId} are inconsistent.");
            }

            if (state.MotherId is Guid motherId
                && !familyStates[motherId].ChildrenIds.Contains(personId))
            {
                throw new InvalidDataException(
                    $"Mother/child references for {personId} and {motherId} are inconsistent.");
            }

            foreach (var childId in state.ChildrenIds)
            {
                var child = familyStates[childId];
                if (child.FatherId != personId
                    && child.MotherId != personId)
                {
                    throw new InvalidDataException(
                        $"Child/parent references for {personId} and {childId} are inconsistent.");
                }
            }
        }
    }

    private static FamilyReferenceState ReadFamilyReferences(
        PreparedComponent prepared)
    {
        var type = prepared.Type;
        var value = prepared.Value;

        Guid? GetGuid(string propertyName)
        {
            var property = type.GetProperty(propertyName)
                ?? throw new InvalidDataException(
                    $"Persisted family component has no '{propertyName}' property.");

            var raw = property.GetValue(value);
            if (raw is null)
                return null;
            if (raw is Guid guid)
                return guid;

            throw new InvalidDataException(
                $"Persisted family property '{propertyName}' has an invalid value.");
        }

        var childrenProperty = type.GetProperty("ChildrenIds")
            ?? throw new InvalidDataException(
                "Persisted family component has no 'ChildrenIds' property.");

        var children = new HashSet<Guid>();
        if (childrenProperty.GetValue(value) is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is Guid id)
                    children.Add(id);
            }
        }

        return new FamilyReferenceState(
            GetGuid("FatherId"),
            GetGuid("MotherId"),
            GetGuid("SpouseId"),
            children);
    }

    private static void ValidateOptionalReference(
        Guid ownerId,
        string role,
        Guid? referencedId,
        IReadOnlySet<Guid> personIds)
    {
        if (referencedId is Guid id
            && !personIds.Contains(id))
        {
            throw new InvalidDataException(
                $"Person {ownerId} references missing {role} {id}.");
        }
    }

    private sealed record FamilyReferenceState(
        Guid? FatherId,
        Guid? MotherId,
        Guid? SpouseId,
        IReadOnlySet<Guid> ChildrenIds);
}
