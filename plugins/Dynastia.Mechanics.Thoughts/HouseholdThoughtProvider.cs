using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class HouseholdThoughtProvider :
    IThoughtProvider
{
    public string Id =>
        "thoughts.household";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        var head =
            context.Households.ResolveHouseholdHead(
                person);

        var status =
            head is null
                ? null
                : context.Households.GetStatus(
                    head);

        if (status?.IsBroke
            == true)
        {
            yield return new ThoughtCandidate(
                "economy.broke",
                "economy.poverty",
                "economy.poverty",
                person.Age <= 11
                    ? 34
                    : person.Age <= 17
                        ? 52
                        : 74,
                "😟",
                "state",
                "household.broke",
                "economy.broke");
        }

        if (status?.IsLargeFamilyStrained
            == true)
        {
            yield return new ThoughtCandidate(
                "household.strain",
                "household.strain",
                "household.strain",
                person.Age <= 11
                    ? 42
                    : person.Age <= 17
                        ? 52
                        : 68,
                "😫",
                "state",
                "household.strain",
                "household.strain");
        }

        if (person.Age >= 18)
        {
            if (person.Tags.Has(
                "role.family_nanny"))
            {
                yield return new ThoughtCandidate(
                    "role.family_nanny",
                    "household.role",
                    "household.role",
                    38,
                    "🧑‍🍼",
                    "state",
                    "role.family_nanny",
                    "role.family_nanny.started");
            }
            else if (person.Tags.Has(
                "role.nanny"))
            {
                yield return new ThoughtCandidate(
                    "role.nanny",
                    "household.role",
                    "household.role",
                    24,
                    "🧑‍🍼",
                    "state",
                    "role.nanny",
                    "role.nanny");
            }
            else if (context.Career
                .GetCareer(
                    person)
                .JobTitle.Equals(
                    "Housewife",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "role.housewife",
                    "household.role",
                    "household.role",
                    18,
                    "👩‍🍳",
                    "state",
                    "housewife",
                    "role.housewife");
            }
        }

        foreach (var gameEvent in
            context.Events.Where(
                gameEvent =>
                    ThoughtProviderUtilities
                        .IsEventInvolving(
                            gameEvent,
                            person)))
        {
            if (person.Age >= 18)
            {
                if (gameEvent.Type.Equals(
                    "household.house_bought",
                    StringComparison.OrdinalIgnoreCase)
                    && gameEvent.SubjectId
                        == person.Id)
                {
                    yield return EventCandidate(
                        "property.bought",
                        56,
                        "😊",
                        gameEvent);
                }

                if (gameEvent.Type.Equals(
                    "household.house_sold",
                    StringComparison.OrdinalIgnoreCase)
                    && gameEvent.SubjectId
                        == person.Id)
                {
                    yield return EventCandidate(
                        "property.sold",
                        42,
                        "🏠",
                        gameEvent);
                }

                if (gameEvent.Type.Equals(
                    "household.house_given",
                    StringComparison.OrdinalIgnoreCase)
                    && gameEvent.RelatedPersonIds.Contains(
                        person.Id))
                {
                    yield return EventCandidate(
                        "property.given",
                        55,
                        "🏡",
                        gameEvent);
                }

                if (gameEvent.Type.Equals(
                    "household.house_promised",
                    StringComparison.OrdinalIgnoreCase)
                    && gameEvent.RelatedPersonIds.Contains(
                        person.Id))
                {
                    yield return EventCandidate(
                        "property.promised",
                        46,
                        "🏡",
                        gameEvent);
                }

                if (gameEvent.Type.Equals(
                    "inheritance.promised_houses_received",
                    StringComparison.OrdinalIgnoreCase)
                    && gameEvent.SubjectId
                        == person.Id)
                {
                    yield return EventCandidate(
                        "property.received",
                        64,
                        "🏡",
                        gameEvent);
                }
            }

            if (gameEvent.Type.Equals(
                "household.family_nanny_started",
                StringComparison.OrdinalIgnoreCase)
                && gameEvent.RelatedPersonIds.Contains(
                    person.Id))
            {
                yield return new ThoughtCandidate(
                    "family.nanny.started",
                    "household.role",
                    "household.role",
                    55,
                    "🧑‍🍼",
                    "event",
                    gameEvent.Type,
                    "role.family_nanny");
            }

            if (gameEvent.Type.Equals(
                "household.family_nanny_ended",
                StringComparison.OrdinalIgnoreCase)
                && gameEvent.RelatedPersonIds.Contains(
                    person.Id))
            {
                yield return new ThoughtCandidate(
                    "family.nanny.ended",
                    "household.role",
                    "household.role",
                    35,
                    "👋",
                    "event",
                    gameEvent.Type,
                    "role.family_nanny.ended");
            }

            if (gameEvent.Type.Equals(
                    "household.moved",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "household.moved",
                    "household.move",
                    "household.move",
                    76,
                    "🚚",
                    "event",
                    gameEvent.Type,
                    "household.moved",
                    new Dictionary<string, string>
                    {
                        ["fromTown"] = gameEvent.Data.TryGetValue(
                            "fromTown",
                            out var fromTown)
                                ? fromTown
                                : "our old town",
                        ["toTown"] = gameEvent.Data.TryGetValue(
                            "toTown",
                            out var toTown)
                                ? toTown
                                : "our new town"
                    });
            }

            if (gameEvent.Type.Equals(
                    "family_support.parents_success",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "family_support.child_success",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "support.success",
                    "family.support",
                    "family.support",
                    50,
                    "😌",
                    "event",
                    gameEvent.Type,
                    "support.success",
                    new Dictionary<string, string>
                    {
                        ["supportRole"] =
                            gameEvent.SubjectId == person.Id
                                ? "recipient"
                                : "donor"
                    });
            }

            if (gameEvent.Type.Equals(
                    "family_support.parents_failure",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "family_support.child_failure",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "support.failure",
                    "family.support",
                    "family.support",
                    48,
                    "😒",
                    "event",
                    gameEvent.Type,
                    "support.failure");
            }
        }
    }

    private static ThoughtCandidate EventCandidate(
        string wordingKey,
        int salience,
        string emoji,
        GameEvent gameEvent)
    {
        return new ThoughtCandidate(
            wordingKey,
            "property",
            "property",
            salience,
            emoji,
            "event",
            gameEvent.Type,
            wordingKey);
    }
}
