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

        if (status?.HasUnfundedBasicNeeds
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
                             ThoughtMoodIds.Concerned,
                             "💰",
                             ThoughtSalienceTraits.Negative
                             | ThoughtSalienceTraits.ImmediateProblem
                             | ThoughtSalienceTraits.Poverty,
                             "state",
                             "household.broke",
                             "economy.broke"
                         );
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
                             ThoughtMoodIds.Exhausted,
                             "🏠",
                             ThoughtSalienceTraits.HouseholdStrain,
                             "state",
                             "household.strain",
                             "household.strain"
                         );
        }

        if (status?.IsOvercrowded
            == true)
        {
            yield return new ThoughtCandidate(
                             "household.overcrowded",
                             "household.overcrowded",
                             "household.overcrowded",
                             person.Age <= 11
                             ? 42
                             : person.Age <= 17
                             ? 52
                             : 68,
                             ThoughtMoodIds.Neutral,
                             "🏠",
                             ThoughtSalienceTraits.None,
                             "state",
                             "household.overcrowded",
                             "household.overcrowded"
                         );
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
                                 ThoughtMoodIds.Neutral,
                                 "🧑‍🍼",
                                 ThoughtSalienceTraits.None,
                                 "state",
                                 "role.family_nanny",
                                 "role.family_nanny.started"
                             );
            }
            else if (person.Tags.Has(
                "role.nanny"))
            {
                yield return new ThoughtCandidate(
                                 "role.nanny",
                                 "household.role",
                                 "household.role",
                                 24,
                                 ThoughtMoodIds.Neutral,
                                 "🧑‍🍼",
                                 ThoughtSalienceTraits.None,
                                 "state",
                                 "role.nanny",
                                 "role.nanny"
                             );
            }
            else if (context.Career
                .GetCareer(
                    person)
                .StatusId?.Equals(
                    "status.housewife",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                yield return new ThoughtCandidate(
                                 "role.housewife",
                                 "household.role",
                                 "household.role",
                                 18,
                                 ThoughtMoodIds.Neutral,
                                 "👩‍🍳",
                                 ThoughtSalienceTraits.None,
                                 "state",
                                 "housewife",
                                 "role.housewife"
                             );
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
            if (gameEvent.Type.Equals(
                    "farming.income",
                    StringComparison.OrdinalIgnoreCase)
                && gameEvent.RelatedPersonIds.Contains(person.Id))
            {
                var performance = gameEvent.Data.TryGetValue(
                    "performance",
                    out var storedPerformance)
                        ? storedPerformance
                        : "ordinary";

                yield return new ThoughtCandidate(
                                 "farming.work",
                                 "household.farming",
                                 "household.farming",
                                 performance.Equals("ordinary", StringComparison.OrdinalIgnoreCase)
                                 ? 10
                                 : 18,
                                 ThoughtMoodIds.Neutral,
                                 "🌾",
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 gameEvent.Type,
                                 ResolvePerformanceWordingKey(
                                     "farming.work",
                                     performance),
                                 new Dictionary<string, string>
                                 {
                                 ["performance"] = performance
                                 }
                             );
            }

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
                                 ThoughtMoodIds.Neutral,
                                 "🧑‍🍼",
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 gameEvent.Type,
                                 "role.family_nanny"
                             );
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
                                 ThoughtMoodIds.Neutral,
                                 "👋",
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 gameEvent.Type,
                                 "role.family_nanny.ended"
                             );
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
                                 ThoughtMoodIds.Neutral,
                                 "🚚",
                                 ThoughtSalienceTraits.None,
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
                                 }
                             );
            }

            if (gameEvent.Type.Equals(
                    "family_support.parents_success",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "family_support.child_success",
                    StringComparison.OrdinalIgnoreCase))
            {
                var supportRole =
                    gameEvent.SubjectId == person.Id
                        ? "recipient"
                        : "donor";

                yield return new ThoughtCandidate(
                                 "support.success",
                                 "family.support",
                                 "family.support",
                                 50,
                                 ThoughtMoodIds.Relieved,
                                 "👪",
                                 ThoughtSalienceTraits.Positive,
                                 "event",
                                 gameEvent.Type,
                                 ResolveSupportWordingKey(
                                     person,
                                     supportRole),
                                 new Dictionary<string, string>
                                 {
                                 ["supportRole"] = supportRole
                                 }
                             );
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
                                 ThoughtMoodIds.Concerned,
                                 "👪",
                                 ThoughtSalienceTraits.Negative,
                                 "event",
                                 gameEvent.Type,
                                 "support.failure"
                             );
            }
        }
    }

    private static string ResolveSupportWordingKey(
        IPerson person,
        string supportRole)
    {
        if (supportRole.Equals("recipient", StringComparison.OrdinalIgnoreCase))
            return "support.success.recipient";

        if (person.Tags.Has("morals.good"))
            return "support.success.donor.good";

        if (person.Tags.Has("morals.evil"))
            return "support.success.donor.evil";

        return "support.success.donor.neutral";
    }

    private static string ResolvePerformanceWordingKey(
        string prefix,
        string performance) =>
        performance.Equals("strong", StringComparison.OrdinalIgnoreCase)
            ? $"{prefix}.strong"
            : performance.Equals("poor", StringComparison.OrdinalIgnoreCase)
                ? $"{prefix}.poor"
                : $"{prefix}.ordinary";

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
                   ThoughtMoodIds.Neutral,
                   emoji,
                   ThoughtSalienceTraits.None,
                   "event",
                   gameEvent.Type,
                   wordingKey
               );
    }
}
