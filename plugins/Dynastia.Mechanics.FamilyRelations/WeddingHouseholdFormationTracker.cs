using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal sealed class WeddingHouseholdFormationTracker
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly List<PendingWeddingHousehold> _pending = [];

    public WeddingHouseholdFormationTracker(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        IGameEventBus events)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        events.EventPublished += OnEvent;
    }

    public IReadOnlyList<PendingWeddingHousehold> TakeForYear(int year)
    {
        var result = _pending.Where(item => item.Year == year).ToList();
        _pending.RemoveAll(item => item.Year <= year);
        return result;
    }

    private void OnEvent(object? sender, GameEvent gameEvent)
    {
        if (!gameEvent.Type.Equals("relationship.married", StringComparison.OrdinalIgnoreCase)
            && !gameEvent.Type.Equals("relationship.remarried", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var people = new List<IPerson>();
        if (gameEvent.SubjectId is Guid subjectId)
        {
            var subject = Find(subjectId);
            if (subject is not null)
                people.Add(subject);
        }

        foreach (var relatedId in gameEvent.RelatedPersonIds)
        {
            var related = Find(relatedId);
            if (related is not null && people.All(person => person.Id != related.Id))
                people.Add(related);
        }

        foreach (var person in people.Where(_family.IsBloodline))
        {
            var spouse = _family.GetSpouse(person);
            if (spouse is null)
                continue;

            _pending.Add(new PendingWeddingHousehold(
                gameEvent.Year,
                person.Id,
                spouse.Id,
                _economy.GetHouseholdId(person)));
        }
    }

    private IPerson? Find(Guid id) =>
        _gameState.People.FirstOrDefault(person => person.Id == id);
}

internal sealed record PendingWeddingHousehold(
    int Year,
    Guid PersonId,
    Guid SpouseId,
    Guid? PreviousHouseholdId);
