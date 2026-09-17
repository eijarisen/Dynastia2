using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class FamilyNannyTracker
{
    public const string FamilyNannyTag =
        "role.family_nanny";

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;
    private readonly IGameEventBus _events;

    public FamilyNannyTracker(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        ICareerService career,
        IGameEventBus events)
    {
        _gameState =
            gameState;

        _family =
            family;

        _economy =
            economy;

        _career =
            career;

        _events =
            events;

        events.EventPublished +=
            OnEventPublished;
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (!ShouldEndFamilyNannyService(
            gameEvent.Type))
        {
            return;
        }

        var caregiver =
            FindPerson(
                gameEvent.SubjectId);

        if (caregiver is null
            || !caregiver.Tags.Has(
                FamilyNannyTag))
        {
            return;
        }

        EndService(
            caregiver,
            gameEvent.Year,
            ResolveReason(
                gameEvent.Type));
    }

    private void EndService(
        IPerson caregiver,
        int year,
        string reason)
    {
        var heads =
            _gameState.People
                .Where(
                    person =>
                        _economy.HasHousehold(
                            person)
                        && _economy.GetHousehold(
                            person)?.NannyId
                            == caregiver.Id)
                .ToList();

        caregiver.Tags.Remove(
            FamilyNannyTag);

        foreach (var head in
            heads)
        {
            _economy.SetNanny(
                head,
                null);

            _events.Publish(
                new GameEvent
                {
                    Type =
                        "household.family_nanny_ended",

                    Year =
                        year,

                    SubjectId =
                        head.Id,

                    RelatedPersonIds =
                        [caregiver.Id],

                    Data =
                        new Dictionary<string, string>
                        {
                            ["reason"] =
                                reason,

                            ["text"] =
                                $"{_family.GetDisplayName(caregiver)}'s " +
                                $"{_career.GetStatusLabel(FamilyNannyTag)} role " +
                                $"ended because {reason}."
                        }
                });
        }
    }

    private static bool
        ShouldEndFamilyNannyService(
            string type)
    {
        return type.Equals(
                "career.employment",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "career.family_connections_success",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveReason(
        string type)
    {
        if (type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase))
        {
            return "they died";
        }

        if (type.StartsWith(
            "relationship.",
            StringComparison.OrdinalIgnoreCase))
        {
            return "they married";
        }

        return "they found employment";
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
