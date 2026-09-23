using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

public sealed class EstateInheritanceSystem :
    IYearSystem
{
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IHeirloomService _heirlooms;
    private readonly Func<IFarmingService?> _farmingResolver;
    private readonly IGameEventBus _events;

    public EstateInheritanceSystem(
        IFamilyService family,
        IEconomyService economy,
        IHeirloomService heirlooms,
        IGameEventBus events)
        : this(
            family,
            economy,
            heirlooms,
            () => null,
            events)
    {
    }

    public EstateInheritanceSystem(
        IFamilyService family,
        IEconomyService economy,
        IHeirloomService heirlooms,
        Func<IFarmingService?> farmingResolver,
        IGameEventBus events)
    {
        _family =
            family;

        _economy =
            economy;

        _heirlooms =
            heirlooms;

        _farmingResolver =
            farmingResolver;

        _events =
            events;
    }

    public string Id =>
        "inheritance.estate_settlement";

    public YearPhase Phase =>
        YearPhase.Inheritance;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["households.inheritance_reconcile"];

    public void Execute(
        IGameState gameState)
    {
        var readyHouseholds =
            gameState.People
                .Where(
                    person =>
                        _economy.HasHousehold(
                            person)
                        && _economy.IsEstateReady(
                            person))
                .ToList();

        foreach (var head in
            readyHouseholds)
        {
            SettleHouseholdEstate(
                gameState,
                head);
        }
    }

    private void SettleHouseholdEstate(
        IGameState gameState,
        IPerson head)
    {
        var anchorId =
            _economy
                .GetHouseholdDynastyAnchorId(
                    head);

        var anchor =
            anchorId is Guid id
                ? gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id == id)
                : null;

        var finance =
            _economy.GetHousehold(
                head);

        if (finance is null)
        {
            _economy.DissolveHousehold(
                head);

            return;
        }

        var estateHouseholdId =
            _economy.GetHouseholdId(
                head);

        // A household can also dissolve because its living bloodline
        // anchor left it after separation. That is not an inheritance
        // event: remaining assets follow the living bloodline anchor.
        if (anchor is not null
            && anchor.Tags.Has(
                "state.alive"))
        {
            TransferResidualAssetsToLivingAnchor(
                gameState,
                head,
                anchor,
                finance,
                estateHouseholdId);

            return;
        }

        var houses =
            _economy
                .TakeAllHouses(
                    head)
                .ToList();

        var farmland =
            _economy
                .TakeAllFarmland(
                    head)
                .ToList();

        var heirlooms =
            _heirlooms
                .TakeAll(
                    head)
                .ToList();

        var wealth =
            finance.Wealth;

        var heirResolution =
            ResolveEstateHeirs(
                gameState,
                anchor);

        var heirs =
            heirResolution.Heirs;

        if (heirs.Count == 0)
        {
            _economy.SetWealth(
                head,
                0);

            _events.Publish(
                new GameEvent
                {
                    Type =
                        "inheritance.estate_left_dynasty",

                    Year =
                        gameState.Year,

                    SubjectId =
                        anchor?.Id
                        ?? head.Id,

                    RelatedPersonIds =
                        [head.Id],

                    Data =
                        new Dictionary<string, string>
                        {
                            ["amount"] =
                                wealth.ToString(),

                            ["houses"] =
                                houses.Count.ToString(),

                            ["farmland"] =
                                farmland.Count.ToString(),

                            ["heirlooms"] =
                                heirlooms.Count.ToString(),

                            ["text"] =
                                $"The remaining estate of " +
                                $"{(anchor is null ? _family.GetDisplayName(head) : _family.GetDisplayName(anchor))} " +
                                "was lost because no eligible living relatives remained to inherit it."
                        }
                });

            _economy.DissolveHousehold(
                head);

            return;
        }

        DistributeHouses(
            gameState,
            anchor
            ?? head,
            heirs,
            houses,
            estateHouseholdId);

        DistributeFarmland(
            gameState,
            anchor
            ?? head,
            heirs,
            farmland,
            estateHouseholdId);

        DistributeHeirlooms(
            gameState,
            anchor
            ?? head,
            heirs,
            heirlooms,
            estateHouseholdId);

        DistributeCash(
            gameState,
            anchor
            ?? head,
            heirs,
            wealth,
            estateHouseholdId);

        PublishInheritanceDisadvantage(
            gameState,
            anchor ?? head,
            heirs,
            houses,
            farmland,
            heirlooms);

        _economy.SetWealth(
            head,
            0);

        _events.Publish(
            new GameEvent
            {
                Type =
                    "inheritance.estate_settled",

                Year =
                    gameState.Year,

                SubjectId =
                    anchor?.Id
                    ?? head.Id,

                RelatedPersonIds =
                    heirs
                        .Select(
                            heir =>
                                heir.Id)
                        .ToList(),

                Data =
                    new Dictionary<string, string>
                    {
                        ["estate"] =
                            wealth.ToString(),

                        ["houses"] =
                            houses.Count.ToString(),

                        ["farmland"] =
                            farmland.Count.ToString(),

                        ["heirlooms"] =
                            heirlooms.Count.ToString(),

                        ["heirs"] =
                            heirs.Count.ToString(),

                        ["text"] =
                            $"The remaining household estate of " +
                            $"{(anchor is null ? _family.GetDisplayName(head) : _family.GetDisplayName(anchor))} " +
                            $"was divided among {heirResolution.Description}."
                    }
            });

        _economy.DissolveHousehold(
            head);
    }

    private void TransferResidualAssetsToLivingAnchor(
        IGameState gameState,
        IPerson oldHead,
        IPerson anchor,
        HouseholdFinanceSnapshot finance,
        Guid? oldHouseholdId)
    {
        var houses =
            _economy
                .TakeAllHouses(
                    oldHead)
                .ToList();

        var farmland =
            _economy
                .TakeAllFarmland(
                    oldHead)
                .ToList();

        var heirlooms =
            _heirlooms
                .TakeAll(
                    oldHead)
                .ToList();

        var wealth =
            finance.Wealth;

        var anchorHouseholdId =
            _economy.GetHouseholdId(
                anchor);

        var hasEstablishedHousehold =
            anchor.Age >= 18
            && anchorHouseholdId is not null
            && anchorHouseholdId
                != oldHouseholdId;

        if (hasEstablishedHousehold)
        {
            if (wealth > 0)
            {
                _economy.ChangeWealth(
                    anchor,
                    wealth);
            }
            else if (wealth < 0)
            {
                _economy.ChangeWealthAllowDebt(
                    anchor,
                    wealth);
            }

            foreach (var house in
                houses)
            {
                _economy.AddExistingHouse(
                    anchor,
                    house);
            }

            foreach (var parcel in farmland)
            {
                _economy.AddExistingFarmland(
                    anchor,
                    parcel);
            }

            foreach (var heirloom in heirlooms)
            {
                _heirlooms.AddExisting(
                    anchor,
                    heirloom,
                    gameState.Year,
                    anchor.Id,
                    "household_transfer");
            }
        }
        else
        {
            if (wealth != 0)
            {
                _economy.ChangePendingInheritance(
                    anchor,
                    wealth);
            }

            foreach (var house in
                houses)
            {
                _economy.AddPendingHouse(
                    anchor,
                    house);
            }

            foreach (var parcel in farmland)
            {
                _economy.AddPendingFarmland(
                    anchor,
                    parcel);
            }

            foreach (var heirloom in heirlooms)
            {
                _heirlooms.AddPending(
                    anchor,
                    heirloom,
                    gameState.Year,
                    "household_transfer_pending");
            }
        }

        _economy.SetWealth(
            oldHead,
            0);

        _events.Publish(
            new GameEvent
            {
                Type =
                    "household.assets_followed_anchor",

                Year =
                    gameState.Year,

                SubjectId =
                    anchor.Id,

                RelatedPersonIds =
                    [oldHead.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["amount"] =
                            wealth.ToString(),

                        ["houses"] =
                            houses.Count.ToString(),

                        ["farmland"] =
                            farmland.Count.ToString(),

                        ["heirlooms"] =
                            heirlooms.Count.ToString(),

                        ["text"] =
                            $"The remaining assets of " +
                            $"{_family.GetDisplayName(anchor)}'s former household " +
                            "followed the living bloodline member when that household ended."
                    }
            });

        _economy.DissolveHousehold(
            oldHead);
    }

    private void DistributeHouses(
        IGameState gameState,
        IPerson source,
        IReadOnlyList<IPerson> heirs,
        IReadOnlyList<HousePropertyInfo> houses,
        Guid? estateHouseholdId)
    {
        if (houses.Count == 0)
            return;

        var received =
            heirs.ToDictionary(
                heir =>
                    heir.Id,
                _ =>
                    new List<HousePropertyInfo>());

        var heirsById =
            heirs.ToDictionary(
                heir => heir.Id);

        var recipients =
            HouseInheritanceAssignmentRules.ResolveRecipients(
                heirs.Select(heir => heir.Id).ToList(),
                houses.Select(house => house.AssignedHeirId).ToList());

        for (var index = 0;
            index < houses.Count;
            index++)
        {
            var heir =
                heirsById[recipients[index]];

            // A designation belongs to the deceased household. Once the
            // property reaches its recipient it becomes ordinary property
            // in that recipient's estate until they designate an heir.
            var house =
                houses[index] with
                {
                    AssignedHeirId = null
                };

            var hasOwnHousehold =
                HasEstablishedHouseholdOutsideEstate(
                    heir,
                    estateHouseholdId);

            if (hasOwnHousehold)
            {
                _economy.AddExistingHouse(
                    heir,
                    house);
            }
            else
            {
                _economy.AddPendingHouse(
                    heir,
                    house);
            }

            received[heir.Id]
                .Add(
                    house);
        }

        foreach (var heir in
            heirs)
        {
            var inherited =
                received[heir.Id];

            if (inherited.Count == 0)
                continue;

            _events.Publish(
                new GameEvent
                {
                    Type =
                        HasEstablishedHouseholdOutsideEstate(
                            heir,
                            estateHouseholdId)
                            ? "inheritance.houses"
                            : "inheritance.pending_houses",

                    Year =
                        gameState.Year,

                    SubjectId =
                        heir.Id,

                    RelatedPersonIds =
                        [source.Id],

                    Data =
                        new Dictionary<string, string>
                        {
                            ["count"] =
                                inherited.Count.ToString(),

                            ["towns"] =
                                string.Join(
                                    ", ",
                                    inherited.Select(
                                        house =>
                                            house.Town.Town)),

                            ["text"] =
                                $"{_family.GetDisplayName(heir)} " +
                                $"inherited {inherited.Count} " +
                                $"house{(inherited.Count == 1 ? "" : "s")}."
                        }
                });
        }
    }

    private void DistributeFarmland(
        IGameState gameState,
        IPerson source,
        IReadOnlyList<IPerson> heirs,
        IReadOnlyList<FarmlandAssetInfo> farmland,
        Guid? estateHouseholdId)
    {
        if (farmland.Count == 0 || heirs.Count == 0)
            return;

        var received =
            heirs.ToDictionary(
                heir => heir.Id,
                _ => new List<FarmlandAssetInfo>());

        var heirsById =
            heirs.ToDictionary(heir => heir.Id);

        var recipients =
            HouseInheritanceAssignmentRules.ResolveRecipients(
                heirs.Select(heir => heir.Id).ToList(),
                farmland.Select(parcel => parcel.AssignedHeirId).ToList());

        for (var index = 0; index < farmland.Count; index++)
        {
            var heir = heirsById[recipients[index]];
            var parcel = farmland[index] with
            {
                AcquiredYear = gameState.Year,
                AcquisitionSource = "inheritance",
                AssignedHeirId = null
            };

            if (HasEstablishedHouseholdOutsideEstate(
                    heir,
                    estateHouseholdId))
            {
                _economy.AddExistingFarmland(
                    heir,
                    parcel);
            }
            else
            {
                _economy.AddPendingFarmland(
                    heir,
                    parcel);
            }

            received[heir.Id].Add(parcel);
        }

        foreach (var heir in heirs)
        {
            var inherited = received[heir.Id];
            if (inherited.Count == 0)
                continue;

            _events.Publish(
                new GameEvent
                {
                    Type = "farmland.inherited",
                    Year = gameState.Year,
                    SubjectId = heir.Id,
                    RelatedPersonIds = [source.Id],
                    Data = new Dictionary<string, string>
                    {
                        ["count"] = inherited.Count.ToString(),
                        ["towns"] = string.Join(
                            ", ",
                            inherited.Select(asset => asset.Town.Town)),
                        ["text"] =
                            $"{_family.GetDisplayName(heir)} inherited " +
                            $"{inherited.Count} parcel" +
                            $"{(inherited.Count == 1 ? "" : "s")} of farmland."
                    }
                });
        }
    }

    private void DistributeHeirlooms(
        IGameState gameState,
        IPerson source,
        IReadOnlyList<IPerson> heirs,
        IReadOnlyList<HeirloomAssetInfo> heirlooms,
        Guid? estateHouseholdId)
    {
        if (heirlooms.Count == 0 || heirs.Count == 0)
            return;

        var heirsById = heirs.ToDictionary(heir => heir.Id);
        var recipients = HouseInheritanceAssignmentRules.ResolveRecipients(
            heirs.Select(heir => heir.Id).ToList(),
            heirlooms.Select(item => item.AssignedHeirId).ToList());

        for (var index = 0; index < heirlooms.Count; index++)
        {
            var heir = heirsById[recipients[index]];
            var item = heirlooms[index] with { AssignedHeirId = null };
            var established = HasEstablishedHouseholdOutsideEstate(
                heir,
                estateHouseholdId);

            if (established)
            {
                _heirlooms.AddExisting(
                    heir,
                    item,
                    gameState.Year,
                    heir.Id,
                    "inherited");
            }
            else
            {
                _heirlooms.AddPending(
                    heir,
                    item,
                    gameState.Year,
                    "inherited_pending");
            }

            _events.Publish(
                new GameEvent
                {
                    Type = established
                        ? "heirloom.inherited"
                        : "heirloom.pending",
                    Year = gameState.Year,
                    SubjectId = heir.Id,
                    RelatedPersonIds = [source.Id],
                    Data = new Dictionary<string, string>
                    {
                        ["heirloomId"] = item.Id.ToString(),
                        ["item"] = item.DisplayName,
                        ["familyNews"] = "true",
                        ["text"] = established
                            ? $"{_family.GetDisplayName(heir)} inherited {item.DisplayName} from the family estate."
                            : $"{item.DisplayName} was set aside for {_family.GetDisplayName(heir)} until the inheritance can be received."
                    }
                });
        }
    }

    private void DistributeCash(
        IGameState gameState,
        IPerson source,
        IReadOnlyList<IPerson> heirs,
        decimal wealth,
        Guid? estateHouseholdId)
    {
        if (wealth == 0 || heirs.Count == 0)
            return;

        // Whole-zł estate balances are split by absolute amount, then the
        // original sign is restored. This conserves both inherited cash and
        // inherited household debt with the existing eldest-first remainder.
        var wholeUnits = decimal.ToInt64(
            Math.Round(wealth, 0, MidpointRounding.AwayFromZero));
        if (wholeUnits == 0)
            return;

        var sign = Math.Sign(wholeUnits);
        var units = Math.Abs(wholeUnits);
        var baseUnits = units / heirs.Count;
        var remainder = units % heirs.Count;

        for (var index = 0; index < heirs.Count; index++)
        {
            var heir = heirs[index];
            var signedUnits = sign * (baseUnits + (index < remainder ? 1L : 0L));
            var amount = (decimal)signedUnits;
            if (amount == 0)
                continue;

            var hasOwnHousehold =
                HasEstablishedHouseholdOutsideEstate(heir, estateHouseholdId);

            if (hasOwnHousehold)
            {
                if (amount > 0)
                    _economy.ChangeWealth(heir, amount);
                else
                    _economy.ChangeWealthAllowDebt(heir, amount);

                PublishCashEvent(
                    gameState,
                    source,
                    heir,
                    amount,
                    amount > 0 ? "inheritance.received" : "inheritance.debt_received",
                    amount > 0
                        ? $"{_family.GetDisplayName(heir)} received an inheritance of {amount:N0} zł."
                        : $"{_family.GetDisplayName(heir)} inherited {Math.Abs(amount):N0} zł of household debt.");
            }
            else
            {
                _economy.ChangePendingInheritance(heir, amount);

                PublishCashEvent(
                    gameState,
                    source,
                    heir,
                    amount,
                    amount > 0 ? "inheritance.pending" : "inheritance.debt_pending",
                    amount > 0
                        ? $"{_family.GetDisplayName(heir)} has an inheritance of {amount:N0} zł waiting until they establish a household."
                        : $"{_family.GetDisplayName(heir)} has {Math.Abs(amount):N0} zł of inherited debt waiting until they establish a household.");
            }
        }
    }

    private void PublishInheritanceDisadvantage(
        IGameState gameState,
        IPerson source,
        IReadOnlyList<IPerson> heirs,
        IReadOnlyList<HousePropertyInfo> houses,
        IReadOnlyList<FarmlandAssetInfo> farmland,
        IReadOnlyList<HeirloomAssetInfo> heirlooms)
    {
        if (heirs.Count < 2)
            return;

        var heirIds = heirs
            .Select(heir => heir.Id)
            .ToList();
        var livingHeirIds = heirIds.ToHashSet();

        var hasExplicitDesignation = houses.Any(asset =>
                asset.AssignedHeirId is Guid id
                && livingHeirIds.Contains(id))
            || farmland.Any(asset =>
                asset.AssignedHeirId is Guid id
                && livingHeirIds.Contains(id))
            || heirlooms.Any(asset =>
                asset.AssignedHeirId is Guid id
                && livingHeirIds.Contains(id));

        if (!hasExplicitDesignation)
            return;

        var received = heirs.ToDictionary(heir => heir.Id, _ => 0m);

        var houseRecipients = HouseInheritanceAssignmentRules.ResolveRecipients(
            heirIds,
            houses.Select(asset => asset.AssignedHeirId).ToList());
        for (var index = 0; index < houses.Count; index++)
        {
            received[houseRecipients[index]] +=
                Math.Max(0m, _economy.GetHouseValue(houses[index]));
        }

        var farmlandValue = Math.Max(
            0m,
            _farmingResolver()?.PurchasePrice ?? 0m);
        var farmlandRecipients = HouseInheritanceAssignmentRules.ResolveRecipients(
            heirIds,
            farmland.Select(asset => asset.AssignedHeirId).ToList());
        for (var index = 0; index < farmland.Count; index++)
            received[farmlandRecipients[index]] += farmlandValue;

        var heirloomRecipients = HouseInheritanceAssignmentRules.ResolveRecipients(
            heirIds,
            heirlooms.Select(asset => asset.AssignedHeirId).ToList());
        for (var index = 0; index < heirlooms.Count; index++)
        {
            received[heirloomRecipients[index]] +=
                Math.Max(0m, heirlooms[index].AppraisedValue);
        }

        var maximum = received.Values.DefaultIfEmpty(0m).Max();
        if (maximum <= 0m)
            return;

        var favored = received
            .Where(pair => pair.Value == maximum)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var heir in heirs)
        {
            var receivedValue = received[heir.Id];
            string? severity = null;

            if (receivedValue == 0m)
            {
                severity = "skipped";
            }
            else if (receivedValue * 2m < maximum)
            {
                severity = "heavy";
            }

            if (severity is null)
                continue;

            var favoredIds = favored
                .Where(id => id != heir.Id)
                .ToList();

            _events.Publish(
                new GameEvent
                {
                    Type = "inheritance.disadvantaged",
                    Year = gameState.Year,
                    SubjectId = heir.Id,
                    RelatedPersonIds = [source.Id, .. favoredIds],
                    Data = new Dictionary<string, string>
                    {
                        ["sourceId"] = source.Id.ToString(),
                        ["severity"] = severity,
                        ["receivedAssetValue"] = receivedValue.ToString(CultureInfo.InvariantCulture),
                        ["favoredAssetValue"] = maximum.ToString(CultureInfo.InvariantCulture),
                        ["favoredHeirIds"] = string.Join(";", favoredIds),
                        ["suppressChronicle"] = "true",
                        ["text"] = severity == "skipped"
                            ? $"{_family.GetDisplayName(heir)} was passed over for designated property in {_family.GetDisplayName(source)}'s estate."
                            : $"{_family.GetDisplayName(heir)} received substantially less designated property than favored heirs in {_family.GetDisplayName(source)}'s estate."
                    }
                });
        }
    }

    private EstateHeirResolution ResolveEstateHeirs(
        IGameState gameState,
        IPerson? source)
    {
        if (source is null)
        {
            return new EstateHeirResolution(
                Array.Empty<IPerson>(),
                "no heirs");
        }

        var children =
            SortLiving(
                _family.GetChildren(source));

        if (children.Count > 0)
        {
            return new EstateHeirResolution(
                children,
                "the living children");
        }

        var spouse =
            _family.GetSpouse(source);

        if (spouse is not null
            && spouse.Tags.Has("state.alive"))
        {
            return new EstateHeirResolution(
                [spouse],
                "the surviving spouse");
        }

        var siblings =
            SortLiving(
                GetSiblings(
                    gameState,
                    source));

        if (siblings.Count > 0)
        {
            return new EstateHeirResolution(
                siblings,
                "the living siblings");
        }

        var parents =
            SortLiving(
                new[]
                {
                    _family.GetFather(source),
                    _family.GetMother(source)
                }
                .Where(person => person is not null)
                .Cast<IPerson>());

        if (parents.Count > 0)
        {
            return new EstateHeirResolution(
                parents,
                "the living parents");
        }

        var parentSiblings =
            GetParentSiblings(
                gameState,
                source);

        var cousins =
            SortLiving(
                parentSiblings
                    .SelectMany(relative => _family.GetChildren(relative))
                    .Where(relative => relative.Id != source.Id)
                    .DistinctBy(relative => relative.Id));

        if (cousins.Count > 0)
        {
            return new EstateHeirResolution(
                cousins,
                "the living first cousins");
        }

        var unclesAndAunts =
            SortLiving(
                parentSiblings);

        if (unclesAndAunts.Count > 0)
        {
            return new EstateHeirResolution(
                unclesAndAunts,
                "the living uncles and aunts");
        }

        return new EstateHeirResolution(
            Array.Empty<IPerson>(),
            "no heirs");
    }

    private IReadOnlyList<IPerson> GetSiblings(
        IGameState gameState,
        IPerson person)
    {
        var father =
            _family.GetFather(person);

        var mother =
            _family.GetMother(person);

        if (father is null
            && mother is null)
        {
            return Array.Empty<IPerson>();
        }

        return gameState.People
            .Where(candidate => candidate.Id != person.Id)
            .Where(candidate =>
                father is not null
                    && _family.GetFather(candidate)?.Id == father.Id
                || mother is not null
                    && _family.GetMother(candidate)?.Id == mother.Id)
            .DistinctBy(candidate => candidate.Id)
            .ToList();
    }

    private IReadOnlyList<IPerson> GetParentSiblings(
        IGameState gameState,
        IPerson person)
    {
        var result =
            new Dictionary<Guid, IPerson>();

        foreach (var parent in
            new[]
            {
                _family.GetFather(person),
                _family.GetMother(person)
            })
        {
            if (parent is null)
                continue;

            foreach (var sibling in
                GetSiblings(gameState, parent))
            {
                result[sibling.Id] = sibling;
            }
        }

        return result.Values.ToList();
    }

    private static IReadOnlyList<IPerson> SortLiving(
        IEnumerable<IPerson> people) =>
        people
            .Where(person => person.Tags.Has("state.alive"))
            .DistinctBy(person => person.Id)
            .OrderBy(person => person.BirthDate?.Year ?? int.MaxValue)
            .ThenBy(person => person.BirthDate?.Month ?? 1)
            .ThenBy(person => person.BirthDate?.Day ?? 1)
            .ThenBy(person => person.Id)
            .ToList();

    private sealed record EstateHeirResolution(
        IReadOnlyList<IPerson> Heirs,
        string Description);

    private bool HasEstablishedHouseholdOutsideEstate(
        IPerson person,
        Guid? estateHouseholdId)
    {
        if (person.Age < 18)
            return false;

        var householdId =
            _economy.GetHouseholdId(
                person);

        if (householdId is null
            || householdId
                == estateHouseholdId)
        {
            return false;
        }

        if (_economy.HasHousehold(
            person))
        {
            return true;
        }

        if (_economy
            .GetHouseholdDynastyAnchorId(
                person)
            == person.Id)
        {
            return true;
        }

        var spouse =
            _family.GetSpouse(
                person);

        return spouse is not null
            && _economy.GetHouseholdId(
                spouse)
                == householdId;
    }

    private void PublishCashEvent(
        IGameState gameState,
        IPerson source,
        IPerson heir,
        decimal amount,
        string type,
        string text)
    {
        _events.Publish(
            new GameEvent
            {
                Type =
                    type,

                Year =
                    gameState.Year,

                SubjectId =
                    heir.Id,

                RelatedPersonIds =
                    [source.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["amount"] =
                            amount.ToString(),

                        ["text"] =
                            text
                    }
            });
    }
}
