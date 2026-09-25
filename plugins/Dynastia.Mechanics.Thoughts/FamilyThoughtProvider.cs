using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class FamilyThoughtProvider :
    IThoughtProvider
{
    private readonly IPersonLookup? _people;

    public FamilyThoughtProvider(
        IPersonLookup? people)
    {
        _people = people;
    }

    public string Id =>
        "thoughts.family";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        foreach (var candidate in
            CurrentFamilyEvents(
                person,
                context))
        {
            yield return candidate;
        }

        if (person.Age < 18)
        {
            if (person.Tags.Has(
                "trait.orphan"))
            {
                yield return new ThoughtCandidate(
                                 "orphan.ongoing",
                                 "family.orphanhood",
                                 "family.orphanhood",
                                 person.Age <= 11
                                 ? 72
                                 : 66,
                                 ThoughtMoodIds.Grieving,
                                 "👪",
                                 ThoughtSalienceTraits.Emotional
                                 | ThoughtSalienceTraits.Negative
                                 | ThoughtSalienceTraits.Orphanhood,
                                 "state",
                                 "trait.orphan",
                                 "orphan.ongoing"
                             );
            }

            if (person.Tags.Has(
                "residence.orphanage"))
            {
                yield return new ThoughtCandidate(
                                 "orphanage",
                                 "family.placement",
                                 "family.placement",
                                 86,
                                 ThoughtMoodIds.Grieving,
                                 "👪",
                                 ThoughtSalienceTraits.Emotional
                                 | ThoughtSalienceTraits.Negative
                                 | ThoughtSalienceTraits.Placement,
                                 "state",
                                 "residence.orphanage",
                                 "orphanage"
                             );
            }

            if (person.Tags.Has(
                "residence.adopted"))
            {
                yield return new ThoughtCandidate(
                                 "placement.ongoing",
                                 "family.placement",
                                 "family.placement",
                                 38,
                                 ThoughtMoodIds.Neutral,
                                 "👪",
                                 ThoughtSalienceTraits.Placement,
                                 "state",
                                 "residence.adopted",
                                 "placement.ongoing"
                             );
            }

            if (person.Tags.Has(
                    "residence.independent")
                || person.Tags.Has(
                    "household.independent_orphan"))
            {
                yield return new ThoughtCandidate(
                                 "orphan.independent",
                                 "family.orphanhood",
                                 "family.orphanhood",
                                 78,
                                 ThoughtMoodIds.Concerned,
                                 "👪",
                                 ThoughtSalienceTraits.Emotional
                                 | ThoughtSalienceTraits.Negative
                                 | ThoughtSalienceTraits.Orphanhood,
                                 "state",
                                 "household.independent_orphan",
                                 "orphan.independent"
                             );
            }

            if (person.Tags.Has(
                "state.parents_divorced"))
            {
                var current =
                    context.Events.Any(
                        gameEvent =>
                            IsDivorceEvent(
                                gameEvent.Type)
                            && IsChildOfBoth(
                                person,
                                gameEvent,
                                context));

                yield return new ThoughtCandidate(
                                 current
                                 ? "parents.divorced.current"
                                 : "parents.divorced.ongoing",
                                 "family.parents_divorced",
                                 "family.parents_divorced",
                                 current
                                 ? 88
                                 : 62,
                                 current ? ThoughtMoodIds.Distressed : ThoughtMoodIds.Sad,
                                 "👪",
                                 ThoughtSalienceTraits.Emotional
                                 | ThoughtSalienceTraits.Negative
                                 | ThoughtSalienceTraits.ImmediateProblem
                                 | ThoughtSalienceTraits.MelancholicHighImpact
                                 | ThoughtSalienceTraits.ParentsDivorced,
                                 current
                                 ? "event"
                                 : "state",
                                 current
                                 ? "relationship.divorce"
                                 : "state.parents_divorced",
                                 "parents.divorced"
                             );
            }
        }

        if (person.Tags.Has(
            "recent.bereavement")
            && !context.Events.Any(
                gameEvent =>
                    gameEvent.Type.Equals(
                        "life.death",
                        StringComparison.OrdinalIgnoreCase)
                    && IsCurrentLossFor(
                        person,
                        gameEvent,
                        context)))
        {
            yield return new ThoughtCandidate(
                             "bereavement.recent",
                             "family.loss",
                             "family.bereavement",
                             82,
                             ThoughtMoodIds.Grieving,
                             "👪",
                             ThoughtSalienceTraits.Emotional
                             | ThoughtSalienceTraits.Negative
                             | ThoughtSalienceTraits.MelancholicHighImpact
                             | ThoughtSalienceTraits.FamilyLoss,
                             "state",
                             "recent.bereavement",
                             "family.bereavement.recent"
                         );
        }
    }

    private IEnumerable<ThoughtCandidate>
        CurrentFamilyEvents(
            IPerson person,
            ThoughtContext context)
    {
        foreach (var gameEvent in
            context.Events)
        {
            if (gameEvent.Type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase))
            {
                if (gameEvent.SubjectId
                    is not Guid deceasedId)
                {
                    continue;
                }

                var deceased =
                    FindPerson(
                        context,
                        deceasedId);

                if (deceased is null)
                    continue;

                var relation =
                    ThoughtProviderUtilities
                        .LossRelation(
                            person,
                            deceased,
                            context.Family);

                if (relation is null)
                    continue;

                yield return new ThoughtCandidate(
                                 $"loss:{deceased.Id}",
                                 $"family.loss:{deceased.Id}",
                                 $"family.loss:{deceased.Id}",
                                 relation.Value.Salience,
                                 ThoughtMoodIds.Grieving,
                                 "👪",
                                 ThoughtSalienceTraits.Emotional
                                 | ThoughtSalienceTraits.Negative
                                 | ThoughtSalienceTraits.FamilyLoss,
                                 "event",
                                 gameEvent.Type,
                                 "family.loss.current",
                                 ThoughtProviderUtilities.Context(
                                 (
                                 "relation",
                                 relation.Value.Relation
                                 ),
                                 (
                                 "relationPossessive",
                                 relation.Value.RelationPossessive
                                 ))
                             );
            }

            if (gameEvent.Type.Equals(
                "adoption.orphaned",
                StringComparison.OrdinalIgnoreCase)
                && gameEvent.SubjectId
                    == person.Id
                && person.Age < 18)
            {
                yield return new ThoughtCandidate(
                                 "orphan.new",
                                 "family.orphanhood",
                                 "family.orphanhood",
                                 98,
                                 ThoughtMoodIds.Grieving,
                                 "👪",
                                 ThoughtSalienceTraits.Emotional
                                 | ThoughtSalienceTraits.Negative
                                 | ThoughtSalienceTraits.Orphanhood,
                                 "event",
                                 gameEvent.Type,
                                 "orphan.new"
                             );
            }

            if (gameEvent.Type.Equals(
                "adoption.placed",
                StringComparison.OrdinalIgnoreCase)
                && gameEvent.SubjectId
                    == person.Id
                && person.Age < 18)
            {
                yield return new ThoughtCandidate(
                                 "placement.new",
                                 "family.placement",
                                 "family.placement",
                                 80,
                                 ThoughtMoodIds.Neutral,
                                 "👪",
                                 ThoughtSalienceTraits.Placement,
                                 "event",
                                 gameEvent.Type,
                                 "placement.new"
                             );
            }

            if (gameEvent.Type.Equals(
                    "life.birth",
                    StringComparison.OrdinalIgnoreCase)
                && gameEvent.SubjectId
                    is Guid newbornId)
            {
                var newborn =
                    FindPerson(
                        context,
                        newbornId);

                if (newborn is null)
                    continue;

                if (gameEvent.RelatedPersonIds.Contains(
                        person.Id)
                    && person.Age >= 18)
                {
                    yield return new ThoughtCandidate(
                                     $"birth.parent:{newborn.Id}",
                                     "family.birth",
                                     "family.birth",
                                     84,
                                     ThoughtMoodIds.Pleased,
                                     "👪",
                                     ThoughtSalienceTraits.Emotional
                                     | ThoughtSalienceTraits.Positive
                                     | ThoughtSalienceTraits.FamilyBirth,
                                     "event",
                                     gameEvent.Type,
                                     "birth.parent"
                                 );
                }

                if (person.Age is >= 5 and <= 17
                    && IsSibling(
                        person,
                        newborn,
                        context.Family))
                {
                    yield return new ThoughtCandidate(
                                     $"birth.sibling:{newborn.Id}",
                                     "family.birth",
                                     "family.birth",
                                     person.Age <= 11
                                     ? 68
                                     : 58,
                                     person.Age <= 11 ? ThoughtMoodIds.Happy : ThoughtMoodIds.Neutral,
                                     "👪",
                                     ThoughtSalienceTraits.Emotional
                                     | ThoughtSalienceTraits.Positive
                                     | ThoughtSalienceTraits.FamilyBirth,
                                     "event",
                                     gameEvent.Type,
                                     "birth.sibling"
                                 );
                }
            }
        }
    }

    private IPerson? FindPerson(
        ThoughtContext context,
        Guid id) =>
        _people?.FindPerson(id)
        ?? context.GameState.People.FirstOrDefault(
            candidate => candidate.Id == id);

    private bool IsCurrentLossFor(
        IPerson person,
        GameEvent gameEvent,
        ThoughtContext context)
    {
        if (gameEvent.SubjectId
            is not Guid deceasedId)
        {
            return false;
        }

        var deceased =
            FindPerson(
                context,
                deceasedId);

        return deceased is not null
            && ThoughtProviderUtilities
                .LossRelation(
                    person,
                    deceased,
                    context.Family)
                is not null;
    }

    private static bool IsChildOfBoth(
        IPerson child,
        GameEvent gameEvent,
        ThoughtContext context)
    {
        if (gameEvent.SubjectId
            is not Guid firstId
            || gameEvent.RelatedPersonIds.Count == 0)
        {
            return false;
        }

        var secondId =
            gameEvent.RelatedPersonIds[0];

        var father =
            context.Family.GetFather(
                child)?.Id;

        var mother =
            context.Family.GetMother(
                child)?.Id;

        return (
                father == firstId
                && mother == secondId
            )
            || (
                father == secondId
                && mother == firstId
            );
    }

    private static bool IsSibling(
        IPerson first,
        IPerson second,
        IFamilyService family)
    {
        if (first.Id == second.Id)
            return false;

        var firstFather =
            family.GetFather(
                first)?.Id;

        var firstMother =
            family.GetMother(
                first)?.Id;

        var secondFather =
            family.GetFather(
                second)?.Id;

        var secondMother =
            family.GetMother(
                second)?.Id;

        return (
                firstFather is not null
                && firstFather == secondFather
            )
            || (
                firstMother is not null
                && firstMother == secondMother
            );
    }

    private static bool IsDivorceEvent(
        string type)
    {
        return type.Equals(
                "relationship.divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.low_satisfaction_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.prison_divorce",
                StringComparison.OrdinalIgnoreCase);
    }
}
