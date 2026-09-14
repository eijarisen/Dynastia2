using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class StandardBiographyService
{
    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (gameEvent.Type.Equals(
            "game.started",
            StringComparison.OrdinalIgnoreCase))
        {
            _entries.Clear();

            SeedFounderBiography(
                gameEvent);

            return;
        }

        if (gameEvent.Type.Equals(
                "life.birth",
                StringComparison.OrdinalIgnoreCase)
            && gameEvent.Data.TryGetValue(
                "suppressChronicle",
                out var suppressBirth)
            && suppressBirth.Equals(
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            AddBirthEntries(
                gameEvent);
            return;
        }

        if (!_households.ShouldShowFamilyNews(
            gameEvent))
        {
            return;
        }

        if (gameEvent.Type.Equals(
            "life.birth",
            StringComparison.OrdinalIgnoreCase))
        {
            AddBirthEntries(
                gameEvent);

            return;
        }

        if (gameEvent.Type.Equals(
            "peripheral.birth",
            StringComparison.OrdinalIgnoreCase))
        {
            AddPeripheralBirthEntries(
                gameEvent);

            return;
        }

        var subject =
            FindPerson(
                gameEvent.SubjectId);

        if (subject is null)
            return;

        var message =
            GetEventText(
                gameEvent);

        if (!string.IsNullOrWhiteSpace(
            message))
        {
            var formatted =
                FormatWithEmoji(
                    gameEvent.Type,
                    message);

            AddEntry(
                subject,
                gameEvent.Year,
                formatted);

            if (IsFamilyMoneyEvent(
                gameEvent.Type))
            {
                foreach (var related in
                    gameEvent.RelatedPersonIds
                        .Select(id => FindPerson(id))
                        .Where(person =>
                            person is not null
                            && person.Id != subject.Id)
                        .Cast<IPerson>()
                        .DistinctBy(person => person.Id))
                {
                    AddEntry(
                        related,
                        gameEvent.Year,
                        formatted);
                }
            }
        }

        if (gameEvent.Type.Equals(
            "relationship.married",
            StringComparison.OrdinalIgnoreCase))
        {
            AddMarriageEntryForSpouse(
                gameEvent,
                subject);
        }

        if (IsImportantForRelatives(
            gameEvent.Type))
        {
            AddRelativeEntries(
                gameEvent,
                subject);
        }
    }

    private void AddPeripheralBirthEntries(
        GameEvent gameEvent)
    {
        var message =
            GetEventText(
                gameEvent);

        if (string.IsNullOrWhiteSpace(
            message))
        {
            return;
        }

        var parentIds =
            new List<Guid>();

        if (gameEvent.SubjectId
            is Guid subjectId)
        {
            parentIds.Add(
                subjectId);
        }

        parentIds.AddRange(
            gameEvent.RelatedPersonIds);

        foreach (var parent in
            parentIds
                .Distinct()
                .Select(
                    id =>
                        FindPerson(
                            id))
                .Where(
                    person =>
                        person is not null)
                .Cast<IPerson>())
        {
            AddEntry(
                parent,
                gameEvent.Year,
                $"👶 {message}");
        }
    }

    private void AddBirthEntries(
        GameEvent gameEvent)
    {
        var child =
            FindPerson(
                gameEvent.SubjectId);

        if (child is null)
            return;

        var father =
            FindRelatedPerson(
                gameEvent,
                0);

        var mother =
            FindRelatedPerson(
                gameEvent,
                1);

        if (father is null
            || mother is null)
        {
            var fallback =
                GetEventText(
                    gameEvent);

            if (!string.IsNullOrWhiteSpace(
                fallback))
            {
                AddEntry(
                    child,
                    gameEvent.Year,
                    $"👶 {fallback}");
            }

            return;
        }

        AddEntry(
            child,
            gameEvent.Year,
            $"👶 Was born to " +
            $"{_family.GetDisplayName(father)} and " +
            $"{_family.GetDisplayName(mother)}.");

        var fatherCount =
            GetInt(
                gameEvent,
                "fatherCount",
                _family.GetChildren(
                    father)
                    .Count);

        var motherCount =
            GetInt(
                gameEvent,
                "motherCount",
                _family.GetChildren(
                    mother)
                    .Count);

        AddEntry(
            father,
            gameEvent.Year,
            $"👶 His {ToOrdinalWord(fatherCount)} child, " +
            $"{_family.GetDisplayName(child)}, was born.");

        AddEntry(
            mother,
            gameEvent.Year,
            $"👶 Her {ToOrdinalWord(motherCount)} child, " +
            $"{_family.GetDisplayName(child)}, was born.");
    }

    private void AddMarriageEntryForSpouse(
        GameEvent gameEvent,
        IPerson subject)
    {
        var spouse =
            FindRelatedPerson(
                gameEvent,
                0);

        if (spouse is null)
            return;

        AddEntry(
            spouse,
            gameEvent.Year,
            $"💍 Married " +
            $"{_family.GetDisplayName(subject)}.");
    }

    private void AddRelativeEntries(
        GameEvent gameEvent,
        IPerson subject)
    {
        var subjectName =
            _family.GetDisplayName(
                subject);

        var eventVerb =
            GetRelativeEventVerb(
                gameEvent,
                subject);

        if (string.IsNullOrWhiteSpace(
            eventVerb))
        {
            return;
        }

        var emoji =
            GetEmoji(
                gameEvent.Type);

        foreach (var parent in
            new[]
            {
                _family.GetFather(subject),
                _family.GetMother(subject)
            })
        {
            if (!IsAlive(
                parent))
            {
                continue;
            }

            var relationship =
                _family.GetSex(subject)
                    == Sex.Male
                        ? "son"
                        : "daughter";

            AddEntry(
                parent!,
                gameEvent.Year,
                $"{emoji}{Possessive(parent!)} " +
                $"{relationship}, {subjectName}, " +
                $"{eventVerb}.");
        }

        foreach (var child in
            _family.GetChildren(
                subject))
        {
            if (!IsAlive(
                child))
            {
                continue;
            }

            var relationship =
                _family.GetSex(subject)
                    == Sex.Male
                        ? "father"
                        : "mother";

            AddEntry(
                child,
                gameEvent.Year,
                $"{emoji}{Possessive(child)} " +
                $"{relationship}, {subjectName}, " +
                $"{eventVerb}.");
        }

        var father =
            _family.GetFather(
                subject);

        if (father is null)
            return;

        foreach (var sibling in
            _family.GetChildren(
                father))
        {
            if (sibling.Id
                    == subject.Id
                || !IsAlive(
                    sibling))
            {
                continue;
            }

            var relationship =
                _family.GetSex(subject)
                    == Sex.Male
                        ? "brother"
                        : "sister";

            AddEntry(
                sibling,
                gameEvent.Year,
                $"{emoji}{Possessive(sibling)} " +
                $"{relationship}, {subjectName}, " +
                $"{eventVerb}.");
        }
    }

    private string GetRelativeEventVerb(
        GameEvent gameEvent,
        IPerson subject)
    {
        return gameEvent.Type switch
        {
            "life.death" =>
                gameEvent.Data.TryGetValue(
                    "age",
                    out var age)
                    ? $"died at age {age}"
                    : "died",

            "relationship.married" =>
                RelatedName(
                    gameEvent,
                    0,
                    "married"),

            "relationship.partnered" =>
                gameEvent.Data.TryGetValue(
                    "biographyVerb",
                    out var biographyVerb)
                    && !string.IsNullOrWhiteSpace(biographyVerb)
                        ? biographyVerb
                        : RelatedName(
                            gameEvent,
                            0,
                            "entered a partnership with"),

            "relationship.remarried" =>
                RelatedName(
                    gameEvent,
                    0,
                    "remarried"),

            "relationship.divorce" =>
                RelatedName(
                    gameEvent,
                    0,
                    "divorced"),

            "relationship.low_satisfaction_divorce" =>
                RelatedName(
                    gameEvent,
                    0,
                    "divorced after the marriage deteriorated with"),

            "relationship.prison_divorce" =>
                "was divorced while imprisoned",

            "relationship.affair" =>
                "was caught having an affair and divorced",

            "justice.crime" =>
                gameEvent.Data.TryGetValue(
                    "crime",
                    out var crime)
                    ? $"was convicted of {crime}"
                    : "was convicted of a crime",

            "health.serious_illness" =>
                gameEvent.Data.TryGetValue(
                    "condition",
                    out var condition)
                    ? $"fell ill with {condition}"
                    : "became seriously ill",

            "birth.condition" =>
                gameEvent.Data.TryGetValue(
                    "condition",
                    out var defect)
                    ? $"was born with {defect}"
                    : "was born with a serious condition",

            _ =>
                StripSubjectName(
                    GetEventText(
                        gameEvent),
                    subject)
        };
    }

    private string RelatedName(
        GameEvent gameEvent,
        int index,
        string verb)
    {
        var related =
            FindRelatedPerson(
                gameEvent,
                index);

        return related is null
            ? verb
            : $"{verb} " +
              $"{_family.GetDisplayName(related)}";
    }

    private string StripSubjectName(
        string? message,
        IPerson subject)
    {
        if (string.IsNullOrWhiteSpace(
            message))
        {
            return string.Empty;
        }

        var subjectName =
            _family.GetDisplayName(
                subject);

        if (message.StartsWith(
            subjectName,
            StringComparison.OrdinalIgnoreCase))
        {
            return message[
                subjectName.Length..]
                .Trim()
                .TrimEnd('.');
        }

        return message
            .Trim()
            .TrimEnd('.');
    }

}
