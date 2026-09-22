using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal sealed class ArtisticWorkYearSystem : IYearSystem
{
    private readonly IFamilyService _family;
    private readonly ICraftService _crafts;
    private readonly IHeirloomService _heirlooms;
    private readonly IWorkCapacityService _workCapacity;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly ArtisticWorkCatalog _catalog;

    public ArtisticWorkYearSystem(
        IFamilyService family,
        ICraftService crafts,
        IHeirloomService heirlooms,
        IWorkCapacityService workCapacity,
        IGameRandom random,
        IGameEventBus events,
        ArtisticWorkCatalog catalog)
    {
        _family = family;
        _crafts = crafts;
        _heirlooms = heirlooms;
        _workCapacity = workCapacity;
        _random = random;
        _events = events;
        _catalog = catalog;
    }

    public string Id => "heirlooms.artistic_work_production";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (!person.Tags.Has("state.alive")
                || SimulationState.IsInactive(person)
                || SimulationState.IsExternallyResident(person)
                || person.Tags.Has("state.imprisoned")
                || !_crafts.IsSelfEmployed(person))
            {
                continue;
            }

            var craft = _crafts.GetActiveCraft(person);
            if (craft is null || !_catalog.IsArtisticCraft(craft.Id))
                continue;

            var progress = _crafts.GetProgress(person, craft.Id);
            if (progress is null || progress.MasteryLevel is < 1 or > 5)
                continue;

            var productiveEffort = AnnualProductiveEffortRules.Get(
                person,
                _workCapacity);
            if (!productiveEffort.CanProduce)
                continue;

            var state = person.Components.Get<ArtisticWorkPersonComponent>();
            if (state is null)
            {
                state = new ArtisticWorkPersonComponent();
                person.Components.Set(state);
            }

            if (state.LastProductionYearByCraft.TryGetValue(craft.Id, out var lastYear)
                && lastYear == gameState.Year)
            {
                continue;
            }

            state.LastProductionYearByCraft[craft.Id] = gameState.Year;

            var masterGuaranteeDue = progress.MasteryLevel >= 5
                && !state.MasterGuaranteeCompletedCraftIds.Contains(
                    craft.Id,
                    StringComparer.OrdinalIgnoreCase);

            if (masterGuaranteeDue)
            {
                state.MasterGuaranteeCompletedCraftIds.Add(craft.Id);
                CreateWork(person, craft, progress, gameState.Year, guaranteedMasterwork: true);
                continue;
            }

            var production = _catalog.GetProductionRule(progress.MasteryLevel);
            var adjustedProductionChance = Math.Clamp(
                production.AnnualProductionChance * productiveEffort.OutputMultiplier,
                0,
                1);
            if (_random.Chance(adjustedProductionChance))
                CreateWork(person, craft, progress, gameState.Year, guaranteedMasterwork: false);
        }
    }

    private void CreateWork(
        IPerson person,
        CraftInfo craft,
        CraftProgressSnapshot progress,
        int year,
        bool guaranteedMasterwork)
    {
        var production = _catalog.GetProductionRule(progress.MasteryLevel);
        var template = _catalog.ChooseTemplate(craft.Id, progress.MasteryLevel, _random);
        var value = _random.NextInt(production.MinimumValue, production.MaximumValue);
        var triggerId = $"artistic:{person.Id}:{craft.Id}:{year}";
        var name = _family.GetDisplayName(person);

        var created = _heirlooms.Create(
            person,
            new HeirloomCreationRequest(
                template.TemplateId,
                person.Id,
                "artistic_work",
                triggerId,
                guaranteedMasterwork
                    ? $"Created as {name}'s guaranteed Master-tier {craft.Name} work."
                    : $"Created during {name}'s active self-employment as a {craft.Name}.",
                AppraisedValueOverride: value,
                RoyaltyAuthorId: template.RoyaltyAnnualRate > 0m ? person.Id : null,
                RoyaltyAnnualRate: template.RoyaltyAnnualRate));

        _events.Publish(
            new GameEvent
            {
                Type = "artistic.work_created",
                Year = year,
                SubjectId = person.Id,
                Data = new Dictionary<string, string>
                {
                    ["craftId"] = craft.Id,
                    ["craftName"] = craft.Name,
                    ["masteryLevel"] = progress.MasteryLevel.ToString(CultureInfo.InvariantCulture),
                    ["mastery"] = progress.MasteryName,
                    ["heirloomId"] = created.Id.ToString(),
                    ["item"] = created.DisplayName,
                    ["value"] = created.AppraisedValue.ToString(CultureInfo.InvariantCulture),
                    ["guaranteedMasterwork"] = guaranteedMasterwork ? "true" : "false",
                    ["familyNews"] = "true",
                    ["text"] = $"{name} created {created.DisplayName}, valued at {created.AppraisedValue:N0} zł."
                }
            });
    }
}
