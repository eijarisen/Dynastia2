using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CommunityConnectionService : IHouseholdConnectionService
{
    private static readonly string[] WealthBands = ["Poor", "Modest", "Comfortable", "Wealthy", "Rich"];

    private readonly IGameState _gameState;
    private readonly CommunityPolicyService _community;
    private readonly CommunityPolicyCatalog _catalog;
    private readonly CommunityConnectionRules _rules;
    private readonly IEconomyService _economy;
    private readonly IStatusService _status;
    private readonly ILocationService _locations;
    private readonly INationalityService _nationalities;
    private readonly IHistoricalNameService _names;
    private readonly IFamilyService _family;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public CommunityConnectionService(
        IGameState gameState,
        CommunityPolicyService community,
        CommunityPolicyCatalog catalog,
        CommunityConnectionRules rules,
        IEconomyService economy,
        IStatusService status,
        ILocationService locations,
        INationalityService nationalities,
        IHistoricalNameService names,
        IFamilyService family,
        IGameRandom random,
        IGameEventBus events)
    {
        _gameState = gameState;
        _community = community;
        _catalog = catalog;
        _rules = rules;
        _economy = economy;
        _status = status;
        _locations = locations;
        _nationalities = nationalities;
        _names = names;
        _family = family;
        _random = random;
        _events = events;
    }

    public IReadOnlyList<HouseholdConnectionInfo> GetConnections(
        Guid householdId,
        bool activeOnly = true)
    {
        var state = _community.GetWorldStateForConnections(create: false);
        if (state is null)
            return [];

        return state.Connections
            .Where(connection => connection.HouseholdId == householdId
                && (!activeOnly || connection.IsActive))
            .OrderByDescending(connection => connection.IsActive)
            .ThenByDescending(connection => connection.Renown)
            .ThenBy(connection => connection.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToInfo)
            .ToArray();
    }

    public double GetNetworkRenownBonus(Guid householdId)
    {
        var total = GetConnections(householdId)
            .Sum(connection =>
            {
                if (!connection.RelationState.Equals("Warm", StringComparison.OrdinalIgnoreCase)
                    && !connection.RelationState.Equals("Close", StringComparison.OrdinalIgnoreCase))
                {
                    return 0d;
                }
                if (connection.Renown >= _rules.NotableThreshold)
                    return connection.RelationState == "Close"
                        ? _rules.NotableCloseBonus
                        : _rules.NotableWarmBonus;
                if (connection.Renown >= _rules.ProminentThreshold)
                    return connection.RelationState == "Close"
                        ? _rules.CloseBonus
                        : _rules.WarmBonus;
                return 0d;
            });
        return Math.Min(_rules.NetworkRenownBonusCap, total);
    }

    public decimal GetEstimatedMoneyRequestMaximum(
        IPerson requester,
        Guid connectionId)
    {
        var connection = FindForActor(requester, connectionId);
        if (connection is null || !connection.IsActive)
            return 0m;

        var town = _locations.FindTownAtYear(connection.TownId, _gameState.Year)
            ?? _economy.GetResidenceTown(requester);
        var livingCostUnit = Math.Max(1m, _economy.GetLivingCostPerPerson(town));
        var units = _rules.MoneyMaximumLivingCostUnits.GetValueOrDefault(connection.WealthBand, 0);
        var maximum = livingCostUnit * units;
        return Math.Floor(Math.Max(0m, maximum) / 1000m) * 1000m;
    }

    internal CommunityConnectionState? FindForActor(IPerson actor, Guid connectionId)
    {
        var householdId = _economy.GetHouseholdId(actor);
        if (householdId is null)
            return null;
        return Find(householdId.Value, connectionId);
    }

    internal CommunityConnectionState? Find(Guid householdId, Guid connectionId) =>
        _community.GetWorldStateForConnections(create: false)?.Connections.FirstOrDefault(
            connection => connection.HouseholdId == householdId
                && connection.Id == connectionId);

    internal bool Has(Guid householdId, Guid connectionId) =>
        Find(householdId, connectionId) is not null;

    internal void InitializeLobbyConnection(
        IPerson actor,
        Guid connectionId,
        bool publishCreated)
    {
        var connection = FindForActor(actor, connectionId);
        if (connection is null)
            return;

        if (publishCreated)
        {
            connection.Familiarity = _rules.StartingFamiliarity;
            connection.Sympathy = _rules.StartingSympathy;
            var houseChance = _rules.SpareHouseChance.GetValueOrDefault(connection.WealthBand, 0d);
            var farmlandChance = _rules.SpareFarmlandChance.GetValueOrDefault(connection.WealthBand, 0d);
            if (connection.ArchetypeId.Equals("farmer", StringComparison.OrdinalIgnoreCase)
                || connection.ArchetypeId.Equals("landowner", StringComparison.OrdinalIgnoreCase))
            {
                farmlandChance = Math.Min(1d, farmlandChance * _rules.FarmerFarmlandMultiplier);
            }

            connection.HasSpareHouse = _random.Chance(houseChance);
            connection.HasSpareFarmland = _random.Chance(farmlandChance);
            Publish(actor, connection, "connection.created",
                $"The household became acquainted with {connection.Name}, a local {connection.OccupationLabel}.");
        }
    }

    internal string GetRelationState(CommunityConnectionState connection)
    {
        if (connection.Sympathy <= -20 || connection.Familiarity <= 0)
            return "Cold";
        if (connection.Sympathy < 0 || connection.Familiarity < 10)
            return "Cool";
        if (connection.Sympathy < 10 || connection.Familiarity < 25)
            return "Neutral";
        if (connection.Sympathy < 30 || connection.Familiarity < 55)
            return "Warm";
        return "Close";
    }

    internal void ImproveRelations(CommunityConnectionState connection)
    {
        connection.Familiarity = Math.Clamp(
            connection.Familiarity + _rules.ImproveFamiliarityGain, 0, 100);
        connection.Sympathy = Math.Clamp(
            connection.Sympathy + _rules.ImproveSympathyGain, -100, 100);
    }

    internal decimal GetSendMoneyMaximum(IPerson actor)
    {
        var wealth = _economy.GetHousehold(actor)?.Wealth ?? 0m;
        return Math.Floor(Math.Max(0m, wealth) / 1000m) * 1000m;
    }

    internal bool IsValidMoneyAmount(decimal amount) =>
        amount >= _rules.MinimumTransfer && amount % 1000m == 0m;

    internal bool SendMoney(IPerson actor, CommunityConnectionState connection, decimal amount)
    {
        if (!IsValidMoneyAmount(amount) || !_economy.CanAfford(actor, amount))
            return false;

        _economy.ChangeWealth(actor, -amount);
        AdjustRelation(connection, _rules.MoneyGiftFamiliarityGain, _rules.MoneyGiftSympathyGain);

        var town = _locations.FindTownAtYear(connection.TownId, _gameState.Year)
            ?? _economy.GetResidenceTown(actor);
        if (amount >= 2m * Math.Max(1m, _economy.GetLivingCostPerPerson(town)))
            MoveWealthBand(connection, +1);
        return true;
    }

    internal bool GiveHouse(IPerson actor, CommunityConnectionState connection, Guid propertyId)
    {
        var property = _economy.GetHouses(actor).FirstOrDefault(house => house.Id == propertyId);
        if (property is null || property.IsResidence || _economy.GetHouses(actor).Count <= 1)
            return false;

        if (_economy.TakeHouse(actor, propertyId) is null)
            return false;
        connection.HasSpareHouse = true;
        MoveWealthBand(connection, +1);
        AdjustRelation(connection, _rules.AssetGiftFamiliarityGain, _rules.AssetGiftSympathyGain);
        return true;
    }

    internal bool GiveFarmland(IPerson actor, CommunityConnectionState connection, Guid propertyId)
    {
        if (_economy.TakeFarmland(actor, propertyId) is null)
            return false;
        connection.HasSpareFarmland = true;
        MoveWealthBand(connection, +1);
        AdjustRelation(connection, _rules.AssetGiftFamiliarityGain, _rules.AssetGiftSympathyGain);
        return true;
    }

    internal bool RequestMoney(
        IPerson actor,
        CommunityConnectionState connection,
        decimal amount,
        out bool accepted)
    {
        accepted = false;
        if (!IsValidMoneyAmount(amount)
            || amount > GetEstimatedMoneyRequestMaximum(actor, connection.Id))
        {
            return false;
        }

        ApplyRequestBaseCost(connection);
        accepted = _random.Chance(CalculateAcceptance(actor, connection, _rules.MoneyBaseAcceptance));
        if (!accepted)
        {
            ApplyRefusalCost(connection);
            Publish(actor, connection, "connection.request_refused",
                $"{connection.Name} refused the household's request for {amount:N0} zł.");
            return true;
        }

        _economy.ChangeWealth(actor, amount);
        AdjustRelation(connection, _rules.AcceptedMoneyFamiliarityCost, _rules.AcceptedMoneySympathyCost);
        var maximum = Math.Max(_rules.MinimumTransfer, GetEstimatedMoneyRequestMaximum(actor, connection.Id));
        if (amount >= maximum * 0.5m)
            LowerWealthForRequest(actor, connection, 1);
        Publish(actor, connection, "connection.request_accepted",
            $"{connection.Name} agreed to give the household {amount:N0} zł.");
        return true;
    }

    internal bool RequestHouse(
        IPerson actor,
        CommunityConnectionState connection,
        out bool accepted)
    {
        accepted = false;
        if (!connection.HasSpareHouse)
            return false;
        ApplyRequestBaseCost(connection);
        accepted = _random.Chance(CalculateAcceptance(actor, connection, _rules.HouseBaseAcceptance));
        if (!accepted)
        {
            ApplyRefusalCost(connection);
            Publish(actor, connection, "connection.request_refused",
                $"{connection.Name} refused the household's request for a house.");
            return true;
        }

        var town = _locations.FindTownAtYear(connection.TownId, _gameState.Year)
            ?? _economy.GetResidenceTown(actor);
        _economy.AddHouse(actor, town);
        connection.HasSpareHouse = false;
        AdjustRelation(connection, _rules.AcceptedAssetFamiliarityCost, _rules.AcceptedAssetSympathyCost);
        LowerWealthForRequest(actor, connection, _rules.AssetRequestWealthBandLoss);
        EnsureWarmRelation(connection);
        Publish(actor, connection, "connection.request_accepted",
            $"{connection.Name} agreed to transfer a house to the household.");
        return true;
    }

    internal bool RequestFarmland(
        IPerson actor,
        CommunityConnectionState connection,
        out bool accepted)
    {
        accepted = false;
        if (!connection.HasSpareFarmland)
            return false;
        ApplyRequestBaseCost(connection);
        accepted = _random.Chance(CalculateAcceptance(actor, connection, _rules.FarmlandBaseAcceptance));
        if (!accepted)
        {
            ApplyRefusalCost(connection);
            Publish(actor, connection, "connection.request_refused",
                $"{connection.Name} refused the household's request for farmland.");
            return true;
        }

        var town = _locations.FindTownAtYear(connection.TownId, _gameState.Year)
            ?? _economy.GetResidenceTown(actor);
        _economy.AddFarmland(actor, town, _gameState.Year, "connection-request");
        connection.HasSpareFarmland = false;
        AdjustRelation(connection, _rules.AcceptedAssetFamiliarityCost, _rules.AcceptedAssetSympathyCost);
        LowerWealthForRequest(actor, connection, _rules.AssetRequestWealthBandLoss);
        EnsureWarmRelation(connection);
        Publish(actor, connection, "connection.request_accepted",
            $"{connection.Name} agreed to transfer farmland to the household.");
        return true;
    }

    internal void AdvanceYear()
    {
        var state = _community.GetWorldStateForConnections(create: false);
        if (state is null)
            return;

        foreach (var connection in state.Connections.Where(item => item.IsActive).ToArray())
        {
            var representative = FindRepresentative(connection.HouseholdId);
            var age = Math.Max(0, _gameState.Year - connection.BirthYear);
            if (_random.Chance(MortalityChance(age)))
            {
                connection.DeathYear = _gameState.Year;
                connection.IsActive = false;
                if (representative is not null)
                    Publish(representative, connection, "connection.lost", $"The household lost contact with {connection.Name} after their death.");
                continue;
            }

            SimulateSimpleFamily(connection, age);
            DriftWealth(connection);
            connection.Familiarity = Math.Max(0, connection.Familiarity - _rules.FamiliarityDecayPerYear);
            if (connection.Sympathy > 0)
                connection.Sympathy = Math.Max(0, connection.Sympathy - _rules.SympathyDriftTowardNeutralPerYear);
            else if (connection.Sympathy < 0)
                connection.Sympathy = Math.Min(0, connection.Sympathy + _rules.SympathyDriftTowardNeutralPerYear);

            var relationState = GetRelationState(connection);
            var weakPoorConnection = connection.WealthBand.Equals(
                    "Poor",
                    StringComparison.OrdinalIgnoreCase)
                && !relationState.Equals("Warm", StringComparison.OrdinalIgnoreCase)
                && !relationState.Equals("Close", StringComparison.OrdinalIgnoreCase);
            if (weakPoorConnection || connection.Familiarity <= 0)
            {
                connection.IsActive = false;
                if (representative is not null)
                    Publish(representative, connection, "connection.lost", $"The household lost contact with {connection.Name}.");
            }
        }
    }

    private HouseholdConnectionInfo ToInfo(CommunityConnectionState connection) =>
        new(
            connection.Id,
            connection.HouseholdId,
            connection.Name,
            connection.Sex,
            Math.Max(0, _gameState.Year - connection.BirthYear),
            connection.DeathYear,
            connection.NationalityId,
            connection.TownId,
            connection.OccupationLabel,
            connection.ArchetypeId,
            connection.WealthBand,
            connection.Renown,
            connection.Reputation,
            connection.Familiarity,
            connection.Sympathy,
            GetRelationState(connection),
            connection.SpouseName,
            connection.Children.ToArray(),
            connection.HasSpareHouse,
            connection.HasSpareFarmland,
            connection.OriginPolicyId,
            connection.IsActive);

    private double CalculateAcceptance(
        IPerson actor,
        CommunityConnectionState connection,
        double baseChance)
    {
        var social = _status.GetHouseholdStatus(actor);
        var relationship = _rules.RelationshipAcceptancePoints.GetValueOrDefault(GetRelationState(connection), 0d);
        var wealth = _rules.WealthAcceptancePoints.GetValueOrDefault(connection.WealthBand, 0d);
        var reputation = Math.Clamp(social.Reputation / 10d, -5d, 5d);
        var renown = Math.Clamp(social.Renown / 25d, 0d, 3d);
        return Math.Clamp(baseChance + (relationship + wealth + reputation + renown) / 100d, 0d, 0.45d);
    }

    private void ApplyRequestBaseCost(CommunityConnectionState connection) =>
        AdjustRelation(connection, _rules.RequestFamiliarityCost, _rules.RequestSympathyCost);

    private void ApplyRefusalCost(CommunityConnectionState connection) =>
        AdjustRelation(connection, _rules.RefusalFamiliarityCost, _rules.RefusalSympathyCost);

    private static void AdjustRelation(CommunityConnectionState connection, int familiarityDelta, int sympathyDelta)
    {
        connection.Familiarity = Math.Clamp(connection.Familiarity + familiarityDelta, 0, 100);
        connection.Sympathy = Math.Clamp(connection.Sympathy + sympathyDelta, -100, 100);
    }

    private static void EnsureWarmRelation(CommunityConnectionState connection)
    {
        // Giving away a major asset is strong evidence of trust. Keep a small
        // buffer above the Warm thresholds so the normal same-year annual
        // familiarity/sympathy drift does not immediately erase that result.
        connection.Familiarity = Math.Max(connection.Familiarity, 30);
        connection.Sympathy = Math.Max(connection.Sympathy, 15);
    }

    private void LowerWealthForRequest(IPerson actor, CommunityConnectionState connection, int bands)
    {
        var before = connection.WealthBand;
        MoveWealthBand(connection, -Math.Max(0, bands));
        if (!before.Equals("Poor", StringComparison.OrdinalIgnoreCase)
            && connection.WealthBand.Equals("Poor", StringComparison.OrdinalIgnoreCase))
        {
            _status.ApplyPersistentDelta(
                actor,
                0,
                _rules.RequestCausedPoorReputationPenalty,
                "connection.request_caused_poverty");
        }
    }

    private static void MoveWealthBand(CommunityConnectionState connection, int offset)
    {
        var current = Array.FindIndex(WealthBands, band => band.Equals(connection.WealthBand, StringComparison.OrdinalIgnoreCase));
        if (current < 0)
            current = 1;
        connection.WealthBand = WealthBands[Math.Clamp(current + offset, 0, WealthBands.Length - 1)];
    }

    private void DriftWealth(CommunityConnectionState connection)
    {
        if (!_random.Chance(_rules.WealthDriftChance))
            return;
        var archetype = _catalog.GetArchetype(connection.ArchetypeId);
        var current = Array.FindIndex(WealthBands, band => band.Equals(connection.WealthBand, StringComparison.OrdinalIgnoreCase));
        var min = Array.FindIndex(WealthBands, band => band.Equals(archetype.MinimumWealthBand, StringComparison.OrdinalIgnoreCase));
        var max = Array.FindIndex(WealthBands, band => band.Equals(archetype.MaximumWealthBand, StringComparison.OrdinalIgnoreCase));
        var direction = current < min ? 1 : current > max ? -1 : (_random.Chance(0.5) ? 1 : -1);
        MoveWealthBand(connection, direction);
    }

    private void SimulateSimpleFamily(CommunityConnectionState connection, int age)
    {
        if (connection.SpouseName is null && age >= 18 && _random.Chance(_rules.MarriageChance))
        {
            var spouseSex = connection.Sex == Sex.Male ? Sex.Female : Sex.Male;
            connection.SpouseName = GenerateName(connection, spouseSex, Math.Max(18, age - 5));
        }
        else if (connection.SpouseName is not null)
        {
            if (_random.Chance(MortalityChance(age)))
                connection.SpouseName = null;
            else if (_random.Chance(_rules.DivorceChance))
                connection.SpouseName = null;
        }

        if (connection.SpouseName is not null
            && age is >= 18 and <= 50
            && connection.Children.Count < _rules.MaximumChildren
            && _random.Chance(_rules.ChildChance))
        {
            var childSex = _random.Chance(0.5) ? Sex.Male : Sex.Female;
            connection.Children.Add(GenerateName(connection, childSex, 0));
        }
    }

    private string GenerateName(CommunityConnectionState connection, Sex sex, int age)
    {
        var culture = _nationalities.GetNameCultureId(connection.NationalityId);
        var first = _names.GetRandomFirstName(sex, _gameState.Year - age, culture, _random);
        var surname = connection.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
            ?? _names.GetRandomSurname(sex, culture, _random);
        return $"{first} {_names.FormatSurname(surname, sex, culture)}";
    }

    private double MortalityChance(int age)
    {
        var band = _rules.MortalityBands.FirstOrDefault(item =>
            age >= item.MinAge && (!item.MaxAge.HasValue || age <= item.MaxAge.Value));
        return band?.AnnualChance ?? 0d;
    }

    private IPerson? FindRepresentative(Guid householdId) =>
        _gameState.People.FirstOrDefault(person =>
            person.Tags.Has("state.alive")
            && _economy.GetHouseholdId(person) == householdId
            && person.Tags.Has("control.playable"))
        ?? _gameState.People.FirstOrDefault(person =>
            person.Tags.Has("state.alive")
            && _economy.GetHouseholdId(person) == householdId);

    private void Publish(IPerson actor, CommunityConnectionState connection, string type, string text)
    {
        _events.Publish(new GameEvent
        {
            Type = type,
            Year = _gameState.Year,
            SubjectId = actor.Id,
            Data = new Dictionary<string, string>
            {
                ["connectionId"] = connection.Id.ToString("D"),
                ["connectionName"] = connection.Name,
                ["text"] = text,
                ["familyNews"] = "true"
            }
        });
    }
}
