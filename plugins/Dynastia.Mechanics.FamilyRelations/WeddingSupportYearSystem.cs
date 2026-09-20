using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal sealed class WeddingSupportYearSystem : IYearSystem
{
    private const string ProcessedTagPrefix = "family_relations.wedding_support.";

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly StandardFamilyRelationService _relations;
    private readonly WeddingHouseholdFormationTracker _tracker;
    private readonly IGameEventBus _events;

    public WeddingSupportYearSystem(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        StandardFamilyRelationService relations,
        WeddingHouseholdFormationTracker tracker,
        IGameEventBus events)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _relations = relations;
        _tracker = tracker;
        _events = events;
    }

    public string Id => "family_relations.wedding_support";
    public YearPhase Phase => YearPhase.FamilyRelations;
    public IReadOnlyCollection<string> Before => [];
    public IReadOnlyCollection<string> After => ["family_relations.annual"];

    public void Execute(IGameState gameState)
    {
        foreach (var wedding in _tracker.TakeForYear(gameState.Year))
        {
            var child = Find(wedding.PersonId);
            var spouse = Find(wedding.SpouseId);
            if (child is null
                || spouse is null
                || !child.Tags.Has("state.alive")
                || !spouse.Tags.Has("state.alive"))
            {
                continue;
            }

            var currentHouseholdId = _economy.GetHouseholdId(child);
            if (currentHouseholdId is null
                || _economy.GetHouseholdId(spouse) != currentHouseholdId
                || wedding.PreviousHouseholdId == currentHouseholdId)
            {
                continue;
            }

            var processedTag =
                $"{ProcessedTagPrefix}{gameState.Year}.{spouse.Id:N}";
            if (child.Tags.Has(processedTag))
                continue;

            GiveWeddingSupport(child, currentHouseholdId.Value);
            child.Tags.Add(processedTag);
        }
    }

    private void GiveWeddingSupport(
        IPerson child,
        Guid recipientHouseholdId)
    {
        var donorCandidates = _relations.GetAllRelationships()
            .Where(relation =>
                relation.Type != FamilyRelationshipType.ExSpouse
                && (relation.PersonAId == child.Id || relation.PersonBId == child.Id))
            .Select(relation => new
            {
                Relation = relation,
                Relative = Find(
                    relation.PersonAId == child.Id
                        ? relation.PersonBId
                        : relation.PersonAId)
            })
            .Where(item =>
                item.Relative is not null
                && item.Relative.Tags.Has("state.alive")
                && FamilySupportAbilityRules.HasStrongCareerConnectionRelation(
                    item.Relation.Familiarity,
                    item.Relation.Sympathy))
            .Select(item => new
            {
                item.Relation,
                Relative = item.Relative!,
                HouseholdId = _economy.GetHouseholdId(item.Relative!)
            })
            .Where(item =>
                item.HouseholdId is Guid householdId
                && householdId != recipientHouseholdId)
            .GroupBy(item => item.HouseholdId!.Value)
            .Select(group => group
                .OrderByDescending(item => item.Relation.Score)
                .ThenByDescending(item => item.Relation.Sympathy)
                .First())
            .ToList();

        foreach (var donor in donorCandidates)
        {
            var finance = _economy.GetHousehold(donor.Relative);
            if (finance is null)
                continue;

            var expenses = _economy.GetAnnualForecast(donor.Relative)?.ProjectedExpenses
                ?? finance.LastExpenses;
            var gift = WeddingSupportRules.CalculateGift(
                finance.Wealth,
                expenses,
                donor.Relation.Familiarity,
                donor.Relation.Sympathy);
            if (gift < 100m)
                continue;

            _economy.ChangeWealth(donor.Relative, -gift);
            _economy.ChangeWealth(child, gift);
            _relations.RecordInteraction(
                child,
                donor.Relative,
                1,
                1,
                majorInteraction: false);

            _events.Publish(new GameEvent
            {
                Type = "family_relations.wedding_gift",
                Year = _gameState.Year,
                SubjectId = child.Id,
                RelatedPersonIds = [donor.Relative.Id],
                Data = new Dictionary<string, string>
                {
                    ["amount"] = gift.ToString(),
                    ["text"] =
                        $"{_family.GetDisplayName(donor.Relative)}'s household gave {_family.GetDisplayName(child)} a {gift:N0} zł wedding gift."
                }
            });
        }
    }

    private IPerson? Find(Guid id) =>
        _gameState.People.FirstOrDefault(person => person.Id == id);
}
