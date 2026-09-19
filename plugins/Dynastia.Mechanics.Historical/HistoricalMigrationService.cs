using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

internal sealed class HistoricalMigrationService
{
    private readonly IGameState _state;
    private readonly IEconomyService _economy;
    private readonly IHistoricalTownCatalog _towns;
    private readonly ICareerService _career;
    private readonly IFamilyService _family;
    private readonly ISuccessionService _succession;
    private readonly IFarmingService? _farming;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public HistoricalMigrationService(
        IGameState state,
        IEconomyService economy,
        IHistoricalTownCatalog towns,
        ICareerService career,
        IFamilyService family,
        ISuccessionService succession,
        IFarmingService? farming,
        IGameRandom random,
        IGameEventBus events)
    {
        _state = state;
        _economy = economy;
        _towns = towns;
        _career = career;
        _family = family;
        _succession = succession;
        _farming = farming;
        _random = random;
        _events = events;
    }

    public HistoricalMigrationResult Apply(
        IPerson head,
        HistoricalScheduledEvent historicalEvent,
        HistoricalMigrationRoute route,
        bool forced)
    {
        return route.Type.ToLowerInvariant() switch
        {
            "internal_household" => RelocateInternally(head, historicalEvent, route),
            "external_household" => MoveExternal(head, historicalEvent, route, forced),
            "external_branch" => MoveExternalBranch(head, historicalEvent, route, forced),
            _ => HistoricalMigrationResult.NotApplied
        };
    }

