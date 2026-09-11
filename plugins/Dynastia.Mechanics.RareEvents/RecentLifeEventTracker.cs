using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed class RecentLifeEventTracker
{
    private const string BereavementTag =
        "recent.bereavement";

    private const string DivorceTag =
        "recent.divorce";

    private const string JobLossTag =
        "recent.job_loss";

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;

    public RecentLifeEventTracker(
        IGameState gameState,
        IFamilyService family,
        IGameEventBus events)
    {
        _gameState =
            gameState;

        _family =
            family;

        events.EventPublished +=
            OnEventPublished;
    }

    public bool HasFlag(
        IPerson person,
        string tag)
    {
        return person.Tags.Has(
            tag);
    }

    public void SetFlag(
        IPerson person,
        string tag,
        int durationYears,
        int currentYear)
    {
        if (durationYears < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationYears));
        }

        person.Tags.Add(
            tag);

        var component =
            person.Components.Get<
                RecentLifeEventComponent>()
            ?? new RecentLifeEventComponent();

        var expiry =
            currentYear
            + durationYears;

        var existing =
            component.Flags
                .FirstOrDefault(
                    flag =>
                        flag.Tag.Equals(
                            tag,
                            StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            component.Flags.Add(
                new RecentLifeEventFlagState
                {
                    Tag =
                        tag,

                    ExpiresAfterYear =
                        expiry
                });
        }
        else
        {
            existing.ExpiresAfterYear =
                Math.Max(
                    existing.ExpiresAfterYear,
                    expiry);
        }

        person.Components.Set(
            component);
    }

    public void Cleanup(
        int currentYear)
    {
        foreach (var person in
            _gameState.People)
        {
            var component =
                person.Components.Get<
                    RecentLifeEventComponent>();

            if (component is null)
                continue;

            var expired =
                component.Flags
                    .Where(
                        flag =>
                            currentYear
                            > flag.ExpiresAfterYear)
                    .ToList();

            foreach (var flag in
                expired)
            {
                person.Tags.Remove(
                    flag.Tag);

                component.Flags.Remove(
                    flag);
            }
        }
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (gameEvent.Type.Equals(
            "life.death",
            StringComparison.OrdinalIgnoreCase))
        {
            var bereaved =
                gameEvent.RelatedPersonIds
                    .Select(
                        relatedId =>
                            FindPerson(
                                relatedId))
                    .Where(
                        relative =>
                            relative is not null
                            && !relative.Tags.Has(
                                "state.dead"))
                    .Cast<IPerson>()
                    .ToDictionary(
                        relative =>
                            relative.Id,
                        relative =>
                            relative);

            var deceased =
                FindPerson(
                    gameEvent.SubjectId);

            if (deceased is not null)
            {
                var parent =
                    _family.GetFather(
                        deceased)
                    ?? _family.GetMother(
                        deceased);

                if (parent is not null)
                {
                    foreach (var sibling in
                        _family.GetChildren(
                            parent))
                    {
                        if (sibling.Id
                                == deceased.Id
                            || sibling.Tags.Has(
                                "state.dead"))
                        {
                            continue;
                        }

                        bereaved[sibling.Id] =
                            sibling;
                    }
                }
            }

            foreach (var relative in
                bereaved.Values)
            {
                SetFlag(
                    relative,
                    BereavementTag,
                    durationYears:
                        2,
                    currentYear:
                        gameEvent.Year);
            }

            return;
        }

        if (IsDivorceEvent(
            gameEvent.Type))
        {
            SetSubjectAndRelated(
                gameEvent,
                DivorceTag,
                durationYears:
                    2);

            return;
        }

        if (gameEvent.Type.Equals(
            "career.fired",
            StringComparison.OrdinalIgnoreCase))
        {
            var subject =
                FindPerson(
                    gameEvent.SubjectId);

            if (subject is not null)
            {
                SetFlag(
                    subject,
                    JobLossTag,
                    durationYears:
                        1,
                    currentYear:
                        gameEvent.Year);
            }
        }
    }

    private void SetSubjectAndRelated(
        GameEvent gameEvent,
        string tag,
        int durationYears)
    {
        var subject =
            FindPerson(
                gameEvent.SubjectId);

        if (subject is not null)
        {
            SetFlag(
                subject,
                tag,
                durationYears,
                gameEvent.Year);
        }

        foreach (var relatedId in
            gameEvent.RelatedPersonIds)
        {
            var related =
                FindPerson(
                    relatedId);

            if (related is null)
                continue;

            SetFlag(
                related,
                tag,
                durationYears,
                gameEvent.Year);
        }
    }

    private static bool IsDivorceEvent(
        string type)
    {
        return type.Equals(
                "relationship.divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.prison_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.low_satisfaction_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.affair",
                StringComparison.OrdinalIgnoreCase);
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
}
