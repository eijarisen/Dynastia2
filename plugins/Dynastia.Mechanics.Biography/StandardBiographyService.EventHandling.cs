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

        if (gameEvent.Type.Equals(
                "craft.learned",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "craft.teaching_failed",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "craft.self_employment_started",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "craft.self_employment_ended",
                StringComparison.OrdinalIgnoreCase))
        {
            AddCraftEntries(gameEvent);
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
                GetCompletedBiographyYear(gameEvent),
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
                        GetCompletedBiographyYear(gameEvent),
                        formatted);
                }
            }
        }

        if (IsRelationshipEventWithPartner(
            gameEvent.Type))
        {
            AddRelationshipEntryForPartner(
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

    private void AddCraftEntries(GameEvent gameEvent)
    {
        var message = GetEventText(gameEvent);
        if (string.IsNullOrWhiteSpace(message))
            return;

        var formatted = FormatWithEmoji(gameEvent.Type, message);
        var people = new List<IPerson>();
        var subject = FindPerson(gameEvent.SubjectId);
        if (subject is not null)
            people.Add(subject);

        var includeTeacher = gameEvent.Type.Equals(
                "craft.teaching_failed",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "craft.learned",
                StringComparison.OrdinalIgnoreCase)
                && gameEvent.Data.TryGetValue(
                    "learningMode",
                    out var learningMode)
                && learningMode.Equals(
                    "taught",
                    StringComparison.OrdinalIgnoreCase);

        if (includeTeacher)
        {
            people.AddRange(gameEvent.RelatedPersonIds
                .Select(id => FindPerson(id))
                .Where(person => person is not null)
                .Cast<IPerson>());
        }

        foreach (var person in people.DistinctBy(person => person.Id))
        {
            AddEntry(person, GetCompletedBiographyYear(gameEvent), formatted);
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
                GetCompletedBiographyYear(gameEvent),
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
                    GetCompletedBiographyYear(gameEvent),
                    $"👶 {fallback}");
            }

            return;
        }

        AddEntry(
            child,
            GetCompletedBiographyYear(gameEvent),
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
            GetCompletedBiographyYear(gameEvent),
            $"👶 His {ToOrdinalWord(fatherCount)} child, " +
            $"{_family.GetDisplayName(child)}, was born.");

        AddEntry(
            mother,
            GetCompletedBiographyYear(gameEvent),
            $"👶 Her {ToOrdinalWord(motherCount)} child, " +
            $"{_family.GetDisplayName(child)}, was born.");

        foreach (var sibling in
            GetSiblings(child))
        {
            if (!IsAlive(sibling))
                continue;

            var relationship =
                _family.GetSex(child)
                    == Sex.Male
                        ? "brother"
                        : "sister";

            AddEntryIfMissing(
                sibling,
                GetCompletedBiographyYear(gameEvent),
                $"👶 {Possessive(sibling)} {relationship}, " +
                $"{_family.GetDisplayName(child)}, was born.");
        }
    }

    private void AddRelationshipEntryForPartner(
        GameEvent gameEvent,
        IPerson subject)
    {
        var partner =
            FindRelatedPerson(
                gameEvent,
                0);

        if (partner is null)
            return;

        EnsureGeneratedAdultLifeMilestones(
            partner);

        var subjectName =
            GetRelativeSubjectName(
                gameEvent,
                subject);

        var message =
            gameEvent.Type switch
            {
                "relationship.married" or
                "relationship.remarried" =>
                    $"💍 Married {subjectName}.",

                "relationship.partnered" =>
                    $"{GetEmojiPrefix(gameEvent.Type)}" +
                    $"{GetPartnerPartnershipVerb(gameEvent, subject, partner)}.",

                "relationship.divorce" =>
                    $"💔 Divorced {subjectName}.",

                "relationship.low_satisfaction_divorce" =>
                    $"💔 Divorced {subjectName} after the marriage deteriorated.",

                "relationship.prison_divorce" =>
                    $"💔 Marriage to {subjectName} ended in divorce during imprisonment.",

                "relationship.affair" =>
                    $"🤫 Marriage to {subjectName} was badly strained by an affair.",

                _ =>
                    string.Empty
            };

        if (!string.IsNullOrWhiteSpace(
            message))
        {
            AddEntryIfMissing(
                partner,
                GetCompletedBiographyYear(gameEvent),
                message);
        }
    }

    private void AddRelativeEntries(
        GameEvent gameEvent,
        IPerson subject)
    {
        var seenRecipients =
            new HashSet<Guid>();

        var partner =
            IsRelationshipEventWithPartner(
                gameEvent.Type)
                ? FindRelatedPerson(
                    gameEvent,
                    0)
                : null;

        AddCloseRelativeEntries(
            gameEvent,
            subject,
            partner?.Id,
            seenRecipients);

        if (partner is not null)
        {
            AddCloseRelativeEntries(
                gameEvent,
                partner,
                subject.Id,
                seenRecipients);
        }
    }

    private void AddCloseRelativeEntries(
        GameEvent gameEvent,
        IPerson subject,
        Guid? excludedPersonId,
        ISet<Guid> seenRecipients)
    {
        var eventVerb =
            GetRelativeEventVerb(
                gameEvent,
                subject);

        if (string.IsNullOrWhiteSpace(
            eventVerb))
        {
            return;
        }

        var subjectName =
            GetRelativeSubjectName(
                gameEvent,
                subject);

        var emoji =
            GetEmojiPrefix(
                gameEvent.Type);

        foreach (var relative in
            GetCloseRelatives(
                gameEvent,
                subject))
        {
            if (relative.Id == excludedPersonId
                || !IsAlive(relative)
                || !seenRecipients.Add(
                    relative.Id))
            {
                continue;
            }

            var relationship =
                GetRelationshipName(
                    relative,
                    subject,
                    gameEvent);

            if (string.IsNullOrWhiteSpace(
                relationship))
            {
                continue;
            }

            AddEntryIfMissing(
                relative,
                GetCompletedBiographyYear(gameEvent),
                $"{emoji}{Possessive(relative)} " +
                $"{relationship}, {subjectName}, " +
                $"{eventVerb}.");
        }
    }

    private IReadOnlyList<IPerson> GetCloseRelatives(
        GameEvent gameEvent,
        IPerson subject)
    {
        var relatives =
            new List<IPerson>();

        AddIfPresent(
            relatives,
            _family.GetFather(subject));

        AddIfPresent(
            relatives,
            _family.GetMother(subject));

        relatives.AddRange(
            _family.GetChildren(
                subject));

        relatives.AddRange(
            GetSiblings(
                subject));

        AddIfPresent(
            relatives,
            _family.GetSpouse(subject));

        if (gameEvent.Type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase))
        {
            foreach (var relatedId in
                gameEvent.RelatedPersonIds)
            {
                var related =
                    FindPerson(
                        relatedId);

                if (related is not null
                    && WereSpousesAtEvent(
                        subject,
                        related,
                        gameEvent.Year))
                {
                    AddIfPresent(
                        relatives,
                        related);
                }
            }
        }

        return relatives
            .DistinctBy(person => person.Id)
            .ToList();
    }

    private IReadOnlyList<IPerson> GetSiblings(
        IPerson person)
    {
        var father =
            _family.GetFather(person);

        var mother =
            _family.GetMother(person);

        return new[]
            {
                father,
                mother
            }
            .Where(parent =>
                parent is not null)
            .Cast<IPerson>()
            .SelectMany(parent =>
                _family.GetChildren(parent))
            .Where(sibling =>
                sibling.Id != person.Id)
            .DistinctBy(sibling => sibling.Id)
            .ToList();
    }

    private string? GetRelationshipName(
        IPerson observer,
        IPerson subject,
        GameEvent gameEvent)
    {
        if (_family.GetFather(subject)?.Id
                == observer.Id
            || _family.GetMother(subject)?.Id
                == observer.Id)
        {
            return _family.GetSex(subject)
                == Sex.Male
                    ? "son"
                    : "daughter";
        }

        if (_family.GetChildren(subject)
            .Any(child =>
                child.Id == observer.Id))
        {
            return _family.GetSex(subject)
                == Sex.Male
                    ? "father"
                    : "mother";
        }

        if (GetSiblings(subject)
            .Any(sibling =>
                sibling.Id == observer.Id))
        {
            return _family.GetSex(subject)
                == Sex.Male
                    ? "brother"
                    : "sister";
        }

        if (_family.GetSpouse(subject)?.Id
                == observer.Id
            || WereSpousesAtEvent(
                subject,
                observer,
                gameEvent.Year))
        {
            return _family.GetSex(subject)
                == Sex.Male
                    ? "husband"
                    : "wife";
        }

        return null;
    }

    private bool WereSpousesAtEvent(
        IPerson first,
        IPerson second,
        int year)
    {
        return _family.GetRelationshipHistory(
                first)
            .Any(record =>
                record.SpouseId == second.Id
                && record.StartYear <= year
                && (record.EndYear is null
                    || record.EndYear >= year));
    }

    private string GetRelativeEventVerb(
        GameEvent gameEvent,
        IPerson subject)
    {
        var primary =
            FindPerson(
                gameEvent.SubjectId);

        if (primary is not null
            && primary.Id != subject.Id
            && IsRelationshipEventWithPartner(
                gameEvent.Type))
        {
            return GetPartnerRelativeEventVerb(
                gameEvent,
                primary);
        }

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
                "was caught having an affair that badly strained the marriage",

            "justice.crime" =>
                GetCrimeRelativeEventVerb(
                    gameEvent),

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

    private static string GetCrimeRelativeEventVerb(
        GameEvent gameEvent)
    {
        var crime =
            gameEvent.Data.TryGetValue(
                "crime",
                out var crimeName)
                && !string.IsNullOrWhiteSpace(
                    crimeName)
                    ? crimeName
                    : "a crime";

        if (!gameEvent.Data.TryGetValue(
                "sentence",
                out var sentenceText)
            || !int.TryParse(
                sentenceText,
                out var sentence)
            || sentence <= 0)
        {
            return $"was convicted of {crime}";
        }

        var prisonText =
            sentence >= 50
                ? "life in prison"
                : sentence == 1
                    ? "1 year in prison"
                    : $"{sentence} years in prison";

        return $"was convicted of {crime} and sentenced to {prisonText}";
    }

    private string GetPartnerRelativeEventVerb(
        GameEvent gameEvent,
        IPerson primary)
    {
        var primaryName =
            GetRelativeSubjectName(
                gameEvent,
                primary);

        return gameEvent.Type switch
        {
            "relationship.married" =>
                $"married {primaryName}",

            "relationship.remarried" =>
                $"married {primaryName}",

            "relationship.partnered" =>
                $"entered a partnership with {primaryName}",

            "relationship.divorce" =>
                $"divorced {primaryName}",

            "relationship.low_satisfaction_divorce" =>
                $"divorced {primaryName} after the marriage deteriorated",

            "relationship.prison_divorce" =>
                $"ended the marriage to {primaryName} during imprisonment",

            "relationship.affair" =>
                $"badly strained the marriage to {primaryName} through an affair",

            _ =>
                string.Empty
        };
    }

    private string GetPartnerPartnershipVerb(
        GameEvent gameEvent,
        IPerson subject,
        IPerson partner)
    {
        var subjectName =
            GetRelativeSubjectName(
                gameEvent,
                subject);

        if (!gameEvent.Data.TryGetValue(
                "biographyVerb",
                out var biographyVerb)
            || string.IsNullOrWhiteSpace(
                biographyVerb))
        {
            return $"Entered a partnership with {subjectName}";
        }

        var partnerName =
            _family.GetDisplayName(
                partner);

        var reversed =
            biographyVerb.Replace(
                partnerName,
                subjectName,
                StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(
            reversed))
        {
            return $"Entered a partnership with {subjectName}";
        }

        return char.ToUpperInvariant(reversed[0])
            + reversed[1..];
    }

    private string GetRelativeSubjectName(
        GameEvent gameEvent,
        IPerson subject)
    {
        if (IsRelationshipUnion(
                gameEvent.Type)
            && _family.GetSex(subject)
                == Sex.Female
            && !string.IsNullOrWhiteSpace(
                subject.MaidenName))
        {
            return $"{subject.Name} " +
                _family.FormatSurname(
                    subject,
                    subject.MaidenName!,
                    Sex.Female);
        }

        return _family.GetDisplayName(
            subject);
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

    private static bool IsRelationshipUnion(
        string type) =>
        type.Equals(
            "relationship.married",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "relationship.remarried",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "relationship.partnered",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsRelationshipEventWithPartner(
        string type) =>
        IsRelationshipUnion(type)
        || type.Equals(
            "relationship.divorce",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "relationship.low_satisfaction_divorce",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "relationship.prison_divorce",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "relationship.affair",
            StringComparison.OrdinalIgnoreCase);

    private static void AddIfPresent(
        ICollection<IPerson> people,
        IPerson? person)
    {
        if (person is not null)
            people.Add(person);
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