    private HistoricalMigrationResult RelocateInternally(
        IPerson head,
        HistoricalScheduledEvent historicalEvent,
        HistoricalMigrationRoute route)
    {
        var origin = _economy.GetResidenceTown(head);
        var candidates = _towns.GetAvailableTowns(_state.Year)
            .Where(town => route.TargetRegionIds?.Contains(town.RegionId, StringComparer.OrdinalIgnoreCase) == true)
            .Where(town => route.ExcludePlaceIds?.Contains(town.Id, StringComparer.OrdinalIgnoreCase) != true)
            .Where(town => !town.Id.Equals(origin.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
            return HistoricalMigrationResult.NotApplied;

        var destination = ChooseDestination(origin, candidates, route);
        var memberIds = _economy.GetHouseholdMemberIds(head).ToHashSet();
        var workers = _state.People
            .Where(person => memberIds.Contains(person.Id)
                && person.Tags.Has("state.alive")
                && person.Age >= 18)
            .Where(person =>
            {
                var snapshot = _career.GetCareer(person);
                return snapshot.IsEmployed && !snapshot.IsRetired && !snapshot.IsSelfEmployed;
            })
            .ToList();

        var priorWealth = _economy.GetHousehold(head)?.Wealth ?? 0m;
        var priorHouseCount = _economy.GetHouses(head).Count;
        var priorFarmlandCount = _economy.GetFarmland(head).Count;
        var housesConfiscated = route.LoseOriginHouse.ValueKind == JsonValueKind.True
            ? priorHouseCount
            : 0;
        var farmlandConfiscated = route.LoseOriginFarmland.ValueKind == JsonValueKind.True
            ? priorFarmlandCount
            : 0;
        ApplyOriginPropertyPolicy(head, route);
        ApplyCashRetention(head, route);
        var postPolicyWealth = _economy.GetHousehold(head)?.Wealth ?? 0m;

        _economy.SetResidenceTown(head, destination);

        foreach (var worker in workers)
            _career.RelocateEmployment(worker);

        if (priorHouseCount > 0
            && route.ReplacementHouseChance > 0
            && _random.Chance(route.ReplacementHouseChance))
        {
            _economy.AddHouse(head, destination);
        }

        var farmlandReplacementChance = route.ReplacementFarmlandChanceIfPreviouslyOwned > 0
            ? route.ReplacementFarmlandChanceIfPreviouslyOwned
            : route.ReplacementFarmlandChance;
        if (priorFarmlandCount > 0
            && farmlandReplacementChance > 0
            && _random.Chance(farmlandReplacementChance))
        {
            _economy.AddFarmland(
                head,
                destination,
                _state.Year,
                $"historical_event:{historicalEvent.Id}");
        }

        _events.Publish(new GameEvent
        {
            Type = "historical.relocation",
            Year = _state.Year,
            SubjectId = head.Id,
            RelatedPersonIds = memberIds.Where(id => id != head.Id).ToList(),
            Data = new Dictionary<string, string>
            {
                ["eventId"] = historicalEvent.Id,
                ["fromTown"] = origin.Town,
                ["toTown"] = destination.Town,
                ["familyNews"] = "false",
                ["suppressChronicle"] = "true",
                ["text"] = $"The {head.Surname} household was relocated from {origin.Town} to {destination.Town} during {historicalEvent.DisplayName}."
            }
        });

        return new HistoricalMigrationResult(
            true,
            origin.Town,
            destination.Town,
            Math.Max(0m, priorWealth - postPolicyWealth),
            housesConfiscated,
            farmlandConfiscated);
    }

    private HistoricalMigrationResult MoveExternalBranch(
        IPerson head,
        HistoricalScheduledEvent historicalEvent,
        HistoricalMigrationRoute route,
        bool forced)
    {
        // The supplied branch routes are meant for autonomous adult branches.
        // Never automatically remove the authoritative lineage household for
        // voluntary migration opportunities.
        // Voluntary branch emigration may remove an autonomous lineage or
        // bloodline household, but never the household the player currently
        // controls.  Forced departures are allowed to move the active
        // household; the normal Succession phase will then transfer control
        // to another in-Poland lineage household or end the run with the
        // distinct dynasty_left_poland state.
        if (!forced && _succession.ActiveControllerId == head.Id)
            return HistoricalMigrationResult.NotApplied;

        return MoveExternal(head, historicalEvent, route, forced);
    }

    private HistoricalMigrationResult MoveExternal(
        IPerson head,
        HistoricalScheduledEvent historicalEvent,
        HistoricalMigrationRoute route,
        bool forced)
    {
        var destination = route.DestinationLabel;
        if (string.IsNullOrWhiteSpace(destination))
            return HistoricalMigrationResult.NotApplied;

        var origin = _economy.GetResidenceTown(head);
        var priorWealth = _economy.GetHousehold(head)?.Wealth ?? 0m;
        var priorHouseCount = _economy.GetHouses(head).Count;
        var priorFarmlandCount = _economy.GetFarmland(head).Count;
        var housesConfiscated = route.LoseOriginHouse.ValueKind == JsonValueKind.True
            ? priorHouseCount
            : 0;
        var farmlandConfiscated = route.LoseOriginFarmland.ValueKind == JsonValueKind.True
            ? priorFarmlandCount
            : 0;

        var members = _economy.GetHouseholdMemberIds(head)
            .Select(id => _state.People.FirstOrDefault(person => person.Id == id))
            .Where(person => person is not null && person.Tags.Has("state.alive"))
            .Cast<IPerson>()
            .ToList();
        if (members.All(person => person.Id != head.Id))
            members.Insert(0, head);

        ApplyOriginPropertyPolicy(head, route);
        ApplyCashRetention(head, route);
        var postPolicyWealth = _economy.GetHousehold(head)?.Wealth ?? 0m;

        foreach (var person in members)
        {
            person.Tags.Add(SimulationState.ExternalResidenceTag);
            person.Components.Set(new ExternalResidenceComponent
            {
                DestinationLabel = destination,
                DepartureYear = _state.Year,
                CauseEventId = historicalEvent.Id,
                Forced = forced
            });

            if (person.Age >= 18 && _career.GetCareer(person).IsEmployed)
                _career.SetJobLevel(person, 0);
        }

        _events.Publish(new GameEvent
        {
            Type = "historical.external_departure",
            Year = _state.Year,
            SubjectId = head.Id,
            RelatedPersonIds = members.Where(person => person.Id != head.Id).Select(person => person.Id).ToList(),
            Data = new Dictionary<string, string>
            {
                ["eventId"] = historicalEvent.Id,
                ["fromTown"] = origin.Town,
                ["destination"] = destination,
                ["forced"] = forced ? "true" : "false",
                ["familyNews"] = "false",
                ["suppressChronicle"] = "true",
                ["text"] = forced
                    ? $"The {head.Surname} household was forced to leave {origin.Town} for {destination} during {historicalEvent.DisplayName}."
                    : $"The {head.Surname} household left {origin.Town} for {destination} during {historicalEvent.DisplayName}."
            }
        });

        return new HistoricalMigrationResult(
            true,
            origin.Town,
            destination,
            Math.Max(0m, priorWealth - postPolicyWealth),
            housesConfiscated,
            farmlandConfiscated);
    }

    private void ApplyOriginPropertyPolicy(
        IPerson head,
        HistoricalMigrationRoute route)
    {
        ApplyHousePolicy(head, route.LoseOriginHouse);
        ApplyFarmlandPolicy(head, route.LoseOriginFarmland);
    }

    private void ApplyHousePolicy(IPerson head, JsonElement policy)
    {
        if (policy.ValueKind == JsonValueKind.True)
        {
            _economy.TakeAllHouses(head);
            return;
        }

        if (!IsLiquidation(policy))
            return;

        var houses = _economy.TakeAllHouses(head);
        var proceeds = houses.Sum(house => _economy.GetHouseSaleValue(house.Town));
        if (proceeds > 0)
            _economy.ChangeWealth(head, proceeds);
    }

    private void ApplyFarmlandPolicy(IPerson head, JsonElement policy)
    {
        if (policy.ValueKind == JsonValueKind.True)
        {
            _economy.TakeAllFarmland(head);
            return;
        }

        if (!IsLiquidation(policy))
            return;

        var farmland = _economy.TakeAllFarmland(head);
        var parcelSaleValue = _farming?.SalePrice ?? 8000m;
        var proceeds = farmland.Count * parcelSaleValue;
        if (proceeds > 0)
            _economy.ChangeWealth(head, proceeds);
    }

    private static bool IsLiquidation(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
        && element.GetString()?.Equals(
            "liquidate_at_existing_sale_rules_before_departure",
            StringComparison.OrdinalIgnoreCase) == true;

    private void ApplyCashRetention(IPerson head, HistoricalMigrationRoute route)
    {
        if (route.CashRetention is not { Length: >= 2 })
            return;

        var household = _economy.GetHousehold(head);
        if (household is null || household.Wealth <= 0)
            return;

        var fraction = RollRange(route.CashRetention);
        _economy.SetWealth(head, Math.Round(household.Wealth * (decimal)fraction, 0, MidpointRounding.AwayFromZero));
    }

    private TownInfo ChooseDestination(
        TownInfo origin,
        IReadOnlyList<TownInfo> candidates,
        HistoricalMigrationRoute route)
    {
        var weighted = candidates.Select(town =>
        {
            var weight = Math.Sqrt(Math.Max(1, town.Population));
            if (route.RegionWeights?.TryGetValue(town.RegionId, out var regionWeight) == true)
                weight *= regionWeight;

            if (route.DestinationWeighting?.Contains("inverse_distance", StringComparison.OrdinalIgnoreCase) == true)
            {
                var dx = town.Longitude - origin.Longitude;
                var dy = town.Latitude - origin.Latitude;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                weight /= Math.Max(0.25, distance);
            }

            return (Town: town, Weight: Math.Max(0.0001, weight));
        }).ToList();

        var total = weighted.Sum(item => item.Weight);
        var roll = _random.NextDouble() * total;
        foreach (var item in weighted)
        {
            if (roll < item.Weight)
                return item.Town;
            roll -= item.Weight;
        }
        return weighted[^1].Town;
    }

    private double RollRange(double[] range)
    {
        if (range.Length == 0)
            return 0;
        if (range.Length == 1)
            return range[0];
        return range[0] + (range[1] - range[0]) * _random.NextDouble();
    }

    private static bool IsTrue(JsonElement element) =>
        element.ValueKind == JsonValueKind.True;
}

internal sealed record HistoricalMigrationResult(
    bool Applied,
    string? FromLocation,
    string? ToLocation,
    decimal CashLost,
    int HousesConfiscated,
    int FarmlandConfiscated)
{
    public static HistoricalMigrationResult NotApplied { get; } =
        new(false, null, null, 0m, 0, 0);
}

