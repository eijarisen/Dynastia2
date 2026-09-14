using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed class StandardBiographyService :
    IBiographyService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IHouseholdService _households;
    private readonly AboutTextBuilder _about;

    private readonly Dictionary<
        Guid,
        List<BiographyEntry>>
        _entries = [];

    public StandardBiographyService(
        IGameState gameState,
        IFamilyService family,
        IStatsService stats,
        ILocationService locations,
        IHouseholdService households,
        IGameEventBus events)
    {
        _gameState = gameState;
        _family = family;
        _households = households;

        _about =
            new AboutTextBuilder(
                gameState,
                family,
                stats,
                locations);

        events.EventPublished +=
            OnEventPublished;
    }

    public string GetAbout(
        IPerson person)
    {
        return _about.Build(
            person);
    }

    public IReadOnlyList<BiographyEntry>
        GetBiography(
            IPerson person)
    {
        if (!_entries.TryGetValue(
            person.Id,
            out var entries))
        {
            return [];
        }

        return entries
            .Select(
                (entry, index) =>
                    new
                    {
                        Entry = entry,
                        Index = index
                    })
            .OrderByDescending(
                item =>
                    item.Entry.Year)
            .ThenByDescending(
                item =>
                    item.Index)
            .Select(
                item =>
                    item.Entry)
            .ToList();
    }

    public IReadOnlyDictionary<
        Guid,
        IReadOnlyList<BiographyEntry>>
        ExportBiographyState()
    {
        return _entries.ToDictionary(
            pair =>
                pair.Key,
            pair =>
                (IReadOnlyList<BiographyEntry>)
                    pair.Value.ToList());
    }

    public void RestoreBiographyState(
        IReadOnlyDictionary<
            Guid,
            IReadOnlyList<BiographyEntry>>
            entries)
    {
        ArgumentNullException.ThrowIfNull(
            entries);

        _entries.Clear();

        foreach (var pair in
            entries)
        {
            _entries[pair.Key] =
                pair.Value.ToList();
        }
    }

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

    private void SeedFounderBiography(
        GameEvent gameEvent)
    {
        var founder =
            FindPerson(
                gameEvent.SubjectId);

        if (founder is null)
            return;

        var father =
            _family.GetFather(
                founder);

        var mother =
            _family.GetMother(
                founder);

        if (founder.BirthDate
            is GameDate birthDate)
        {
            var parentText =
                father is not null
                && mother is not null
                    ? $" to {_family.GetDisplayName(father)} " +
                      $"and {_family.GetDisplayName(mother)}"
                    : string.Empty;

            AddEntry(
                founder,
                birthDate.Year,
                $"👶 Was born{parentText}.");
        }

        if (father?.DeathDate
                is GameDate fatherDeath
            && mother?.DeathDate
                is GameDate motherDeath)
        {
            var orphanYear =
                Math.Max(
                    fatherDeath.Year,
                    motherDeath.Year);

            var orphanAge =
                founder.BirthDate
                    is GameDate founderBirth
                    ? orphanYear
                      - founderBirth.Year
                    : 17;

            AddEntry(
                founder,
                orphanYear,
                $"💀 Became an orphan at age " +
                $"{orphanAge} after both parents died.");
        }

        AddEntry(
            founder,
            gameEvent.Year,
            "🧑 Became an adult.");
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
                RelatedName(
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

    private static bool IsFamilyMoneyEvent(
        string type) =>
        type.Equals(
            "family_relations.money_received",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "family_relations.money_given",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "family_relations.money_refused",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsImportantForRelatives(
        string type)
    {
        return type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.partnered",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase)
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
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "justice.crime",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "health.serious_illness",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "birth.condition",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatWithEmoji(
        string type,
        string message)
    {
        var emoji =
            GetEmoji(type);

        return string.IsNullOrEmpty(
            emoji)
            ? message
            : $"{emoji}{message}";
    }

    private static string GetEmoji(
        string type)
    {
        return type switch
        {
            // Structured-event aliases of Dynasty 4's const emojiMap.
            "wellbeing.heal" => "❤️‍🩹 ",
            "wellbeing.recover" => "🛌 ",
            "education.success" => "🎓 ",
            "education.failure" => "🧱 ",
            "education.help_learning_success" => "📚 ",
            "education.help_learning_failure" => "📖 ",
            "wellbeing.therapy_success" => "😊 ",
            "wellbeing.therapy_failure" => "😒 ",
            "wellbeing.drink" => "🍺 ",
            "life.adult" => "🧑 ",

            "inheritance.received_at_adulthood" => "💰 ",
            "inheritance.received" => "💸 ",
            "inheritance.pending_minor" => "⏳ ",
            "inheritance.claimable" => "⏳ ",
            "inheritance.unclaimed" => "💨 ",
            "inheritance.estate_settled" => "🏦 ",
            "inheritance.houses" => "🏡 ",
            "inheritance.promised_houses_received" => "🏡 ",

            "loan.taken" => "🏦 ",
            "loan.given" => "🤝 ",
            "loan.repaid" => "✅ ",
            "loan.receivable_repaid" => "✅ ",
            "loan.debt_inherited" => "📜 ",
            "career.retirement" => "🕊️ ",
            "justice.released" => "✅ ",
            "health.illness" => "🤧 ",
            "health.serious_illness" => "😣 ",
            "life.death" => "💀 ",
            "career.quit" => "🚶 ",
            "relationship.divorce" => "💔 ",
            "relationship.low_satisfaction_divorce" => "💔 ",
            "relationship.repair_marriage" => "❤️‍🩹 ",
            "relationship.prison_divorce" => "💔 ",
            "career.employment" => "✅ ",
            "family_support.parents_success" => "🙏 ",
            "family_support.parents_failure" => "🚫 ",

            "family_support.child_success" => "🙏 ",
            "family_support.child_failure" => "🚫 ",
            "family_relations.money_received" => "💰 ",
            "family_relations.money_given" => "🎁 ",
            "family_relations.money_refused" => "🚫 ",

            "career.ask_quit_success" => "✅ ",
            "career.ask_quit_failure" => "🚫 ",
            "career.ask_recover_success" => "✅ ",
            "career.ask_recover_failure" => "🚫 ",
            "justice.crime" => "⛓️ ",
            "career.fired" => "💥 ",
            "career.promotion" => "✨ ",
            "relationship.partnered" => "👩‍❤️‍👩 ",
            "relationship.married" => "💍 ",
            "relationship.affair" => "🤫 ",
            "birth.condition" => "🧩 ",
            "life.birth" => "👶 ",
            "relationship.remarried" => "💍 ",

            "household.house_bought" => "🏠 ",
            "household.house_sold" => "💵 ",
            "household.house_rented" => "🏘️ ",
            "household.house_given" => "🎁 ",

            "household.house_promised" => "🎁 ",

            "household.nanny_hired" => "🧑‍🍼 ",
            "household.family_nanny_started" => "🧑‍🍼 ",
            "household.family_nanny_ended" => "👋 ",
            "household.nanny_service_ended" => "👋 ",
            "household.nanny_fired" => "👋 ",

            "adoption.with_mother" => "👩‍👧 ",
            "adoption.orphaned" => "🕯️ ",
            "adoption.placed" => "🏠 ",
            "adoption.orphanage" => "🏚️ ",
            "adoption.left_orphanage" => "🧳 ",

            "rare.house_fire" => "🔥 ",
            "rare.burglary" => "🕵️ ",
            "rare.storm_flood_damage" => "🌊 ",
            "rare.structural_accident" => "🧱 ",
            "rare.assault" => "🥊 ",
            "rare.mugging" => "💸 ",
            "rare.workplace_accident" => "⚠️ ",
            "rare.traffic_accident" => "🚗 ",
            "rare.lightning_strike" => "⚡ ",
            "rare.serious_fall" => "🤕 ",
            "rare.lottery_win" => "🎰 ",
            "rare.distant_inheritance" => "💰 ",
            "rare.fraud" => "🎭 ",
            "rare.found_property" => "💎 ",
            "rare.wrongful_arrest" => "⚖️ ",
            "rare.suicide" => "🕯️ ",

            _ => "🔹 "
        };
    }

    private void AddEntry(
        IPerson person,
        int year,
        string message)
    {
        if (!_entries.TryGetValue(
            person.Id,
            out var entries))
        {
            entries = [];
            _entries[person.Id] =
                entries;
        }

        entries.Add(
            new BiographyEntry(
                year,
                message));
    }

    private IPerson? FindPerson(
        Guid? id)
    {
        if (id is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == id.Value);
    }

    private IPerson? FindRelatedPerson(
        GameEvent gameEvent,
        int index)
    {
        if (index < 0
            || index >= gameEvent
                .RelatedPersonIds
                .Count)
        {
            return null;
        }

        return FindPerson(
            gameEvent.RelatedPersonIds[
                index]);
    }

    private static string? GetEventText(
        GameEvent gameEvent)
    {
        return gameEvent.Data.TryGetValue(
            "text",
            out var text)
            ? text
            : null;
    }

    private static int GetInt(
        GameEvent gameEvent,
        string key,
        int fallback)
    {
        return gameEvent.Data.TryGetValue(
                key,
                out var text)
            && int.TryParse(
                text,
                out var value)
                    ? value
                    : fallback;
    }

    private static string Possessive(
        IPerson person)
    {
        return person.Tags.Has(
            "sex.male")
                ? "His"
                : "Her";
    }

    private static bool IsAlive(
        IPerson? person)
    {
        return person is not null
            && person.Tags.Has(
                "state.alive");
    }

    private static string ToOrdinalWord(
        int value)
    {
        return value switch
        {
            1 => "first",
            2 => "second",
            3 => "third",
            _ => $"{value}th"
        };
    }
}
