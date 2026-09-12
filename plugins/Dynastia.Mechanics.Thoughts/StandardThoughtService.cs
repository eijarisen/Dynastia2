using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class StandardThoughtService :
    IThoughtService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IHealthService _health;
    private readonly ICareerService _career;
    private readonly IHouseholdService _households;
    private readonly IJusticeService _justice;
    private readonly IEducationService _education;
    private readonly IEconomyService _economy;
    private readonly IAdoptionService _adoption;
    private readonly IMarriageSatisfactionService _marriageSatisfaction;
    private readonly IGameEventBus _events;
    private readonly IThoughtProviderRegistry _providers;
    private readonly ThoughtPhraseRenderer _renderer;

    public StandardThoughtService(
        IGameState gameState,
        IFamilyService family,
        IStatsService stats,
        IHealthService health,
        ICareerService career,
        IHouseholdService households,
        IJusticeService justice,
        IEducationService education,
        IEconomyService economy,
        IAdoptionService adoption,
        IMarriageSatisfactionService marriageSatisfaction,
        IGameEventBus events,
        IThoughtProviderRegistry providers)
    {
        _gameState =
            gameState;

        _family =
            family;

        _stats =
            stats;

        _health =
            health;

        _career =
            career;

        _households =
            households;

        _justice =
            justice;

        _education =
            education;

        _economy =
            economy;

        _adoption =
            adoption;

        _marriageSatisfaction =
            marriageSatisfaction;

        _events =
            events;

        _providers =
            providers;

        _renderer =
            new ThoughtPhraseRenderer(
                stats);

        events.EventPublished +=
            OnEventPublished;
    }

    public PersonThoughtSnapshot? GetCurrentThought(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        if (person.Tags.Has(
                "state.dead")
            || person.Age < 5)
        {
            person.Components.Remove<
                PersonThoughtComponent>();

            return null;
        }

        var component =
            person.Components.Get<
                PersonThoughtComponent>();

        if (component?.Year
            != _gameState.Year)
        {
            // Lazy state-only generation is important for old saves and
            // for any UI that asks before the first explicit refresh.
            GenerateForPerson(
                person,
                events:
                    []);

            component =
                person.Components.Get<
                    PersonThoughtComponent>();
        }

        return component is null
            ? null
            : ToSnapshot(
                component);
    }

    public void EnsureCurrentThoughts()
    {
        var anchor =
            FindStateAnchor();

        if (anchor is null)
            return;

        var state =
            anchor.Components.Get<
                ThoughtSystemStateComponent>();

        if (state is null)
        {
            state =
                new ThoughtSystemStateComponent
                {
                    LastProcessedEventCount =
                        _events.AllEvents.Count,

                    LastGeneratedYear =
                        _gameState.Year
                };

            anchor.Components.Set(
                state);
        }
        else
        {
            state.LastProcessedEventCount =
                Math.Clamp(
                    state.LastProcessedEventCount,
                    0,
                    _events.AllEvents.Count);
        }

        foreach (var person in
            _gameState.People)
        {
            if (person.Tags.Has(
                    "state.dead")
                || SimulationState.IsInactive(person)
                || person.Age < 5)
            {
                person.Components.Remove<
                    PersonThoughtComponent>();

                continue;
            }

            var thought =
                person.Components.Get<
                    PersonThoughtComponent>();

            if (thought?.Year
                == _gameState.Year)
            {
                continue;
            }

            GenerateForPerson(
                person,
                events:
                    []);
        }

        state.LastGeneratedYear =
            _gameState.Year;
    }

    public void ResetAfterLoad()
    {
        var anchor =
            FindStateAnchor();

        if (anchor is null)
            return;

        var state =
            anchor.Components.Get<
                ThoughtSystemStateComponent>();

        if (state is null)
        {
            // Pre-Thoughts save: start from the loaded state, but do not
            // fabricate old event thoughts from restored history.
            state =
                new ThoughtSystemStateComponent
                {
                    LastProcessedEventCount =
                        _events.AllEvents.Count,

                    LastGeneratedYear =
                        _gameState.Year
                };

            anchor.Components.Set(
                state);
        }
        else
        {
            state.LastProcessedEventCount =
                Math.Clamp(
                    state.LastProcessedEventCount,
                    0,
                    _events.AllEvents.Count);
        }

        EnsureCurrentThoughts();
    }

    internal void GenerateAnnualThoughts()
    {
        var anchor =
            FindStateAnchor();

        if (anchor is null)
            return;

        var state =
            anchor.Components.Get<
                ThoughtSystemStateComponent>();

        int eventStart;

        if (state is null)
        {
            // Defensive old-save fallback. Only events from the current
            // just-completed simulation may become event thoughts.
            eventStart =
                FindFirstCurrentYearEventIndex();

            state =
                new ThoughtSystemStateComponent();

            anchor.Components.Set(
                state);
        }
        else
        {
            eventStart =
                Math.Clamp(
                    state.LastProcessedEventCount,
                    0,
                    _events.AllEvents.Count);
        }

        var pendingEvents =
            _events.AllEvents
                .Skip(
                    eventStart)
                .ToList();

        foreach (var person in
            _gameState.People)
        {
            if (person.Tags.Has(
                    "state.dead")
                || SimulationState.IsInactive(person)
                || person.Age < 5)
            {
                person.Components.Remove<
                    PersonThoughtComponent>();

                continue;
            }

            GenerateForPerson(
                person,
                pendingEvents);
        }

        state.LastProcessedEventCount =
            _events.AllEvents.Count;

        state.LastGeneratedYear =
            _gameState.Year;
    }

    private void GenerateForPerson(
        IPerson person,
        IReadOnlyList<GameEvent> events)
    {
        var context =
            new ThoughtContext
            {
                Year =
                    _gameState.Year,

                Events =
                    events,

                GameState =
                    _gameState,

                Family =
                    _family,

                Stats =
                    _stats,

                Health =
                    _health,

                Career =
                    _career,

                Households =
                    _households,

                Justice =
                    _justice,

                Education =
                    _education,

                Economy =
                    _economy,

                Adoption =
                    _adoption,

                MarriageSatisfaction =
                    _marriageSatisfaction
            };

        var candidates =
            new List<ThoughtCandidate>();

        foreach (var provider in
            _providers.Providers)
        {
            foreach (var candidate in
                provider.GetCandidates(
                    person,
                    context))
            {
                var ageAdjusted =
                    ThoughtProviderUtilities
                        .ApplyAgePriority(
                            person,
                            candidate);

                candidates.Add(
                    candidate with
                    {
                        Salience =
                            ApplyPersonalitySalience(
                                person,
                                candidate,
                                ageAdjusted)
                    });
            }
        }

        candidates.Add(
            new ThoughtCandidate(
                "fallback",
                "fallback",
                "fallback",
                1,
                ThoughtProviderUtilities
                    .FallbackEmoji(
                        person,
                        _family),
                "fallback",
                null,
                "fallback"));

        var deduplicated =
            candidates
                .GroupBy(
                    candidate =>
                        candidate.DeduplicationKey,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        group
                            .OrderByDescending(
                                candidate =>
                                    candidate.Salience)
                            .ThenBy(
                                candidate =>
                                    candidate.Id,
                                StringComparer.OrdinalIgnoreCase)
                            .First())
                .OrderByDescending(
                    candidate =>
                        candidate.Salience)
                .ThenBy(
                    candidate =>
                        candidate.Id,
                    StringComparer.OrdinalIgnoreCase)
                .Take(
                    3)
                .ToList();

        var dynastyKey =
            GetDynastyKey();

        var selected =
            DeterministicThoughtRandom.Choose(
                deduplicated,
                dynastyKey,
                person.Id.ToString(),
                _gameState.Year.ToString(),
                "thought-selection");

        var text =
            _renderer.Render(
                person,
                selected,
                dynastyKey,
                _gameState.Year);

        person.Components.Set(
            new PersonThoughtComponent
            {
                Year =
                    _gameState.Year,

                ThoughtId =
                    selected.Id,

                Topic =
                    selected.Topic,

                Text =
                    text,

                Emoji =
                    selected.Emoji,

                Salience =
                    selected.Salience,

                SourceId =
                    selected.SourceId
            });
    }


    private static int ApplyPersonalitySalience(
        IPerson person,
        ThoughtCandidate candidate,
        int salience)
    {
        var key =
            $"{candidate.Id} {candidate.Topic} {candidate.WordingKey}"
                .ToLowerInvariant();

        var emotional =
            key.Contains("loss")
            || key.Contains("bereavement")
            || key.Contains("divorce")
            || key.Contains("affair")
            || key.Contains("fired")
            || key.Contains("assault")
            || key.Contains("illness")
            || key.Contains("orphan")
            || key.Contains("marriage")
            || key.Contains("birth")
            || key.Contains("relationship");

        var negative =
            key.Contains("loss")
            || key.Contains("bereavement")
            || key.Contains("divorce")
            || key.Contains("affair")
            || key.Contains("fired")
            || key.Contains("assault")
            || key.Contains("miserable")
            || key.Contains("unhappy")
            || key.Contains("broke")
            || key.Contains("illness")
            || key.Contains("orphan")
            || key.Contains("imprison")
            || key.Contains("failure");

        var positive =
            key.Contains("married")
            || key.Contains("marriage.new")
            || key.Contains("birth")
            || key.Contains("promotion")
            || key.Contains("satisfied")
            || key.Contains("thriving")
            || key.Contains("repaired")
            || key.Contains("success")
            || key.Contains("inheritance")
            || key.Contains("lottery");

        var career =
            key.Contains("career")
            || key.Contains("employment")
            || key.Contains("education");

        var immediateProblem =
            key.Contains("fired")
            || key.Contains("miserable")
            || key.Contains("unhappy")
            || key.Contains("broke")
            || key.Contains("assault")
            || key.Contains("divorce");

        var melancholic =
            negative
                ? (key.Contains("bereavement")
                    || key.Contains("divorce")
                    || key.Contains("fired")
                    || key.Contains("assault")
                        ? 0.20
                        : 0.15)
                : emotional
                    ? 0.10
                    : 0;

        var phlegmatic =
            emotional && salience < 90
                ? -0.10
                : 0;

        var sanguine =
            positive
                ? 0.10
                : career
                    ? 0.05
                    : 0;

        var choleric =
            career || immediateProblem
                ? 0.10
                : 0;

        var multiplier =
            PersonalityInfluence.Multiplier(
                person,
                melancholic,
                phlegmatic,
                sanguine,
                choleric);

        return Math.Max(
            1,
            (int)Math.Round(
                salience * multiplier,
                MidpointRounding.AwayFromZero));
    }

    private int FindFirstCurrentYearEventIndex()
    {
        for (var index = 0;
            index < _events.AllEvents.Count;
            index++)
        {
            if (_events.AllEvents[index].Year
                >= _gameState.Year)
            {
                return index;
            }
        }

        return _events.AllEvents.Count;
    }

    private IPerson? FindStateAnchor()
    {
        return _gameState.People
            .FirstOrDefault(
                person =>
                    _family.GetGeneration(
                        person) == 1
                    && _family.IsMaleLineage(
                        person))
            ?? _gameState.People
                .FirstOrDefault();
    }

    private string GetDynastyKey()
    {
        var anchor =
            FindStateAnchor();

        return
            $"{_gameState.DynastySurname}|" +
            $"{anchor?.Id.ToString() ?? "no-founder"}";
    }

    private static PersonThoughtSnapshot ToSnapshot(
        PersonThoughtComponent component)
    {
        return new PersonThoughtSnapshot(
            component.Year,
            component.ThoughtId,
            component.Topic,
            component.Text,
            component.Emoji,
            component.Salience,
            component.SourceId);
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (!gameEvent.Type.Equals(
            "game.started",
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // New games should already have a visible thought before the
        // player makes the first annual choice.
        EnsureCurrentThoughts();
    }
}
