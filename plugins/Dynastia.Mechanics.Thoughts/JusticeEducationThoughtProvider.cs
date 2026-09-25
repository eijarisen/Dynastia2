using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class JusticeEducationThoughtProvider :
    IThoughtProvider
{
    public string Id =>
        "thoughts.justice_education";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        var justice =
            context.Justice.GetStatus(
                person);

        var currentJusticeEvent =
            false;

        foreach (var gameEvent in
            context.Events.Where(
                gameEvent =>
                    gameEvent.SubjectId
                    == person.Id))
        {
            if (gameEvent.Type.Equals(
                "justice.crime",
                StringComparison.OrdinalIgnoreCase))
            {
                currentJusticeEvent =
                    true;

                yield return new ThoughtCandidate(
                                 "justice.new",
                                 "justice",
                                 "justice",
                                 92,
                                 ThoughtMoodIds.Concerned,
                                 "⚖️",
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 gameEvent.Type,
                                 "justice.new"
                             );
            }

            if (gameEvent.Type.Equals(
                "justice.released",
                StringComparison.OrdinalIgnoreCase))
            {
                currentJusticeEvent =
                    true;

                yield return new ThoughtCandidate(
                                 "justice.released",
                                 "justice",
                                 "justice",
                                 80,
                                 ThoughtMoodIds.Relieved,
                                 "⚖️",
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 gameEvent.Type,
                                 "justice.released"
                             );
            }

            if (gameEvent.Type.Equals(
                "rare.wrongful_arrest",
                StringComparison.OrdinalIgnoreCase))
            {
                currentJusticeEvent =
                    true;

                yield return new ThoughtCandidate(
                                 "wrongful.arrest",
                                 "justice",
                                 "justice",
                                 94,
                                 ThoughtMoodIds.Angry,
                                 "⚖️",
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 gameEvent.Type,
                                 "wrongful.arrest"
                             );
            }

            if (gameEvent.Type.Equals(
                    "education.success",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "education.passive",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "education.help_learning_success",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                                 "education.success",
                                 "education",
                                 "education",
                                 person.Age <= 11
                                 ? 50
                                 : person.Age <= 17
                                 ? 54
                                 : 48,
                                 ThoughtMoodIds.Happy,
                                 "🎓",
                                 ThoughtSalienceTraits.Positive
                                 | ThoughtSalienceTraits.Career
                                 | ThoughtSalienceTraits.Education,
                                 "event",
                                 gameEvent.Type,
                                 "education.success"
                             );
            }

            if (gameEvent.Type.Equals(
                    "education.failure",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "education.help_learning_failure",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                                 "education.failure",
                                 "education",
                                 "education",
                                 person.Age <= 11
                                 ? 38
                                 : person.Age <= 17
                                 ? 44
                                 : 38,
                                 ThoughtMoodIds.Concerned,
                                 "🎓",
                                 ThoughtSalienceTraits.Negative
                                 | ThoughtSalienceTraits.Career
                                 | ThoughtSalienceTraits.Education,
                                 "event",
                                 gameEvent.Type,
                                 "education.failure"
                             );
            }

            if (gameEvent.Type.Equals(
                "life.adult",
                StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                                 "life.adult",
                                 "life.stage",
                                 "life.stage",
                                 52,
                                 ThoughtMoodIds.Neutral,
                                 "🎂",
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 gameEvent.Type,
                                 "life.adult"
                             );
            }
        }

        if (justice.IsImprisoned
            && !currentJusticeEvent)
        {
            yield return new ThoughtCandidate(
                             "justice.imprisoned",
                             "justice",
                             "justice",
                             86,
                             ThoughtMoodIds.Concerned,
                             "⚖️",
                             ThoughtSalienceTraits.Negative,
                             "state",
                             "state.imprisoned",
                             "justice.imprisoned"
                         );
        }
    }
}
