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
        _family = family;
        _economy = economy;
        _events = events;
    }

    public string Id =>
        "inheritance.estate_settlement";

    public YearPhase Phase =>
        YearPhase.Inheritance;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        var deceasedThisYear =
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.dead")
                        && person.DeathDate
                            is GameDate deathDate
                        && deathDate.Year
                            == gameState.Year)
                .ToList();

        foreach (var deceased in
            deceasedThisYear)
        {
            DistributeHouses(
                gameState,
                deceased);

            SettleRecordedUnions(
                gameState,
                deceased);
        }
    }

    private void DistributeHouses(
        IGameState gameState,
        IPerson deceased)
    {
        if (_family.GetSex(deceased)
                != Sex.Male
            || !_family.IsMaleLineage(
                deceased))
        {
            return;
        }

        var household =
            _economy.GetHousehold(
                deceased);

        if (household is null
            || household.HousesOwned <= 0)
        {
            return;
        }

        var livingSons =
            _family.GetChildren(
                deceased)
                .Where(
                    child =>
                        child.Tags.Has(
                            "state.alive")
                        && _family.GetSex(
                            child)
                            == Sex.Male)
                .ToList();

        if (livingSons.Count == 0)
            return;

        var houses =
            _economy.TakeAllHouses(
                deceased)
            .ToList();

        if (houses.Count == 0)
            return;

        var housesPerSon =
            houses.Count
            / livingSons.Count;

        var remainder =
            houses.Count
            % livingSons.Count;

        var houseIndex =
            0;

        foreach (var son in
            livingSons)
        {
            var inherited =
                housesPerSon;

            if (remainder > 0)
            {
                inherited++;
                remainder--;
            }

            if (inherited <= 0)
                continue;

            _economy.EnsureHousehold(
                son);

            var inheritedTowns =
                new List<string>();

            for (var index = 0;
                index < inherited;
                index++)
            {
                var house =
                    houses[
                        houseIndex++];

                _economy.AddExistingHouse(
                    son,
                    house);

                inheritedTowns.Add(
                    house.Town.Town);
            }

            _events.Publish(
                new GameEvent
                {
                    Type =
                        "inheritance.houses",

                    Year =
                        gameState.Year,

                    SubjectId =
                        son.Id,

                    RelatedPersonIds =
                        [deceased.Id],

                    Data =
                        new Dictionary<string, string>
                        {
                            ["count"] =
                                inherited.ToString(),

                            ["fatherId"] =
                                deceased.Id.ToString(),

                            ["towns"] =
                                string.Join(
                                    ", ",
                                    inheritedTowns),

                            ["text"] =
                                $"{_family.GetDisplayName(son)} " +
                                $"inherited {inherited} " +
                                $"house{(inherited == 1 ? "" : "s")} " +
                                $"from their late father."
                        }
                });
        }
    }

    private void SettleRecordedUnions(
        IGameState gameState,
        IPerson deceased)
    {
        foreach (var marriage in
            _family.GetRelationshipHistory(
                deceased))
        {
            var spouse =
                gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == marriage.SpouseId);

            if (spouse is null
                || !spouse.Tags.Has(
                    "state.dead"))
            {
                continue;
            }

            var male =
                _family.GetSex(deceased)
                    == Sex.Male
                        ? deceased
                        : spouse;

            var female =
                _family.GetSex(deceased)
                    == Sex.Female
                        ? deceased
                        : spouse;

            if (!_family.IsMaleLineage(
                male))
            {
                continue;
            }

            var maleHousehold =
                _economy.GetHousehold(
                    male);

            if (maleHousehold is null)
                continue;

            var livingHeirs =
                FindLivingChildrenOfUnion(
                    male,
                    female);

            var estate =
                maleHousehold.Wealth
                + _economy
                    .GetPendingInheritance(
                        male);

            if (livingHeirs.Count > 0
                && estate > 0)
            {
                SettleEstate(
                    gameState,
                    male,
                    female,
                    livingHeirs,
                    estate);
            }

            // The source clears the cash estate as soon as one
            // qualifying recorded union is processed.
            _economy.SetWealth(
                male,
                0);

            _economy.SetPendingInheritance(
                male,
                0);
        }
    }

    private IReadOnlyList<IPerson>
        FindLivingChildrenOfUnion(
            IPerson male,
            IPerson female)
    {
        return _family
            .GetChildren(
                male)
            .Where(
                child =>
                    child.Tags.Has(
                        "state.alive")
                    && _family.GetFather(
                        child)?.Id
                        == male.Id
                    && _family.GetMother(
                        child)?.Id
                        == female.Id)
            .ToList();
    }

    private void SettleEstate(
        IGameState gameState,
        IPerson male,
        IPerson female,
        IReadOnlyList<IPerson> livingHeirs,
        decimal estate)
    {
        var share =
            Math.Floor(
                estate
                / livingHeirs.Count);

        if (share <= 0)
            return;

        _events.Publish(
            new GameEvent
            {
                Type =
                    "inheritance.estate_settled",

                Year =
                    gameState.Year,

                SubjectId =
                    male.Id,

                RelatedPersonIds =
                    [female.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["estate"] =
                            estate.ToString(),

                        ["share"] =
                            share.ToString(),

                        ["heirs"] =
                            livingHeirs.Count.ToString(),

                        ["text"] =
                            $"The estate of the late " +
                            $"{_family.GetDisplayName(male)} and " +
                            $"{_family.GetDisplayName(female)} " +
                            $"(${estate:N0}) was settled."
                    }
            });

        foreach (var heir in
            livingHeirs)
        {
            DistributeCashShare(
                gameState,
                heir,
                share);
        }

        // Any remainder from floor division is intentionally lost
        // when the deceased male's estate is cleared.
    }

    private void DistributeCashShare(
        IGameState gameState,
        IPerson heir,
        decimal share)
    {
        if (heir.Age < 18)
        {
            _economy.ChangePendingInheritance(
                heir,
                share);

            PublishPending(
                gameState,
                heir,
                share,
                "inheritance.pending_minor",
                $"{_family.GetDisplayName(heir)} " +
                $"has an inheritance of ${share:N0} " +
                "waiting for adulthood.");

            return;
        }

        if (_family.GetSex(heir)
                == Sex.Male
            && _family.IsMaleLineage(
                heir))
        {
            _economy.EnsureHousehold(
                heir);

            _economy.ChangeWealth(
                heir,
                share);

            PublishReceived(
                gameState,
                heir,
                share,
                _family.GetDisplayName(
                    heir));

            return;
        }

        if (_family.GetSex(heir)
                == Sex.Female)
        {
            var husband =
                _family.GetSpouse(
                    heir);

            if (husband is not null
                && husband.Tags.Has(
                    "state.alive")
                && _economy.GetHousehold(
                    husband)
                    is not null)
            {
                _economy.ChangeWealth(
                    husband,
                    share);

                PublishReceived(
                    gameState,
                    husband,
                    share,
                    $"{_family.GetDisplayName(heir)}'s household",
                    heir.Id);

                return;
            }

            if (husband is null)
            {
                _economy.ChangePendingInheritance(
                    heir,
                    share);

                PublishPending(
                    gameState,
                    heir,
                    share,
                    "inheritance.claimable",
                    $"{_family.GetDisplayName(heir)} " +
                    $"received an inheritance of ${share:N0}, " +
                    "to be claimed upon marriage.");

                return;
            }
        }

        // The original can display a misleading "received" message
        // here even though no household balance is credited.
        // We preserve the state outcome (no recipient gets the money)
        // but make the event truthful.
        _events.Publish(
            new GameEvent
            {
                Type =
                    "inheritance.unclaimed",

                Year =
                    gameState.Year,

                SubjectId =
                    heir.Id,

                Data =
                    new Dictionary<string, string>
                    {
                        ["amount"] =
                            share.ToString(),

                        ["text"] =
                            $"{_family.GetDisplayName(heir)} " +
                            $"was due ${share:N0} from an inheritance, " +
                            "but had no eligible household to receive it."
                    }
            });
    }

    private void PublishPending(
        IGameState gameState,
        IPerson heir,
        decimal share,
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

                Data =
                    new Dictionary<string, string>
                    {
                        ["amount"] =
                            share.ToString(),

                        ["text"] =
                            text
                    }
            });
    }

    private void PublishReceived(
        IGameState gameState,
        IPerson recipient,
        decimal share,
        string recipientName,
        Guid? heirId = null)
    {
        var related =
            heirId is Guid id
                ? new List<Guid>
                    {
                        id
                    }
                : new List<Guid>();

        _events.Publish(
            new GameEvent
            {
                Type =
                    "inheritance.received",

                Year =
                    gameState.Year,

                SubjectId =
                    recipient.Id,

                RelatedPersonIds =
                    related,

                Data =
                    new Dictionary<string, string>
                    {
                        ["amount"] =
                            share.ToString(),

                        ["text"] =
                            $"{recipientName} received " +
                            $"an inheritance of ${share:N0}."
                    }
            });
    }
}
