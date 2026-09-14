using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class NannyNeedReconcileYearSystem :
    IYearSystem
{
    private readonly IHouseholdService _households;
    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly ICareerService _career;
    private readonly IGameEventBus _events;
    private readonly string _id;
    private readonly YearPhase _phase;
    private readonly IReadOnlyCollection<string> _before;
    private readonly IReadOnlyCollection<string> _after;

    public NannyNeedReconcileYearSystem(
        IHouseholdService households,
        IEconomyService economy,
        IFamilyService family,
        ICareerService career,
        IGameEventBus events,
        string id,
        YearPhase phase,
        IReadOnlyCollection<string> before,
        IReadOnlyCollection<string> after)
    {
        _households = households;
        _economy = economy;
        _family = family;
        _career = career;
        _events = events;
        _id = id;
        _phase = phase;
        _before = before;
        _after = after;
    }

    public string Id => _id;

    public YearPhase Phase => _phase;

    public IReadOnlyCollection<string> Before =>
        _before;

    public IReadOnlyCollection<string> After =>
        _after;

    public void Execute(
        IGameState gameState)
    {
        foreach (var head in
            gameState.People
                .Where(
                    person =>
                        _economy.HasHousehold(
                            person))
                .ToList())
        {
            var status =
                _households.GetStatus(
                    head);

            if (status is null
                || !status.NannyId.HasValue
                || status.UnderageChildren
                    > status.BaseChildCapacity)
            {
                continue;
            }

            var nanny =
                _households.GetNanny(
                    head);

            var familyNanny =
                nanny is not null
                && nanny.Tags.Has(
                    FamilyNannyTracker.FamilyNannyTag);

            if (familyNanny
                && nanny is not null)
            {
                nanny.Tags.Remove(
                    FamilyNannyTracker.FamilyNannyTag);
            }

            _economy.SetNanny(
                head,
                null);

            _events.Publish(
                new GameEvent
                {
                    Type =
                        familyNanny
                            ? "household.family_nanny_ended"
                            : "household.nanny_service_ended",

                    Year =
                        gameState.Year,

                    SubjectId =
                        head.Id,

                    RelatedPersonIds =
                        nanny is null
                            ? Array.Empty<Guid>()
                            : new[] { nanny.Id },

                    Data =
                        new Dictionary<string, string>
                        {
                            ["reason"] =
                                "the household no longer needed nanny help",

                            ["text"] =
                                nanny is not null && familyNanny
                                    ? $"{_family.GetDisplayName(nanny)}'s " +
                                      $"{_career.GetStatusLabel(FamilyNannyTracker.FamilyNannyTag)} role ended because the household was no longer strained by young children."
                                    : $"The household of {_family.GetDisplayName(head)} ended its " +
                                      $"{_career.GetStatusLabel("role.nanny")} arrangement because the family was no longer strained by young children."
                        }
                });
        }
    }
}
