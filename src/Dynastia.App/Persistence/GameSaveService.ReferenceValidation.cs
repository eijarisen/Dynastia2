using System.Collections;
using System.Reflection;
using Dynastia.Contracts;

namespace Dynastia.App.Persistence;

public sealed partial class GameSaveService
{

    private static void RepairPreparedSpouseReferences(
        DesktopSaveEnvelope envelope,
        PreparedComponents prepared)
    {
        var families =
            envelope.People
                .Select(person =>
                    new
                    {
                        Person = person,
                        Family = prepared.ByPerson
                            .TryGetValue(person.Id, out var components)
                            ? components.FirstOrDefault(component =>
                                component.Type
                                    .GetCustomAttribute<PersistedComponentIdAttribute>()
                                    ?.Id
                                    .Equals(
                                        "family.person",
                                        StringComparison.OrdinalIgnoreCase)
                                == true)
                            : null
                    })
                .Where(entry => entry.Family is not null)
                .ToDictionary(
                    entry => entry.Person.Id,
                    entry => entry.Family!);

        if (families.Count == 0)
            return;

        var original =
            families.ToDictionary(
                pair => pair.Key,
                pair => ReadFamilyReferences(pair.Value).SpouseId);

        var resolved =
            families.Keys.ToDictionary(
                id => id,
                _ => (Guid?)null);

        var assigned =
            new HashSet<Guid>();

        // Reciprocal links are already unambiguous and always win.
        foreach (var personId in families.Keys.OrderBy(id => id))
        {
            if (assigned.Contains(personId)
                || original[personId] is not Guid spouseId
                || !families.ContainsKey(spouseId)
                || original[spouseId] != personId
                || assigned.Contains(spouseId))
            {
                continue;
            }

            resolved[personId] = spouseId;
            resolved[spouseId] = personId;
            assigned.Add(personId);
            assigned.Add(spouseId);
        }

        // A common legacy/runtime inconsistency is a one-sided current-spouse
        // link. Repair it only when exactly one unassigned person claims an
        // otherwise-unassigned person whose own spouse link is empty. If two
        // people claim the same free person, the state is ambiguous and all
        // conflicting one-sided links are cleared rather than inventing a
        // relationship.
        var unilateralClaims =
            original
                .Where(pair =>
                    !assigned.Contains(pair.Key)
                    && pair.Value is Guid spouseId
                    && families.ContainsKey(spouseId)
                    && !assigned.Contains(spouseId)
                    && original[spouseId] is null)
                .GroupBy(pair => pair.Value!.Value)
                .Where(group => group.Count() == 1)
                .Select(group =>
                    (PersonId: group.Single().Key,
                     SpouseId: group.Key))
                .OrderBy(pair => pair.PersonId)
                .ToList();

        foreach (var claim in unilateralClaims)
        {
            if (assigned.Contains(claim.PersonId)
                || assigned.Contains(claim.SpouseId))
            {
                continue;
            }

            resolved[claim.PersonId] = claim.SpouseId;
            resolved[claim.SpouseId] = claim.PersonId;
            assigned.Add(claim.PersonId);
            assigned.Add(claim.SpouseId);
        }

        foreach (var (personId, family) in families)
        {
            SetFamilySpouseId(
                family,
                resolved[personId]);
        }

        foreach (var person in envelope.People)
        {
            if (!resolved.TryGetValue(person.Id, out var spouseId)
                || !person.Tags.Any(tag =>
                    tag.Equals(
                        "state.alive",
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            SetRelationshipTagState(
                person.Tags,
                spouseId is not null);
        }
    }

    private static void SetFamilySpouseId(
        PreparedComponent prepared,
        Guid? spouseId)
    {
        var property =
            prepared.Type.GetProperty("SpouseId")
            ?? throw new InvalidDataException(
                "Persisted family component has no 'SpouseId' property.");

        if (!property.CanWrite)
        {
            throw new InvalidDataException(
                "Persisted family component has a read-only 'SpouseId' property.");
        }

        property.SetValue(
            prepared.Value,
            spouseId);
    }

    private static void SetRelationshipTagState(
        List<string> tags,
        bool married)
    {
        tags.RemoveAll(tag =>
            tag.Equals(
                married
                    ? "relationship.single"
                    : "relationship.married",
                StringComparison.OrdinalIgnoreCase));

        var required =
            married
                ? "relationship.married"
                : "relationship.single";

        if (!tags.Any(tag =>
            tag.Equals(
                required,
                StringComparison.OrdinalIgnoreCase)))
        {
            tags.Add(required);
        }
    }

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
