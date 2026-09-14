using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

public sealed class EstateInheritanceSystem :
    IYearSystem
{
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IGameEventBus _events;

    public EstateInheritanceSystem(
        IFamilyService family,
        IEconomyService economy,
        IGameEventBus events)
    {
        _family =
            family;

        _economy =
            economy;

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

        var wealth =
            finance.Wealth;

        IReadOnlyList<IPerson> heirs =
            anchor is null
                ? Array.Empty<IPerson>()
                : _family
                    .GetChildren(
                        anchor)
                    .Where(
                        child =>
                            child.Tags.Has(
                                "state.alive"))
                    .OrderBy(
                        child =>
                            child.BirthDate?.Year
                            ?? int.MaxValue)
                    .ThenBy(
                        child =>
                            child.BirthDate?.Month
                            ?? 1)
                    .ThenBy(
                        child =>
                            child.BirthDate?.Day
                            ?? 1)
                    .ThenBy(
                        child =>
                            child.Id)
                    .ToList();

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

                            ["text"] =
                                $"The remaining estate of " +
                                $"{(anchor is null ? _family.GetDisplayName(head) : _family.GetDisplayName(anchor))} " +
                                "left the dynasty because there were no living children to inherit it."
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

        DistributeCash(
            gameState,
            anchor
            ?? head,
            heirs,
            wealth,
            estateHouseholdId);

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

                        ["heirs"] =
                            heirs.Count.ToString(),

                        ["text"] =
                            $"The remaining household estate of " +
                            $"{(anchor is null ? _family.GetDisplayName(head) : _family.GetDisplayName(anchor))} " +
                            "was divided among the living children."
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

            foreach (var house in
                houses)
            {
                _economy.AddExistingHouse(
                    anchor,
                    house);
            }
        }
        else
        {
            if (wealth > 0)
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

    private void DistributeCash(
        IGameState gameState,
        IPerson source,
        IReadOnlyList<IPerson> heirs,
        decimal wealth,
        Guid? estateHouseholdId)
    {
        if (wealth <= 0)
            return;

        // Dynastia displays and prices money in whole zł units. Divide
        // those units evenly and hand any remainder to the eldest heirs
        // first. If an old save somehow contains a fractional zł residue,
        // preserve it by adding that residue to the eldest share.
        var wholeUnits =
            decimal.ToInt64(
                decimal.Truncate(
                    wealth));

        var fractionalResidue =
            wealth
            - wholeUnits;

        var baseUnits =
            wholeUnits
            / heirs.Count;

        var remainder =
            wholeUnits
            % heirs.Count;

        for (var index = 0;
            index < heirs.Count;
            index++)
        {
            var heir =
                heirs[index];

            var amount =
                baseUnits
                + (
                    index < remainder
                        ? 1m
                        : 0m
                )
                + (
                    index == 0
                        ? fractionalResidue
                        : 0m
                );

            if (amount <= 0)
                continue;

            var hasOwnHousehold =
                HasEstablishedHouseholdOutsideEstate(
                    heir,
                    estateHouseholdId);

            if (hasOwnHousehold)
            {
                _economy.ChangeWealth(
                    heir,
                    amount);

                PublishCashEvent(
                    gameState,
                    source,
                    heir,
                    amount,
                    "inheritance.received",
                    $"{_family.GetDisplayName(heir)} received " +
                    $"an inheritance of {amount:N0} zł.");
            }
            else
            {
                _economy.ChangePendingInheritance(
                    heir,
                    amount);

                PublishCashEvent(
                    gameState,
                    source,
                    heir,
                    amount,
                    "inheritance.pending",
                    $"{_family.GetDisplayName(heir)} has " +
                    $"an inheritance of {amount:N0} zł waiting until " +
                    "they establish a household.");
            }
        }
    }

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
