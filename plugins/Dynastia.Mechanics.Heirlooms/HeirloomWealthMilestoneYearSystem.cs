using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal sealed class HeirloomWealthMilestoneYearSystem : IYearSystem
{
    private readonly IEconomyService _economy;
    private readonly IHeirloomService _heirlooms;
    private readonly IGameRandom _random;
    private readonly HeirloomAchievementCatalog _catalog;

    public HeirloomWealthMilestoneYearSystem(
        IEconomyService economy,
        IHeirloomService heirlooms,
        IGameRandom random,
        HeirloomAchievementCatalog catalog)
    {
        _economy = economy;
        _heirlooms = heirlooms;
        _random = random;
        _catalog = catalog;
    }

    public string Id => "heirlooms.wealth_milestones";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => [];
    public IReadOnlyCollection<string> After => [];

    public void Execute(IGameState gameState)
    {
        var representatives = gameState.People
            .Where(_economy.HasHousehold)
            .Select(person => new
            {
                Person = person,
                HouseholdId = _economy.GetHouseholdId(person)
            })
            .Where(item => item.HouseholdId is not null)
            .GroupBy(item => item.HouseholdId!.Value)
            .Select(group => group.First())
            .ToList();

        foreach (var item in representatives)
        {
            var household = _economy.GetHousehold(item.Person);
            if (household is null)
                continue;

            var ledger = GetOrCreateLedger(gameState, item.Person, item.HouseholdId!.Value);
            foreach (var tier in _catalog.WealthTiers)
            {
                if (household.Wealth < tier.MinimumHouseholdWealth
                    || ledger.RolledWealthTierIds.Contains(tier.TierId))
                {
                    continue;
                }

                // Crossing a tier buys exactly one roll. Mark it before rolling
                // so losing and later regaining wealth can never reroll the tier.
                ledger.RolledWealthTierIds.Add(tier.TierId);
                if (!_random.Chance(tier.GenerationChanceOnFirstCrossing))
                    continue;

                var templateId = tier.TemplateIds[
                    _random.NextInt(0, tier.TemplateIds.Count - 1)];
                _heirlooms.Create(
                    item.Person,
                    new HeirloomCreationRequest(
                        templateId,
                        null,
                        "wealth",
                        $"wealth:{item.HouseholdId}:{tier.TierId}",
                        $"Acquired when household wealth first reached {tier.MinimumHouseholdWealth:N0} zł.",
                        NewsTokens: new Dictionary<string, string>
                        {
                            ["WealthThreshold"] = tier.MinimumHouseholdWealth.ToString("N0", CultureInfo.InvariantCulture)
                        }));
            }
        }
    }

    private static HeirloomHouseholdMilestoneComponent GetOrCreateLedger(
        IGameState gameState,
        IPerson representative,
        Guid householdId)
    {
        var existing = gameState.People
            .Select(person => person.Components.Get<HeirloomHouseholdMilestoneComponent>())
            .FirstOrDefault(component => component?.HouseholdId == householdId);
        if (existing is not null)
            return existing;

        var created = new HeirloomHouseholdMilestoneComponent
        {
            HouseholdId = householdId
        };
        representative.Components.Set(created);
        return created;
    }
}
